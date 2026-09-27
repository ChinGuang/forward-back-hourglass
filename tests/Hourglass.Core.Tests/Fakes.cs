using Hourglass.Core;

namespace Hourglass.Core.Tests;

internal sealed class FakeTicker : ITicker
{
    public event EventHandler<TimeSpan>? Tick;

    public bool IsRunning { get; private set; }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;

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
