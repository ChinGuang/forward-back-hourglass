using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class TimeFormatterTests
{
    [Theory]
    [InlineData(0, "00:00:00.0")]
    [InlineData(99, "00:00:00.0")]
    [InlineData(100, "00:00:00.1")]
    [InlineData(61_950, "00:01:01.9")]
    [InlineData(3_723_400, "01:02:03.4")]
    [InlineData(360_000_000, "100:00:00.0")]
    public void Format_UsesHoursMinutesSecondsTenths(long milliseconds, string expected)
    {
        Assert.Equal(expected, TimeFormatter.Format(TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Theory]
    [InlineData(0, "00:00:00.0")]
    [InlineData(1, "00:00:00.1")]              // any time left (here 100 ns) still shows a tenth
    [InlineData(99_670_000, "00:00:10.0")]     // 9.967 s: doesn't drop a tenth the moment a countdown starts
    [InlineData(100_000_000, "00:00:10.0")]    // exact tenths are unchanged
    [InlineData(599_999_900, "00:01:00.0")]    // 59.99999 s: rounding carries into minutes
    public void Format_RoundUp_ForCountdowns(long ticks, string expected)
    {
        var value = TimeSpan.FromTicks(ticks);

        Assert.Equal(expected, TimeFormatter.Format(value, roundUp: true));
    }

    [Fact]
    public void Format_RoundUp_AtMaxValue_DoesNotOverflow()
    {
        Assert.StartsWith("256204778:", TimeFormatter.Format(TimeSpan.MaxValue, roundUp: true));
    }

    [Fact]
    public void Format_NegativeShowsZero()
    {
        Assert.Equal("00:00:00.0", TimeFormatter.Format(TimeSpan.FromSeconds(-3)));
    }
}
