using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class SpeedPresetsTests
{
    [Fact]
    public void Presets_AreTheAgreedMultipliers()
    {
        Assert.Equal([0.25, 0.5, 1.0, 2.0, 4.0, 8.0], SpeedPresets.All);
    }

    [Theory]
    [InlineData(0.25, "0.25×")]
    [InlineData(0.5, "0.5×")]
    [InlineData(1, "1×")]
    [InlineData(8, "8×")]
    public void Label_FormatsMultiplier(double speed, string expected)
    {
        Assert.Equal(expected, SpeedPresets.Label(speed));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 4)] // log-scale: 3 is closer to 4 than to 2
    [InlineData(0.3, 0.25)]
    [InlineData(100, 8)]
    [InlineData(0.001, 0.25)]
    [InlineData(0, 1)]
    [InlineData(-2, 1)]
    [InlineData(double.NaN, 1)]
    public void Normalize_SnapsToNearestPreset(double input, double expected)
    {
        Assert.Equal(expected, SpeedPresets.Normalize(input));
    }
}
