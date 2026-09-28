using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class RulesViewModelTests
{
    private readonly FakeSettingsStore _settings = new();

    private RulesViewModel CreateRules() => new MainViewModel(new FakeTicker(), new FakeAlarm(), _settings).Rules;

    [Fact]
    public void AddApp_NormalizesAndSaves()
    {
        var rules = CreateRules();

        Assert.True(rules.AddApp(@"C:\Program Files\Microsoft VS Code\Code.exe", RuleAction.Forward));

        Assert.Equal(["Code.exe"], rules.Apps.Select(a => a.Name));
        Assert.Equal([new AppRule("Code.exe", RuleAction.Forward)], _settings.Current.Rules!.Apps);
    }

    [Fact]
    public void AddApp_Twice_UpdatesInsteadOfDuplicating()
    {
        var rules = CreateRules();
        rules.AddApp("Code.exe", RuleAction.Forward);

        rules.AddApp("code", RuleAction.Backward);

        Assert.Equal([("Code.exe", RuleAction.Backward)], rules.Apps.Select(a => (a.Name, a.Action)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a domain")]
    [InlineData("brave://settings")]
    public void AddSite_RejectsNonDomains(string input)
    {
        var rules = CreateRules();

        Assert.False(rules.AddSite(input, RuleAction.Forward));
        Assert.Empty(rules.Sites);
    }

    [Fact]
    public void AddSite_AcceptsAPastedUrl()
    {
        var rules = CreateRules();

        Assert.True(rules.AddSite("https://www.youtube.com/watch?v=1", RuleAction.Backward));

        Assert.Equal(["youtube.com"], rules.Sites.Select(s => s.Name));
    }

    [Fact]
    public void Remove_DeletesAndSaves()
    {
        var rules = CreateRules();
        rules.AddApp("Code.exe", RuleAction.Forward);
        rules.AddSite("youtube.com", RuleAction.Backward);

        rules.Remove(rules.Apps[0]);
        rules.Remove(rules.Sites[0]);

        Assert.Equal(AutoRules.Empty, _settings.Current.Rules);
    }

    [Fact]
    public void ChangingAnAction_Saves()
    {
        var rules = CreateRules();
        rules.AddSite("youtube.com", RuleAction.Backward);

        rules.Sites[0].Action = RuleAction.Pause;

        Assert.Equal([new SiteRule("youtube.com", RuleAction.Pause)], _settings.Current.Rules!.Sites);
    }

    [Fact]
    public void ActionChoices_AreForwardPauseBackward()
    {
        Assert.Equal([RuleAction.Forward, RuleAction.Pause, RuleAction.Backward], RulesViewModel.ActionChoices);
    }
}
