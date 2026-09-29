using Hourglass.Core;

namespace Hourglass.Core.Tests;

/// <summary>v1.2.1: the "Add running app" list hides apps already added and can be searched.</summary>
public class RunningAppFilterTests
{
    private static readonly App[] Running =
    [
        new("Code.exe", "forward-back-hourglass - Visual Studio Code"),
        new("slack.exe", "Slack – general"),
        new("game.exe", "Space Game"),
        new("notepad.exe", "a.txt - Notepad", "todo.txt - Notepad"),   // two windows
        new("brave.exe", "YouTube - Brave"),
    ];

    [Fact]
    public void Addable_HidesAlreadyAddedApps_IgnoringCaseAndExtension()
    {
        var rules = new AutoRules([new AppRule("code", RuleAction.Forward), new AppRule("GAME.EXE", RuleAction.Backward, 4)], []);

        var addable = RunningAppFilter.Addable(Running, rules);

        Assert.Equal(["slack.exe", "notepad.exe"], addable.Select(a => a.FileName));
    }

    [Fact]
    public void Addable_HidesBrowsers_EvenWithNoRules()
    {
        var addable = RunningAppFilter.Addable(Running, AutoRules.Empty);

        Assert.DoesNotContain(addable, a => a.FileName == "brave.exe");
        Assert.Equal(4, addable.Count);
    }

    [Fact]
    public void Addable_WebsiteRulesDontHideApps()
    {
        var rules = new AutoRules([], [new SiteRule("slack.com", RuleAction.Forward)]);

        Assert.Contains(RunningAppFilter.Addable(Running, rules), a => a.FileName == "slack.exe");
    }

    [Theory]
    [InlineData("code", new[] { "Code.exe" })]              // program name, any case
    [InlineData("SLACK", new[] { "slack.exe" })]
    [InlineData("general", new[] { "slack.exe" })]          // window title
    [InlineData("todo", new[] { "notepad.exe" })]           // the second window's title
    [InlineData(".exe", new[] { "Code.exe", "slack.exe", "game.exe", "notepad.exe" })]
    [InlineData("  game  ", new[] { "game.exe" })]          // surrounding spaces ignored
    [InlineData("zzz", new string[0])]
    public void Search_MatchesProgramNameOrWindowTitle(string query, string[] expected)
    {
        var addable = RunningAppFilter.Addable(Running, AutoRules.Empty);

        Assert.Equal(expected, RunningAppFilter.Search(addable, query).Select(a => a.FileName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Search_BlankShowsEverything(string? query)
    {
        var addable = RunningAppFilter.Addable(Running, AutoRules.Empty);

        Assert.Same(addable, RunningAppFilter.Search(addable, query));
    }

    [Fact]
    public void EmptyMessage_WhenEverythingOpenIsAlreadyAdded()
    {
        Assert.Equal(
            "All open apps are already added. Use \"Browse for .exe…\" to add another program.",
            RunningAppFilter.EmptyMessage(trackableCount: 2, addableCount: 0, matchCount: 0, query: "anything"));
    }

    [Fact]
    public void EmptyMessage_WhenNoAppsAreOpen_OrOnlyBrowsers()
    {
        var onlyBrowser = new[] { new App("brave.exe", "YouTube - Brave") };

        int trackable = RunningAppFilter.Trackable(onlyBrowser).Count;

        Assert.Equal(0, trackable);
        Assert.Equal(
            "No other open apps found. Use \"Browse for .exe…\" to add a program.",
            RunningAppFilter.EmptyMessage(trackable, addableCount: 0, matchCount: 0, query: ""));
    }

    [Fact]
    public void EmptyMessage_WhenTheSearchMatchesNothing()
    {
        Assert.Equal("No open app matches \"zzz\".", RunningAppFilter.EmptyMessage(trackableCount: 3, addableCount: 3, matchCount: 0, query: " zzz "));
    }

    [Fact]
    public void EmptyMessage_NoneWhenThereIsSomethingToPick()
    {
        Assert.Null(RunningAppFilter.EmptyMessage(trackableCount: 3, addableCount: 3, matchCount: 1, query: "code"));
    }

    private sealed record App(string FileName, params string[] WindowTitles) : IRunningApp
    {
        public IReadOnlyList<string> Titles => WindowTitles;
    }
}
