using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class SandLevelTests
{
    [Fact]
    public void FreshHourglass_HasAllSandOnTop()
    {
        var level = SandLevel.Compute(TimerDirection.None, TimeSpan.Zero, TimeSpan.Zero, isRunning: false);

        Assert.Equal(new SandLevel(1, 0, false), level);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 0.25)]
    [InlineData(30, 0.5)]
    [InlineData(60, 0)] // cycle restarts every 60 s
    [InlineData(75, 0.25)]
    public void Forward_FillsLowerBulbOverSixtySecondCycle(double seconds, double expectedLower)
    {
        var level = SandLevel.Compute(TimerDirection.Forward, TimeSpan.FromSeconds(seconds), TimeSpan.Zero, isRunning: true);

        Assert.Equal(expectedLower, level.Lower, precision: 6);
        Assert.Equal(1 - expectedLower, level.Upper, precision: 6);
        Assert.True(level.IsFlowing);
    }

    [Theory]
    [InlineData(40, 1)]
    [InlineData(30, 0.75)]
    [InlineData(10, 0.25)]
    [InlineData(0, 0)]
    public void Backward_UpperBulbIsRemainingOverPeak(double seconds, double expectedUpper)
    {
        var level = SandLevel.Compute(TimerDirection.Backward, TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(40), isRunning: true);

        Assert.Equal(expectedUpper, level.Upper, precision: 6);
        Assert.Equal(1 - expectedUpper, level.Lower, precision: 6);
    }

    [Fact]
    public void Backward_WithZeroPeak_IsEmptyAndDoesNotDivideByZero()
    {
        var level = SandLevel.Compute(TimerDirection.Backward, TimeSpan.Zero, TimeSpan.Zero, isRunning: false);

        Assert.Equal(new SandLevel(0, 1, false), level);
    }

    [Fact]
    public void From_Timer_ReflectsDrainAfterReachingZero()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromSeconds(4));
        timer.Backward();
        timer.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(0.75, SandLevel.From(timer).Upper, precision: 6);

        timer.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(new SandLevel(0, 1, false), SandLevel.From(timer));
    }
}
