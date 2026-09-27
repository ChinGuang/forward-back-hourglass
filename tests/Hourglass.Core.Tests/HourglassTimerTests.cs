using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class HourglassTimerTests
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public void NewTimer_IsIdleAtZero()
    {
        var timer = new HourglassTimer();

        Assert.Equal(TimeSpan.Zero, timer.Value);
        Assert.Equal(TimerState.Idle, timer.State);
        Assert.False(timer.IsRinging);
        Assert.Equal(1.0, timer.ForwardSpeed);
        Assert.Equal(1.0, timer.BackwardSpeed);
    }

    [Fact]
    public void Advance_WhileIdle_DoesNothing()
    {
        var timer = new HourglassTimer();

        timer.Advance(OneSecond);

        Assert.Equal(TimeSpan.Zero, timer.Value);
    }

    [Fact]
    public void Start_CountsForward()
    {
        var timer = new HourglassTimer();

        Assert.True(timer.Start());
        timer.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(TimerState.Forward, timer.State);
        Assert.Equal(TimeSpan.FromSeconds(3), timer.Value);
    }

    [Fact]
    public void Forward_HasNoUpperLimit()
    {
        var timer = new HourglassTimer();
        timer.Start();

        timer.Advance(TimeSpan.FromDays(3));

        Assert.Equal(TimeSpan.FromDays(3), timer.Value);
        Assert.Equal(TimerState.Forward, timer.State);
    }

    [Fact]
    public void Forward_SaturatesInsteadOfOverflowing()
    {
        var timer = new HourglassTimer { ForwardSpeed = 8 };
        timer.Start();

        timer.Advance(TimeSpan.MaxValue / 16);
        timer.Advance(TimeSpan.MaxValue / 16);
        timer.Advance(TimeSpan.MaxValue / 16);

        Assert.Equal(TimeSpan.MaxValue, timer.Value);
    }

    [Theory]
    [InlineData(0.25, 250)]
    [InlineData(0.5, 500)]
    [InlineData(2, 2000)]
    [InlineData(8, 8000)]
    public void ForwardSpeed_ScalesElapsedTime(double speed, int expectedMs)
    {
        var timer = new HourglassTimer { ForwardSpeed = speed };
        timer.Start();

        timer.Advance(OneSecond);

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), timer.Value);
    }

    [Fact]
    public void Backward_CountsDownAtBackwardSpeed()
    {
        var timer = new HourglassTimer { BackwardSpeed = 4 };
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(10));

        Assert.True(timer.Backward());
        timer.Advance(OneSecond);

        Assert.Equal(TimerState.Backward, timer.State);
        Assert.Equal(TimeSpan.FromSeconds(6), timer.Value);
    }

    [Fact]
    public void Backward_AtZero_IsIgnored()
    {
        var timer = new HourglassTimer();

        Assert.False(timer.CanGoBackward);
        Assert.False(timer.Backward());
        Assert.Equal(TimerState.Idle, timer.State);
    }

    [Fact]
    public void Backward_ReachingZero_ClampsStopsAndRingsOnce()
    {
        var timer = new HourglassTimer();
        int rings = 0;
        timer.ReachedZero += (_, _) => rings++;
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(2));
        timer.Backward();

        timer.Advance(TimeSpan.FromSeconds(5)); // overshoots zero
        timer.Advance(TimeSpan.FromSeconds(5)); // already stopped

        Assert.Equal(TimeSpan.Zero, timer.Value);
        Assert.Equal(TimerState.Idle, timer.State);
        Assert.True(timer.IsRinging);
        Assert.Equal(1, rings);
    }

    [Fact]
    public void Backward_LandingExactlyOnZero_Rings()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(OneSecond);
        timer.Backward();

        timer.Advance(OneSecond);

        Assert.Equal(TimeSpan.Zero, timer.Value);
        Assert.True(timer.IsRinging);
    }

    [Fact]
    public void SwitchingDirectionMidRun_KeepsValue()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(10));

        timer.Backward();
        timer.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(TimeSpan.FromSeconds(7), timer.Value);

        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(8), timer.Value);
        Assert.Equal(TimerState.Forward, timer.State);
    }

    [Fact]
    public void Start_WhileAlreadyForward_IsIgnored()
    {
        var timer = new HourglassTimer();
        timer.Start();

        Assert.False(timer.CanStart);
        Assert.False(timer.Start());
    }

    [Fact]
    public void Backward_WhileAlreadyBackward_IsIgnoredAndKeepsPeak()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(10));
        timer.Backward();
        timer.Advance(TimeSpan.FromSeconds(4));

        Assert.False(timer.Backward());
        Assert.Equal(TimeSpan.FromSeconds(10), timer.BackwardPeak);
    }

    [Fact]
    public void Pause_FreezesValue()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(5));

        Assert.True(timer.Pause());
        timer.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal(TimerState.Paused, timer.State);
        Assert.Equal(TimeSpan.FromSeconds(5), timer.Value);
    }

    [Fact]
    public void Pause_WhenNotRunning_IsIgnored()
    {
        var timer = new HourglassTimer();

        Assert.False(timer.Pause());
        Assert.Equal(TimerState.Idle, timer.State);
    }

    [Fact]
    public void Pause_ThenStart_ResumesForward()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(5));
        timer.Pause();

        timer.Start();
        timer.Advance(OneSecond);

        Assert.Equal(TimeSpan.FromSeconds(6), timer.Value);
    }

    [Fact]
    public void PausedBackward_ResumedBackward_KeepsOriginalPeak()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(10));
        timer.Backward();
        timer.Advance(TimeSpan.FromSeconds(4));
        timer.Pause();

        timer.Backward();
        timer.Advance(OneSecond);

        Assert.Equal(TimeSpan.FromSeconds(10), timer.BackwardPeak);
        Assert.Equal(TimeSpan.FromSeconds(5), timer.Value);
    }

    [Fact]
    public void PausedForward_ThenBackward_UsesCurrentValueAsPeak()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(10));
        timer.Pause();

        timer.Backward();

        Assert.Equal(TimeSpan.FromSeconds(10), timer.BackwardPeak);
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(10));
        timer.Backward();

        timer.Reset();

        Assert.Equal(TimeSpan.Zero, timer.Value);
        Assert.Equal(TimeSpan.Zero, timer.BackwardPeak);
        Assert.Equal(TimerState.Idle, timer.State);
        Assert.Equal(TimerDirection.None, timer.LastDirection);
        Assert.False(timer.CanReset);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("reset")]
    [InlineData("stopRing")]
    public void RingIsStoppedBy(string action)
    {
        var timer = RingingTimer();

        switch (action)
        {
            case "start": timer.Start(); break;
            case "reset": timer.Reset(); break;
            case "stopRing": timer.StopRing(); break;
        }

        Assert.False(timer.IsRinging);
    }

    [Fact]
    public void WhileRinging_BackwardIsDisabled_BecauseValueIsZero()
    {
        var timer = RingingTimer();

        Assert.False(timer.CanGoBackward);
        Assert.False(timer.Backward());
        Assert.True(timer.IsRinging);
    }

    [Fact]
    public void ChangingSpeedWhileRunning_AppliesToLaterTicks()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(OneSecond);

        timer.ForwardSpeed = 4;
        timer.Advance(OneSecond);

        Assert.Equal(TimeSpan.FromSeconds(5), timer.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidSpeed_Throws(double speed)
    {
        var timer = new HourglassTimer();

        Assert.Throws<ArgumentOutOfRangeException>(() => timer.ForwardSpeed = speed);
        Assert.Throws<ArgumentOutOfRangeException>(() => timer.BackwardSpeed = speed);
    }

    [Fact]
    public void NegativeOrZeroElapsed_IsIgnored()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(OneSecond);

        timer.Advance(TimeSpan.Zero);
        timer.Advance(TimeSpan.FromSeconds(-5));

        Assert.Equal(OneSecond, timer.Value);
    }

    private static HourglassTimer RingingTimer()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(OneSecond);
        timer.Backward();
        timer.Advance(OneSecond);
        Assert.True(timer.IsRinging);
        return timer;
    }
}
