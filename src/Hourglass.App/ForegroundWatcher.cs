using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Threading;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>
/// Checks about twice a second which window is in front and, for supported browsers, what its address bar shows.
/// Runs on a background thread (UI Automation calls into other apps can be slow) and reports changes on the UI thread.
/// </summary>
public sealed class ForegroundWatcher(Dispatcher dispatcher) : IForegroundWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    // After a failed search for a window's address bar, wait this long before searching that window again.
    private static readonly TimeSpan SearchRetry = TimeSpan.FromSeconds(2);

    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private CancellationTokenSource? _running;

    public event EventHandler<ActiveWindow?>? Changed;

    public void Start()
    {
        if (_running is not null)
        {
            return;
        }

        var running = new CancellationTokenSource();
        _running = running;
        var thread = new Thread(() => Watch(running.Token)) { IsBackground = true, Name = "Foreground watcher" };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
    }

    public void Stop()
    {
        _running?.Cancel();
        _running = null;
    }

    private void Watch(CancellationToken stop)
    {
        var addressBars = new Dictionary<IntPtr, CachedAddressBar>();
        ActiveWindow? last = null;
        bool reported = false;

        while (!stop.IsCancellationRequested)
        {
            ActiveWindow? current = ReadActiveWindow(addressBars);
            if (!reported || current != last)
            {
                reported = true;
                last = current;
                dispatcher.BeginInvoke(() =>
                {
                    // Drop readings that arrive after auto mode was switched off.
                    if (!stop.IsCancellationRequested)
                    {
                        Changed?.Invoke(this, current);
                    }
                });
            }

            stop.WaitHandle.WaitOne(PollInterval);
        }
    }

    /// <summary>The window in front, or null for none / the hourglass itself / something that can't be identified.</summary>
    private ActiveWindow? ReadActiveWindow(Dictionary<IntPtr, CachedAddressBar> addressBars)
    {
        IntPtr window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(window, out uint processId);
        if (processId == 0 || processId == _ownProcessId)
        {
            return null;
        }

        string? fileName = NativeMethods.GetProcessFileName(processId);
        if (fileName is null)
        {
            return null;
        }

        string? url = KnownBrowsers.Find(fileName) is { } browser ? ReadAddressBar(window, browser, addressBars) : null;
        return new ActiveWindow(fileName, window.ToInt64(), url);
    }

    private static string? ReadAddressBar(IntPtr window, BrowserProfile browser, Dictionary<IntPtr, CachedAddressBar> addressBars)
    {
        if (addressBars.Count > 32 && !addressBars.ContainsKey(window))
        {
            addressBars.Clear();
        }

        addressBars.TryGetValue(window, out CachedAddressBar? cached);
        cached ??= addressBars[window] = new CachedAddressBar();

        try
        {
            if (cached.Element is null && DateTime.UtcNow >= cached.NextSearch)
            {
                cached.NextSearch = DateTime.UtcNow + SearchRetry;
                var found = AddressBarFinder.Find(new UiaNode(AutomationElement.FromHandle(window)), browser) as UiaNode;
                cached.Element = found?.Element;
            }

            return cached.Element is null ? null : UiaNode.ReadValue(cached.Element);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or ArgumentException)
        {
            // The window or its toolbar went away (closed, full screen, re-created): search again later.
            cached.Element = null;
            return null;
        }
    }

    private sealed class CachedAddressBar
    {
        public AutomationElement? Element { get; set; }

        public DateTime NextSearch { get; set; }
    }
}
