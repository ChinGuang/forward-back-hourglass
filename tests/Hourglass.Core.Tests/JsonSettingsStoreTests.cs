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
    public void Load_ReturnsStoredValuesAsIs()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "ForwardSpeed": 1000, "BackwardSpeed": -3 }""");

        Assert.Equal(new HourglassSettings(1000, -3), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Save_WithBareFileName_DoesNotThrow()
    {
        string original = Environment.CurrentDirectory;
        Directory.CreateDirectory(_dir);
        try
        {
            Environment.CurrentDirectory = _dir;
            var store = new JsonSettingsStore("settings.json");

            store.Save(new HourglassSettings(2, 2));

            Assert.Equal(new HourglassSettings(2, 2), store.Load());
        }
        finally
        {
            Environment.CurrentDirectory = original;
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheTimer()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var timer = new TimerSnapshot(
            TimeSpan.FromSeconds(12.345), TimerState.Backward, TimerDirection.Backward, TimeSpan.FromMinutes(3));

        store.Save(new HourglassSettings(2, 4, timer));

        Assert.Equal(new HourglassSettings(2, 4, timer), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Load_FileFromPreviousVersion_HasNoTimer_ButKeepsSpeeds()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "ForwardSpeed": 2, "BackwardSpeed": 0.5 }""");

        var loaded = new JsonSettingsStore(SettingsPath).Load();

        Assert.Equal(new HourglassSettings(2, 0.5), loaded);
        Assert.Null(loaded.Timer);
    }

    [Fact]
    public void Save_WritesStatesByName()
    {
        var store = new JsonSettingsStore(SettingsPath);

        store.Save(new HourglassSettings(1, 1,
            new TimerSnapshot(TimeSpan.FromSeconds(3), TimerState.Paused, TimerDirection.Backward, TimeSpan.FromSeconds(9))));

        string json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"Paused\"", json);
        Assert.Contains("\"Backward\"", json);
    }

    [Theory]
    [InlineData("""{ "Value": "not a time", "State": "Paused", "LastDirection": "Forward", "BackwardPeak": "00:00:00" }""")]
    [InlineData("""{ "Value": "00:00:05", "State": "Sideways", "LastDirection": "Forward", "BackwardPeak": "00:00:00" }""")]
    [InlineData("\"just a string\"")]
    public void Load_UnreadableTimer_IsDropped_ButSpeedsAreKept(string timerJson)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, $$"""{ "ForwardSpeed": 4, "BackwardSpeed": 0.5, "Timer": {{timerJson}} }""");

        Assert.Equal(new HourglassSettings(4, 0.5), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Load_NumericStates_AreStillAccepted()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath,
            """{ "ForwardSpeed": 1, "BackwardSpeed": 1, "Timer": { "Value": "00:00:05", "State": 3, "LastDirection": 1, "BackwardPeak": "00:00:00" } }""");

        Assert.Equal(
            new TimerSnapshot(TimeSpan.FromSeconds(5), TimerState.Paused, TimerDirection.Forward, TimeSpan.Zero),
            new JsonSettingsStore(SettingsPath).Load().Timer);
    }

    [Fact]
    public void Load_NonObjectFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "[1, 2, 3]");

        Assert.Equal(new HourglassSettings(), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAutoModeAndRules()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var rules = new AutoRules(
            [new AppRule("Code.exe", RuleAction.Forward)],
            [new SiteRule("youtube.com", RuleAction.Backward), new SiteRule("news.com", RuleAction.Pause)]);

        store.Save(new HourglassSettings(2, 4, AutoMode: true, Rules: rules));

        Assert.Equal(new HourglassSettings(2, 4, AutoMode: true, Rules: rules), new JsonSettingsStore(SettingsPath).Load());
        Assert.Contains("\"Backward\"", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Load_UnreadableRules_AreDropped_ButEverythingElseIsKept()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "ForwardSpeed": 4, "BackwardSpeed": 0.5, "AutoMode": true, "Rules": { "Apps": [ { "FileName": "a.exe", "Action": "Sideways" } ] } }""");

        Assert.Equal(new HourglassSettings(4, 0.5, AutoMode: true), new JsonSettingsStore(SettingsPath).Load());
    }

    [Fact]
    public void Load_Version100File_HasAutoModeOffAndNoRules()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "ForwardSpeed": 2, "BackwardSpeed": 1, "Timer": null }""");

        HourglassSettings loaded = new JsonSettingsStore(SettingsPath).Load();

        Assert.False(loaded.AutoMode);
        Assert.Equal(AutoRules.Empty, loaded.Rules);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsRuleSpeeds()
    {
        var store = new JsonSettingsStore(SettingsPath);
        var rules = new AutoRules(
            [new AppRule("Code.exe", RuleAction.Forward, 2.5), new AppRule("notes.exe", RuleAction.Forward)],
            [new SiteRule("youtube.com", RuleAction.Backward, 12.75)]);

        store.Save(new HourglassSettings(AutoMode: true, Rules: rules));

        Assert.Equal(rules, new JsonSettingsStore(SettingsPath).Load().Rules);
    }

    [Fact]
    public void Load_Version110Rules_WithoutSpeeds_UseTheMainSpeed()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, """{ "AutoMode": true, "Rules": { "Apps": [ { "FileName": "Code.exe", "Action": "Forward" } ], "Sites": [ { "Domain": "youtube.com", "Action": "Backward" } ] } }""");

        AutoRules rules = new JsonSettingsStore(SettingsPath).Load().Rules;

        Assert.Null(rules.Apps[0].Speed);
        Assert.Null(rules.Sites[0].Speed);
    }

    [Fact]
    public void DefaultPath_IsUnderAppDataFolder()
    {
        Assert.EndsWith(Path.Combine("ForwardBackHourglass", "settings.json"), JsonSettingsStore.DefaultPath);
    }
}
