using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class AutoModeEngineTests
{
    private static readonly AutoRules Rules = new(
        [new AppRule("Code.exe", RuleAction.Forward), new AppRule("game.exe", RuleAction.Backward), new AppRule("mail.exe", RuleAction.Pause)],
        [new SiteRule("github.com", RuleAction.Forward), new SiteRule("youtube.com", RuleAction.Backward), new SiteRule("news.com", RuleAction.Pause)]);

    private readonly AutoModeEngine _engine = new();

    [Fact]
    public void NoWindow_Pauses()
    {
        Assert.Equal(new AutoDecision(RuleAction.Pause, AutoReason.NoWindow), _engine.Decide(null, Rules));
    }

    [Theory]
    [InlineData("code.exe", RuleAction.Forward)]
    [InlineData("GAME.EXE", RuleAction.Backward)]
    [InlineData("mail.exe", RuleAction.Pause)]
    public void AppRules_Apply(string app, RuleAction expected)
    {
        AutoDecision decision = _engine.Decide(new ActiveWindow(app, 1), Rules);

        Assert.Equal(expected, decision.Action);
        Assert.Equal(AutoReason.AppRule, decision.Reason);
    }

    [Fact]
    public void UntrackedApp_Pauses()
    {
        AutoDecision decision = _engine.Decide(new ActiveWindow("notepad.exe", 1), Rules);

        Assert.Equal(new AutoDecision(RuleAction.Pause, AutoReason.UntrackedApp, "notepad.exe"), decision);
    }

    [Theory]
    [InlineData("github.com/me/repo", RuleAction.Forward)]
    [InlineData("https://www.youtube.com/watch?v=1", RuleAction.Backward)]
    [InlineData("m.youtube.com", RuleAction.Backward)]
    [InlineData("news.com/today", RuleAction.Pause)]
    public void SiteRules_ApplyInSupportedBrowsers(string url, RuleAction expected)
    {
        foreach (BrowserProfile browser in KnownBrowsers.All)
        {
            AutoDecision decision = _engine.Decide(new ActiveWindow(browser.FileName, 5, url), Rules);

            Assert.Equal(expected, decision.Action);
            Assert.Equal(AutoReason.SiteRule, decision.Reason);
        }
    }

    [Fact]
    public void SiteRules_DoNotApplyToOtherApps()
    {
        AutoDecision decision = _engine.Decide(new ActiveWindow("firefox.exe", 5, "youtube.com"), Rules);

        Assert.Equal(AutoReason.UntrackedApp, decision.Reason);
    }

    [Fact]
    public void BrowserAppRule_IsIgnored_WebsitesDecide()
    {
        var rules = Rules with { Apps = [new AppRule("brave.exe", RuleAction.Forward)] };

        AutoDecision decision = _engine.Decide(new ActiveWindow("brave.exe", 5, "youtube.com"), rules);

        Assert.Equal(RuleAction.Backward, decision.Action);
    }

    [Fact]
    public void UnclassifiedSite_CountsBackward_AndAsksOnlyOnce()
    {
        AutoDecision first = _engine.Decide(new ActiveWindow("brave.exe", 5, "www.reddit.com/r/x"), Rules);
        AutoDecision again = _engine.Decide(new ActiveWindow("opera.exe", 6, "old.reddit.com"), Rules);

        Assert.Equal(new AutoDecision(RuleAction.Backward, AutoReason.UnclassifiedSite, "reddit.com", "reddit.com"), first);
        Assert.Equal(new AutoDecision(RuleAction.Backward, AutoReason.UnclassifiedSite, "reddit.com"), again);
        Assert.Equal(["reddit.com"], _engine.SeenUnclassified);
    }

    [Fact]
    public void MarkClassified_RemovesFromSeenList()
    {
        _engine.Decide(new ActiveWindow("brave.exe", 5, "reddit.com"), Rules);
        _engine.Decide(new ActiveWindow("brave.exe", 5, "twitch.tv"), Rules);

        _engine.MarkClassified("reddit.com");

        Assert.Equal(["twitch.tv"], _engine.SeenUnclassified);
    }

    [Theory]
    [InlineData("brave://newtab")]
    [InlineData("about:blank")]
    public void InternalPages_PauseWithoutAsking(string url)
    {
        AutoDecision decision = _engine.Decide(new ActiveWindow("brave.exe", 5, url), Rules);

        Assert.Equal(new AutoDecision(RuleAction.Pause, AutoReason.InternalPage, "Brave"), decision);
        Assert.Empty(_engine.SeenUnclassified);
    }

    [Fact]
    public void UnreadableUrl_KeepsThatWindowsLastSite()
    {
        _engine.Decide(new ActiveWindow("brave.exe", 5, "youtube.com/watch?v=1"), Rules);

        // The video goes full screen and the address bar disappears.
        AutoDecision decision = _engine.Decide(new ActiveWindow("brave.exe", 5, null), Rules);

        Assert.Equal(new AutoDecision(RuleAction.Backward, AutoReason.SiteRule, "youtube.com"), decision);
    }

    [Fact]
    public void UnreadableUrl_DoesNotBorrowAnotherWindowsSite()
    {
        _engine.Decide(new ActiveWindow("brave.exe", 5, "youtube.com"), Rules);

        AutoDecision decision = _engine.Decide(new ActiveWindow("brave.exe", 6, null), Rules);

        Assert.Equal(new AutoDecision(RuleAction.Pause, AutoReason.UnknownPage, "Brave"), decision);
    }

    [Fact]
    public void TypingInTheAddressBar_KeepsTheLastSite()
    {
        _engine.Decide(new ActiveWindow("brave.exe", 5, "github.com"), Rules);

        AutoDecision decision = _engine.Decide(new ActiveWindow("brave.exe", 5, "how to bake"), Rules);

        Assert.Equal(RuleAction.Forward, decision.Action);
    }
}
