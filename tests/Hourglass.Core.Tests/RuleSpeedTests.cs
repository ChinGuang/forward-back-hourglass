using Hourglass.Core;

namespace Hourglass.Core.Tests;

/// <summary>v1.2.0: each app and website can have its own speed multiplier.</summary>
public class RuleSpeedTests
{
    private readonly FakeTicker _ticker = new();
    private readonly FakeAlarm _alarm = new();
    private readonly FakeForegroundWatcher _watcher = new();

    [Theory]
    [InlineData("4", 4.0)]
    [InlineData("1.5", 1.5)]
    [InlineData("0,25", 0.25)]
    [InlineData("2x", 2.0)]
    [InlineData(" 3 X ", 3.0)]
    [InlineData("8×", 8.0)]
    [InlineData("12.75", 12.75)]
    [InlineData("0.001", 0.001)]
    [InlineData("1000000", 1000000.0)]
    public void TryParseRuleSpeed_AcceptsPositiveNumbers(string text, double expected)
    {
        Assert.True(SpeedPresets.TryParseRuleSpeed(text, out double? speed));
        Assert.Equal(expected, speed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Default")]
    [InlineData("default")]
    public void TryParseRuleSpeed_EmptyOrDefault_MeansMainSpeed(string? text)
    {
        Assert.True(SpeedPresets.TryParseRuleSpeed(text, out double? speed));
        Assert.Null(speed);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("-2")]
    [InlineData("abc")]
    [InlineData("2 3")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e400")]
    public void TryParseRuleSpeed_RejectsZeroNegativeAndText(string text)
    {
        Assert.False(SpeedPresets.TryParseRuleSpeed(text, out _));
    }

    [Theory]
    [InlineData(1.5, "1.5×")]
    [InlineData(12.75, "12.75×")]
    [InlineData(0.25, "0.25×")]
    public void Label_ShowsTypedSpeeds(double speed, string expected)
    {
        Assert.Equal(expected, SpeedPresets.Label(speed));
        Assert.Equal("Default", SpeedPresets.Label((double?)null));
    }

    [Fact]
    public void Timer_SpeedOverride_AppliesToEitherDirection_AndNullFallsBack()
    {
        var timer = new HourglassTimer { ForwardSpeed = 2, BackwardSpeed = 4 };
        timer.Start();

        timer.SpeedOverride = 3;
        timer.Advance(TimeSpan.FromSeconds(10));        // 30 s
        timer.SpeedOverride = null;
        timer.Advance(TimeSpan.FromSeconds(1));         // +2 s
        timer.Backward();
        timer.SpeedOverride = 0.5;
        timer.Advance(TimeSpan.FromSeconds(4));         // -2 s

        Assert.Equal(TimeSpan.FromSeconds(30), timer.Value);
        Assert.Equal(0.5, timer.CurrentSpeed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Timer_InvalidOverride_Throws(double speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HourglassTimer().SpeedOverride = speed);
    }

    [Fact]
    public void Timer_HugeSpeedForward_SaturatesInsteadOfOverflowing()
    {
        var timer = new HourglassTimer { SpeedOverride = 1e300 };
        timer.Start();

        timer.Advance(TimeSpan.FromMilliseconds(33));

        Assert.Equal(TimeSpan.MaxValue, timer.Value);
    }

    [Fact]
    public void Timer_HugeSpeedBackward_DrainsAtOnceAndRings()
    {
        var timer = new HourglassTimer();
        timer.Start();
        timer.Advance(TimeSpan.FromHours(5));
        timer.Backward();
        timer.SpeedOverride = double.MaxValue;

        timer.Advance(TimeSpan.FromMilliseconds(33));

        Assert.Equal(TimeSpan.Zero, timer.Value);
        Assert.True(timer.IsRinging);
    }

    [Fact]
    public void Timer_TinySpeed_StillCounts()
    {
        var timer = new HourglassTimer { SpeedOverride = 0.001 };
        timer.Start();

        timer.Advance(TimeSpan.FromSeconds(1000));

        Assert.Equal(TimeSpan.FromSeconds(1), timer.Value);
    }

    [Fact]
    public void Engine_DecisionCarriesTheRulesSpeed_UnclassifiedUsesMain()
    {
        var rules = new AutoRules(
            [new AppRule("Code.exe", RuleAction.Forward, 2)],
            [new SiteRule("youtube.com", RuleAction.Backward, 4), new SiteRule("docs.com", RuleAction.Forward)]);
        var engine = new AutoModeEngine();

        Assert.Equal(2, engine.Decide(new ActiveWindow("code.exe", 1), rules).Speed);
        Assert.Equal(4, engine.Decide(new ActiveWindow("brave.exe", 2, "youtube.com"), rules).Speed);
        Assert.Null(engine.Decide(new ActiveWindow("brave.exe", 2, "docs.com"), rules).Speed);
        Assert.Null(engine.Decide(new ActiveWindow("brave.exe", 2, "reddit.com"), rules).Speed);
    }

    [Fact]
    public void AutoMode_UsesEachRulesOwnSpeed_AndDefaultUsesMain()
    {
        var rules = new AutoRules(
            [new AppRule("Code.exe", RuleAction.Forward, 2), new AppRule("notes.exe", RuleAction.Forward)],
            [new SiteRule("youtube.com", RuleAction.Backward, 4)]);
        var vm = CreateActive(new HourglassSettings(ForwardSpeed: 0.5, BackwardSpeed: 1, AutoMode: true, Rules: rules));

        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);                      // +20 s at Code.exe's 2×
        Assert.Equal("Auto · Code.exe: counting forward (2×)", vm.AutoStatusText);

        _watcher.ShowApp("notes.exe");
        _ticker.Elapse(10);                      // +5 s at the main forward 0.5×
        Assert.Equal("Auto · notes.exe: counting forward (0.5×)", vm.AutoStatusText);

        _watcher.ShowBrowser("youtube.com/watch?v=1");
        _ticker.Elapse(5);                       // -20 s at YouTube's 4×

        Assert.Equal("00:00:05.0", vm.DisplayTime);
        Assert.Equal("Auto · youtube.com: counting backward (4×)", vm.AutoStatusText);
    }

    [Fact]
    public void AutoMode_UnclassifiedSite_UsesMainBackwardSpeed()
    {
        var vm = CreateActive(new HourglassSettings(ForwardSpeed: 1, BackwardSpeed: 2, AutoMode: true,
            Rules: new AutoRules([new AppRule("Code.exe", RuleAction.Forward, 8)], [])));
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);                      // 80 s

        _watcher.ShowBrowser("reddit.com");
        _ticker.Elapse(10);                      // -20 s

        Assert.Equal("00:01:00.0", vm.DisplayTime);
    }

    [Fact]
    public void SwitchingRules_CountsTheTimeSinceTheLastTickAtTheOldSpeed()
    {
        var rules = new AutoRules(
            [new AppRule("Code.exe", RuleAction.Forward, 2), new AppRule("slow.exe", RuleAction.Forward, 0.25)], []);
        var vm = CreateActive(new HourglassSettings(AutoMode: true, Rules: rules));
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);                      // 20 s
        _ticker.Pending = TimeSpan.FromSeconds(1);  // 1 more real second spent in Code.exe → 2 s

        _watcher.ShowApp("slow.exe");

        Assert.Equal("00:00:22.0", vm.DisplayTime);
    }

    [Fact]
    public void TurningAutoOff_GoesBackToTheMainSpeeds()
    {
        var rules = new AutoRules([new AppRule("Code.exe", RuleAction.Forward, 8)], []);
        var vm = CreateActive(new HourglassSettings(AutoMode: true, Rules: rules));
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(1);                       // 8 s

        vm.IsAutoMode = false;
        vm.StartCommand.Execute(null);
        _ticker.Elapse(1);                       // +1 s at main 1×

        Assert.Equal("00:00:09.0", vm.DisplayTime);
    }

    [Fact]
    public void PausedRule_ClearsTheOverride()
    {
        var rules = new AutoRules([new AppRule("Code.exe", RuleAction.Forward, 8), new AppRule("mail.exe", RuleAction.Pause, 3)], []);
        var vm = CreateActive(new HourglassSettings(AutoMode: true, Rules: rules));
        _watcher.ShowApp("code.exe");

        _watcher.ShowApp("mail.exe");

        Assert.Equal(TimerState.Paused, vm.State);
        Assert.Equal("Auto · mail.exe: paused", vm.AutoStatusText);
    }

    [Fact]
    public void EditingARulesSpeed_TakesEffectImmediately_AndIsSaved()
    {
        var store = new FakeSettingsStore(new HourglassSettings(AutoMode: true,
            Rules: new AutoRules([new AppRule("Code.exe", RuleAction.Forward)], [])));
        var vm = new MainViewModel(_ticker, _alarm, store, _watcher);
        vm.Activate();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(1);

        vm.Rules.Apps[0].SpeedText = "4";
        _ticker.Elapse(1);

        Assert.Equal("00:00:05.0", vm.DisplayTime);
        Assert.Equal([new AppRule("Code.exe", RuleAction.Forward, 4)], store.Current.Rules.Apps);
    }

    [Fact]
    public void RuleItem_InvalidSpeedText_FlagsErrorAndKeepsTheOldSpeed()
    {
        var store = new FakeSettingsStore();
        var rules = new MainViewModel(_ticker, _alarm, store).Rules;
        rules.AddSite("youtube.com", RuleAction.Backward, 4);
        RuleItem site = rules.Sites[0];
        int saves = store.SaveCount;

        site.SpeedText = "0";
        Assert.True(site.HasSpeedError);
        site.SpeedText = "-3";
        site.SpeedText = "fast";

        Assert.Equal(4, site.Speed);
        Assert.Equal(saves, store.SaveCount);

        site.SpeedText = "Default";
        Assert.False(site.HasSpeedError);
        Assert.Null(site.Speed);
        Assert.Equal([new SiteRule("youtube.com", RuleAction.Backward)], store.Current.Rules.Sites);
    }

    [Fact]
    public void RuleItem_ShowsItsSpeed_AndDisablesSpeedForPause()
    {
        var rules = new MainViewModel(_ticker, _alarm, new FakeSettingsStore()).Rules;
        rules.AddApp("Code.exe", RuleAction.Forward, 1.5);
        RuleItem app = rules.Apps[0];

        Assert.Equal("1.5×", app.SpeedText);
        Assert.True(app.IsSpeedEnabled);

        app.Action = RuleAction.Pause;
        Assert.False(app.IsSpeedEnabled);
    }

    [Fact]
    public void AddSite_WithoutSpeed_KeepsAnExistingRulesSpeed_WithSpeed_ReplacesIt()
    {
        var rules = new MainViewModel(_ticker, _alarm, new FakeSettingsStore()).Rules;
        rules.AddSite("youtube.com", RuleAction.Backward, 4);

        rules.AddSite("youtube.com", RuleAction.Forward);
        Assert.Equal((RuleAction.Forward, (double?)4), (rules.Sites[0].Action, rules.Sites[0].Speed));

        rules.AddSite("youtube.com", RuleAction.Backward, null);
        Assert.Null(rules.Sites[0].Speed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void AddWithInvalidSpeed_IsRefused(double speed)
    {
        var rules = new MainViewModel(_ticker, _alarm, new FakeSettingsStore()).Rules;

        Assert.False(rules.AddApp("Code.exe", RuleAction.Forward, speed));
        Assert.False(rules.AddSite("youtube.com", RuleAction.Backward, speed));
    }

    [Fact]
    public void SpeedChoices_AreDefaultPlusPresets()
    {
        Assert.Equal(["Default", "0.25×", "0.5×", "1×", "2×", "4×", "8×"], RuleItem.SpeedChoices);
    }

    [Fact]
    public void Sanitized_DropsInvalidSavedSpeeds()
    {
        var saved = new AutoRules(
            [new AppRule("a.exe", RuleAction.Forward, 0), new AppRule("b.exe", RuleAction.Forward, -2), new AppRule("c.exe", RuleAction.Forward, 1.5)],
            [new SiteRule("x.com", RuleAction.Backward, double.PositiveInfinity)]);

        AutoRules clean = saved.Sanitized();

        Assert.Equal([null, null, 1.5], clean.Apps.Select(a => a.Speed));
        Assert.Null(clean.Sites[0].Speed);
    }

    private MainViewModel CreateActive(HourglassSettings settings)
    {
        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(settings), _watcher);
        vm.Activate();
        return vm;
    }
}
