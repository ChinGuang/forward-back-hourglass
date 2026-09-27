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

    [Fact]
    public void Format_NegativeShowsZero()
    {
        Assert.Equal("00:00:00.0", TimeFormatter.Format(TimeSpan.FromSeconds(-3)));
    }
}
