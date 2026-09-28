using Hourglass.Core;

namespace Hourglass.Core.Tests;

/// <summary>Auto mode as the main window sees it: apps and websites driving the timer.</summary>
public class AutoModeViewModelTests
{
    private static readonly AutoRules Rules = new(
        [new AppRule("Code.exe", RuleAction.Forward), new AppRule("game.exe", RuleAction.Backward)],
        [new SiteRule("youtube.com", RuleAction.Backward), new SiteRule("news.com", RuleAction.Pause)]);

    private readonly FakeTicker _ticker = new();
    private readonly FakeAlarm _alarm = new();
    private readonly FakeForegroundWatcher _watcher = new();
    private readonly FakeSettingsStore _settings = new(new HourglassSettings(1, 1, AutoMode: true, Rules: Rules));

    private MainViewModel CreateActive()
    {
        var vm = new MainViewModel(_ticker, _alarm, _settings, _watcher);
        vm.Activate();
        return vm;
    }

    [Fact]
    public void Activate_StartsWatching_AndWaitsForATrackedApp()
    {
        var vm = CreateActive();

        Assert.True(_watcher.IsRunning);
        Assert.False(_ticker.IsRunning);
        Assert.Equal("Auto · paused until a tracked app or site is in front", vm.AutoStatusText);
    }

    [Fact]
    public void WatcherOnlyRunsInAutoMode()
    {
        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(new HourglassSettings(Rules: Rules)), _watcher);
        vm.Activate();
        Assert.False(_watcher.IsRunning);

        vm.IsAutoMode = true;
        Assert.True(_watcher.IsRunning);

        vm.IsAutoMode = false;
        Assert.False(_watcher.IsRunning);
        Assert.Equal("", vm.AutoStatusText);
    }

    [Fact]
    public void ForwardApp_CountsForward()
    {
        var vm = CreateActive();

        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);

        Assert.Equal("00:00:10.0", vm.DisplayTime);
        Assert.Equal("Auto · Code.exe: counting forward", vm.AutoStatusText);
    }

    [Fact]
    public void SwitchingApps_FlipsAndPauses()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);

        _watcher.ShowApp("game.exe");
        _ticker.Elapse(4);
        Assert.Equal("00:00:06.0", vm.DisplayTime);
        Assert.Equal(TimerState.Backward, vm.State);

        _watcher.ShowApp("notepad.exe");
        _ticker.Elapse(4);
        Assert.Equal("00:00:06.0", vm.DisplayTime);
        Assert.Equal(TimerState.Paused, vm.State);
        Assert.Equal("Auto · paused (notepad.exe isn't tracked)", vm.AutoStatusText);
    }

    [Fact]
    public void ReturningToACountdown_ResumesWithoutRefillingTheGlass()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);
        _watcher.ShowApp("game.exe");
        _ticker.Elapse(5);
        _watcher.Show(null);

        _watcher.ShowApp("game.exe");

        Assert.Equal(0.5, vm.UpperSand, precision: 6);
    }

    [Fact]
    public void CountdownReachingZero_RingsAndAsksForAttention_ThenStaysAtZero()
    {
        var vm = CreateActive();
        int attention = 0;
        vm.AttentionRequested += (_, _) => attention++;
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(2);
        _watcher.ShowApp("game.exe");

        _ticker.Elapse(3);

        Assert.True(_alarm.IsPlaying);
        Assert.Equal(1, attention);
        Assert.Equal("Auto · game.exe: nothing left to count down", vm.AutoStatusText);

        _watcher.ShowApp("game.exe", handle: 2);
        Assert.False(_ticker.IsRunning);
        Assert.Equal("00:00:00.0", vm.DisplayTime);
    }

    [Fact]
    public void SwitchingToAForwardApp_StopsTheRing()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(1);
        _watcher.ShowApp("game.exe");
        _ticker.Elapse(1);
        Assert.True(_alarm.IsPlaying);

        _watcher.ShowApp("code.exe");

        Assert.False(_alarm.IsPlaying);
        Assert.Equal(TimerState.Forward, vm.State);
    }

    [Fact]
    public void ManualMode_RingDoesNotAskForAttention()
    {
        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(), _watcher);
        vm.Activate();
        int attention = 0;
        vm.AttentionRequested += (_, _) => attention++;
        vm.StartCommand.Execute(null);
        _ticker.Elapse(1);
        vm.BackwardCommand.Execute(null);
        _ticker.Elapse(1);

        Assert.True(_alarm.IsPlaying);
        Assert.Equal(0, attention);
    }

    [Fact]
    public void AutoMode_DisablesDirectionButtons_ButNotReset()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(3);

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.False(vm.BackwardCommand.CanExecute(null));
        Assert.False(vm.PauseCommand.CanExecute(null));
        Assert.True(vm.ResetCommand.CanExecute(null));

        vm.ResetCommand.Execute(null);
        Assert.Equal("00:00:00.0", vm.DisplayTime);
    }

    [Fact]
    public void TurningAutoOff_LeavesTimerPaused_AndReenablesButtons()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(3);
        _watcher.Show(null);
        int raised = 0;
        vm.StartCommand.CanExecuteChanged += (_, _) => raised++;

        vm.IsAutoMode = false;

        Assert.Equal(TimerState.Paused, vm.State);
        Assert.True(vm.StartCommand.CanExecute(null));
        Assert.Equal(1, raised);
        _watcher.ShowApp("code.exe");
        Assert.Equal(TimerState.Paused, vm.State); // no longer following apps
    }

    [Fact]
    public void TogglingBeforeActivate_UpdatesButtonsOnly()
    {
        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(), _watcher);

        vm.IsAutoMode = true;

        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.False(_watcher.IsRunning);
    }

    [Fact]
    public void KnownWebsite_FollowsItsRule()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);

        _watcher.ShowBrowser("https://www.youtube.com/watch?v=1");
        _ticker.Elapse(1);

        Assert.Equal("00:00:09.0", vm.DisplayTime);
        Assert.Equal("Auto · youtube.com: counting backward", vm.AutoStatusText);

        _watcher.ShowBrowser("news.com/today");
        Assert.Equal(TimerState.Paused, vm.State);
    }

    [Fact]
    public void UnknownWebsite_CountsBackward_AndAsksToClassifyOnce()
    {
        var vm = CreateActive();
        var asked = new List<string>();
        vm.ClassifyRequested += (_, domain) => asked.Add(domain);
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);

        _watcher.ShowBrowser("www.reddit.com/r/x");
        _ticker.Elapse(2);
        _watcher.ShowBrowser("old.reddit.com");

        Assert.Equal("00:00:08.0", vm.DisplayTime);
        Assert.Equal(["reddit.com"], asked);
        Assert.Equal(["reddit.com"], vm.Rules.Unclassified);
        Assert.Equal("Auto · reddit.com (not classified yet): counting backward", vm.AutoStatusText);
    }

    [Fact]
    public void ClassifyingTheCurrentSite_TakesEffectImmediately_AndIsSaved()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(10);
        _watcher.ShowBrowser("reddit.com");

        Assert.True(vm.Rules.AddSite("reddit.com", RuleAction.Pause));

        Assert.Equal(TimerState.Paused, vm.State);
        Assert.Empty(vm.Rules.Unclassified);
        Assert.Contains(new SiteRule("reddit.com", RuleAction.Pause), _settings.Current.Rules!.Sites);
    }

    [Fact]
    public void EditingARule_ReappliesAtOnce()
    {
        var vm = CreateActive();
        _watcher.ShowApp("code.exe");
        _ticker.Elapse(5);

        vm.Rules.Apps.Single(a => a.Name == "Code.exe").Action = RuleAction.Pause;

        Assert.Equal(TimerState.Paused, vm.State);
        Assert.Contains(new AppRule("Code.exe", RuleAction.Pause), _settings.Current.Rules!.Apps);
    }

    [Fact]
    public void AutoModeAndRules_AreSavedAndRestored()
    {
        var store = new FakeSettingsStore();
        var vm = new MainViewModel(_ticker, _alarm, store, _watcher);
        vm.IsAutoMode = true;
        vm.Rules.AddApp(@"C:\Games\game.exe", RuleAction.Backward);
        vm.Rules.AddSite("https://www.github.com/x", RuleAction.Forward);

        var reopened = new MainViewModel(new FakeTicker(), new FakeAlarm(), store, new FakeForegroundWatcher());

        Assert.True(reopened.IsAutoMode);
        Assert.Equal(["game.exe"], reopened.Rules.Apps.Select(a => a.Name));
        Assert.Equal([("github.com", RuleAction.Forward)], reopened.Rules.Sites.Select(s => (s.Name, s.Action)));
    }

    [Fact]
    public void RestoredRunningTimer_InAutoMode_WaitsForATrackedApp()
    {
        var store = new FakeSettingsStore(new HourglassSettings(1, 1,
            new TimerSnapshot(TimeSpan.FromSeconds(30), TimerState.Forward, TimerDirection.Forward, TimeSpan.Zero),
            AutoMode: true, Rules: Rules));

        var vm = new MainViewModel(_ticker, _alarm, store, _watcher);
        vm.Activate();

        Assert.Equal(TimerState.Paused, vm.State);
        _watcher.ShowApp("code.exe");
        Assert.True(_ticker.IsRunning);
    }

    [Fact]
    public void Dispose_StopsTheWatcher_AndIgnoresLateEvents()
    {
        var vm = CreateActive();

        vm.Dispose();
        _watcher.ShowApp("code.exe");

        Assert.False(_watcher.IsRunning);
        Assert.Equal(TimerState.Idle, vm.State);
    }
}
