using Hourglass.Core;

namespace Hourglass.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "hourglass-tests-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "nested", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = new JsonSettingsStore(SettingsPath);

        Assert.Equal(new HourglassSettings(1, 1), store.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTrips_AndCreatesDirectory()
    {
        var store = new JsonSettingsStore(SettingsPath);

        store.Save(new HourglassSettings(2, 0.25));

        Assert.True(File.Exists(SettingsPath));
        Assert.Equal(new HourglassSettings(2, 0.25), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ not json");

        Assert.Equal(new HourglassSettings(), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Load_OutOfRangeSpeeds_AreSnappedToPresets()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "ForwardSpeed": 1000, "BackwardSpeed": -3 }""");

        Assert.Equal(new HourglassSettings(8, 1), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void DefaultPath_IsUnderAppDataFolder()
    {
        Assert.EndsWith(Path.Combine("ForwardBackHourglass", "settings.json"), JsonSettingsStore.DefaultPath);
    }
}
