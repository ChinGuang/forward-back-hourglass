using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class RulesTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Microsoft VS Code\Code.exe", "Code.exe")]
    [InlineData("Code.exe", "Code.exe")]
    [InlineData("Code", "Code.exe")]
    [InlineData("\"C:\\Games\\game.EXE\"", "game.EXE")]
    [InlineData("/usr/bin/tool", "tool.exe")]
    public void NormalizeFileName(string input, string expected)
    {
        Assert.Equal(expected, AppRule.NormalizeFileName(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\folder\")]
    public void NormalizeFileName_RejectsEmpty(string? input)
    {
        Assert.Null(AppRule.NormalizeFileName(input));
    }

    [Fact]
    public void AppRule_MatchesIgnoringCaseAndExtension()
    {
        var rule = new AppRule("Code.exe", RuleAction.Forward);

        Assert.True(rule.Matches("code.exe"));
        Assert.True(rule.Matches("CODE"));
        Assert.False(rule.Matches("Code2.exe"));
    }

    [Fact]
    public void AppRule_NormalizesItsNameWhenCreated()
    {
        Assert.Equal("Code.exe", new AppRule(@"C:\Apps\Code", RuleAction.Forward).FileName);
        Assert.Equal(new AppRule("code.exe", RuleAction.Forward), new AppRule("code", RuleAction.Forward));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AppRule_BlankNamesNeverMatch(string blank)
    {
        var rule = new AppRule(blank, RuleAction.Forward);

        Assert.False(rule.Matches(""));
        Assert.False(rule.Matches(blank));
    }

    [Fact]
    public void FindSite_MostSpecificRuleWins()
    {
        var rules = new AutoRules([], [
            new SiteRule("youtube.com", RuleAction.Backward),
            new SiteRule("music.youtube.com", RuleAction.Forward),
        ]);

        Assert.Equal(RuleAction.Forward, rules.FindSite("music.youtube.com")?.Action);
        Assert.Equal(RuleAction.Backward, rules.FindSite("www.youtube.com")?.Action);
        Assert.Null(rules.FindSite("vimeo.com"));
    }

    [Fact]
    public void Sanitized_NormalizesDropsInvalidAndKeepsLastDuplicate()
    {
        var messy = new AutoRules(
            [
                new AppRule(@"C:\x\Code.exe", RuleAction.Forward),
                new AppRule("", RuleAction.Forward),
                new AppRule("game", (RuleAction)42),
                new AppRule("code.EXE", RuleAction.Backward),
            ],
            [
                new SiteRule("https://www.YouTube.com/x", RuleAction.Backward),
                new SiteRule("not a domain", RuleAction.Forward),
                new SiteRule("youtube.com", RuleAction.Pause),
            ]);

        AutoRules clean = messy.Sanitized();

        Assert.Equal([new AppRule("code.EXE", RuleAction.Backward)], clean.Apps);
        Assert.Equal([new SiteRule("youtube.com", RuleAction.Pause)], clean.Sites);
    }

    [Fact]
    public void Sanitized_ToleratesMissingLists()
    {
        var fromJson = new AutoRules(null!, null!);

        Assert.Equal(AutoRules.Empty, fromJson.Sanitized());
    }

    [Fact]
    public void AutoRules_CompareByContent()
    {
        Assert.Equal(
            new AutoRules([new AppRule("a.exe", RuleAction.Forward)], []),
            new AutoRules(new List<AppRule> { new("a.exe", RuleAction.Forward) }, new List<SiteRule>()));
        Assert.NotEqual(
            new AutoRules([new AppRule("a.exe", RuleAction.Forward)], []),
            new AutoRules([new AppRule("a.exe", RuleAction.Backward)], []));
    }

    [Theory]
    [InlineData("brave.exe", "Brave")]
    [InlineData("OPERA.EXE", "Opera")]
    [InlineData(@"C:\Users\me\AppData\Local\Vivaldi\Application\vivaldi.exe", "Vivaldi")]
    [InlineData("chrome.exe", "Chrome")]
    [InlineData("msedge.exe", "Edge")]
    public void KnownBrowsers_AreRecognised(string fileName, string name)
    {
        Assert.Equal(name, KnownBrowsers.Find(fileName)?.DisplayName);
    }

    [Fact]
    public void KnownBrowsers_ChromeAndEdgeAreMarkedExperimental()
    {
        Assert.Equal(["Chrome", "Edge"], KnownBrowsers.All.Where(b => b.Experimental).Select(b => b.DisplayName));
        Assert.Null(KnownBrowsers.Find("firefox.exe"));
    }
}
