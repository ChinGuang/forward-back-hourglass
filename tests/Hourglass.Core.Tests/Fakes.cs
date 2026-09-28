using Hourglass.Core;

namespace Hourglass.Core.Tests;

internal sealed class FakeTicker : ITicker
{
    public event EventHandler<TimeSpan>? Tick;

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    /// <summary>Time "since the last tick" that <see cref="Flush"/> delivers.</summary>
    public TimeSpan Pending { get; set; }

    public void Flush()
    {
        if (IsRunning && Pending > TimeSpan.Zero)
        {
            Tick?.Invoke(this, Pending);
        }

        Pending = TimeSpan.Zero;
    }

    /// <summary>Simulates the UI timer firing; only delivers ticks while started, like the real one.</summary>
    public void Elapse(TimeSpan elapsed)
    {
        if (IsRunning)
        {
            Tick?.Invoke(this, elapsed);
        }
    }

    public void Elapse(double seconds) => Elapse(TimeSpan.FromSeconds(seconds));
}

internal sealed class FakeAlarm : IAlarmPlayer
{
    public bool IsPlaying { get; private set; }

    public int PlayCount { get; private set; }

    public void Play()
    {
        IsPlaying = true;
        PlayCount++;
    }

    public void Stop() => IsPlaying = false;
}

internal sealed class FakeSettingsStore(HourglassSettings? initial = null) : ISettingsStore
{
    public HourglassSettings Current { get; private set; } = initial ?? new HourglassSettings();

    public int SaveCount { get; private set; }

    public HourglassSettings Load() => Current;

    public void Save(HourglassSettings settings)
    {
        Current = settings;
        SaveCount++;
    }
}

internal sealed class FakeForegroundWatcher : IForegroundWatcher
{
    public event EventHandler<ActiveWindow?>? Changed;

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

    public void Show(ActiveWindow? window) => Changed?.Invoke(this, window);

    public void ShowApp(string fileName, long handle = 1) => Show(new ActiveWindow(fileName, handle));

    public void ShowBrowser(string url, string browser = "brave.exe", long handle = 100) =>
        Show(new ActiveWindow(browser, handle, url));
}

/// <summary>A node in a fake accessibility tree. Counts how often its children are enumerated.</summary>
internal sealed class FakeUi(UiControlType type, string name = "", string? value = null, params FakeUi[] children) : IUiElement
{
    public UiControlType ControlType { get; } = type;

    public string Name { get; } = name;

    public string? Value { get; } = value;

    public int ChildReads { get; private set; }

    public IEnumerable<IUiElement> Children
    {
        get
        {
            ChildReads++;
            return children;
        }
    }

    public static FakeUi Pane(params FakeUi[] children) => new(UiControlType.Other, "", null, children);

    public static FakeUi Doc(params FakeUi[] children) => new(UiControlType.Document, "", null, children);

    public static FakeUi Edit(string name, string? value) => new(UiControlType.Edit, name, value);
}
