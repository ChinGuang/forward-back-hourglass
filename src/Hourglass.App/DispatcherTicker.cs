using System.Diagnostics;
using System.Windows.Threading;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>
/// Ticks on the UI thread about 30 times a second. Elapsed time comes from a <see cref="Stopwatch"/>,
/// not from counting ticks, so a busy or throttled UI thread never makes the timer drift.
/// </summary>
public sealed class DispatcherTicker : ITicker
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _last;

    public DispatcherTicker()
    {
        _timer.Tick += OnTimerTick;
    }

    public event EventHandler<TimeSpan>? Tick;

    public void Start()
    {
        _last = TimeSpan.Zero;
        _stopwatch.Restart();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _stopwatch.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        TimeSpan now = _stopwatch.Elapsed;
        TimeSpan elapsed = now - _last;
        _last = now;
        Tick?.Invoke(this, elapsed);
    }
}
