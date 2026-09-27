using Hourglass.Core;

namespace Hourglass.Core.Tests;

/// <summary>Saving the timer on close and restoring it on the next launch.</summary>
public class TimerPersistenceTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public void RunningForward_RoundTrips_AndKeepsCounting()
    {
        var original = new HourglassTimer();
        original.Start();
        original.Advance(TimeSpan.FromSeconds(42));

        var restored = new HourglassTimer();
        Assert.True(restored.Restore(original.ToSnapshot()));
        restored.Advance(OneSecond);

        Assert.Equal(TimerState.Forward, restored.State);
        Assert.Equal(TimeSpan.FromSeconds(43), restored.Value);
    }

    [Fact]
    public void RunningBackward_RoundTrips_WithItsPeak_AndStillRingsAtZero()
    {
        var original = new HourglassTimer();
        original.Start();
        original.Advance(TimeSpan.FromSeconds(10));
        original.Backward();
        original.Advance(TimeSpan.FromSeconds(8));

        var restored = new HourglassTimer();
        restored.Restore(original.ToSnapshot());

        Assert.Equal(TimerState.Backward, restored.State);
        Assert.Equal(TimeSpan.FromSeconds(2), restored.Value);
        Assert.Equal(TimeSpan.FromSeconds(10), restored.BackwardPeak);

        restored.Advance(TimeSpan.FromSeconds(2));
        Assert.True(restored.IsRinging);
    }

    [Fact]
    public void PausedBackward_RoundTrips_AndResumingKeepsThePeak()
    {
        var original = new HourglassTimer();
        original.Start();
        original.Advance(TimeSpan.FromSeconds(10));
        original.Backward();
        original.Advance(TimeSpan.FromSeconds(4));
        original.Pause();

        var restored = new HourglassTimer();
        restored.Restore(original.ToSnapshot());
        restored.Backward();

        Assert.Equal(TimerState.Backward, restored.State);
        Assert.Equal(TimeSpan.FromSeconds(10), restored.BackwardPeak);
        Assert.Equal(TimeSpan.FromSeconds(6), restored.Value);
    }

    [Fact]
    public void ClosedWhileRinging_ReopensDrainedButSilent()
    {
        var original = new HourglassTimer();
        original.Start();
        original.Advance(OneSecond);
        original.Backward();
        original.Advance(OneSecond);
        Assert.True(original.IsRinging);

        var restored = new HourglassTimer();
        Assert.True(restored.Restore(original.ToSnapshot()));

        Assert.False(restored.IsRinging);
        Assert.Equal(TimerState.Idle, restored.State);
        Assert.Equal(TimerDirection.Backward, restored.LastDirection);
        Assert.Equal(new SandLevel(0, 1, false), SandLevel.From(restored));
        Assert.True(restored.CanReset);
    }

    [Fact]
    public void FreshTimer_RoundTrips()
    {
        var restored = new HourglassTimer();

        Assert.True(restored.Restore(new HourglassTimer().ToSnapshot()));
        Assert.Equal(TimerState.Idle, restored.State);
        Assert.Equal(TimerDirection.None, restored.LastDirection);
        Assert.Equal(TimeSpan.Zero, restored.Value);
    }

    [Fact]
    public void Restore_KeepsCurrentSpeeds()
    {
        var timer = new HourglassTimer { ForwardSpeed = 4, BackwardSpeed = 0.5 };

        timer.Restore(new TimerSnapshot(TimeSpan.FromSeconds(5), TimerState.Paused, TimerDirection.Forward, TimeSpan.Zero));

        Assert.Equal(4, timer.ForwardSpeed);
        Assert.Equal(0.5, timer.BackwardSpeed);
    }

    public static TheoryData<TimerSnapshot> InvalidSnapshots => new()
    {
        new(TimeSpan.FromSeconds(-1), TimerState.Paused, TimerDirection.Forward, TimeSpan.Zero),       // negative time
        new(TimeSpan.FromSeconds(5), TimerState.Paused, TimerDirection.Backward, TimeSpan.FromSeconds(-1)), // negative peak
        new(TimeSpan.FromSeconds(5), (TimerState)99, TimerDirection.Forward, TimeSpan.Zero),           // unknown state
        new(TimeSpan.FromSeconds(5), TimerState.Paused, (TimerDirection)7, TimeSpan.Zero),             // unknown direction
        new(TimeSpan.FromSeconds(5), TimerState.Forward, TimerDirection.Backward, TimeSpan.Zero),      // state/direction disagree
        new(TimeSpan.FromSeconds(5), TimerState.Backward, TimerDirection.Forward, TimeSpan.Zero),
        new(TimeSpan.Zero, TimerState.Backward, TimerDirection.Backward, TimeSpan.FromSeconds(5)),     // counting down at zero
        new(TimeSpan.FromSeconds(5), TimerState.Paused, TimerDirection.None, TimeSpan.Zero),           // paused but never ran
        new(TimeSpan.FromSeconds(5), TimerState.Idle, TimerDirection.None, TimeSpan.Zero),             // idle with time left
        new(TimeSpan.Zero, TimerState.Idle, TimerDirection.Forward, TimeSpan.Zero),                    // idle is never "after forward"
    };

    [Theory]
    [MemberData(nameof(InvalidSnapshots))]
    public void InvalidSnapshot_IsRejected_AndLeavesAFreshTimer(TimerSnapshot snapshot)
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(3));

        Assert.False(timer.Restore(snapshot));

        Assert.Equal(TimerState.Idle, timer.State);
        Assert.Equal(TimerDirection.None, timer.LastDirection);
        Assert.Equal(TimeSpan.Zero, timer.Value);
    }

    [Fact]
    public void BackwardPeakBelowValue_IsRaisedToTheValue()
    {
        var timer = new HourglassTimer();

        Assert.True(timer.Restore(new TimerSnapshot(TimeSpan.FromSeconds(8), TimerState.Paused, TimerDirection.Backward, TimeSpan.FromSeconds(2))));

        Assert.Equal(TimeSpan.FromSeconds(8), timer.BackwardPeak);
        Assert.Equal(1, SandLevel.From(timer).Upper, precision: 6);
    }

    [Fact]
    public void ForwardSnapshot_DoesNotCarryABackwardPeak()
    {
        var timer = new HourglassTimer();

        timer.Restore(new TimerSnapshot(TimeSpan.FromSeconds(8), TimerState.Forward, TimerDirection.Forward, TimeSpan.FromSeconds(30)));

        Assert.Equal(TimeSpan.Zero, timer.BackwardPeak);
    }
}
