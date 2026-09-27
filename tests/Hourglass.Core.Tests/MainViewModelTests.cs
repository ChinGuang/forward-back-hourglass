using System.ComponentModel;
using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class MainViewModelTests
{
    private readonly FakeTicker _ticker = new();
    private readonly FakeAlarm _alarm = new();
    private readonly FakeSettingsStore _settings = new();

    private MainViewModel CreateViewModel() => new(_ticker, _alarm, _settings);

    [Fact]
    public void Initially_ReadyAtZero_WithOnlyStartEnabled()
    {
        var vm = CreateViewModel();

        Assert.Equal("00:00:00.0", vm.DisplayTime);
        Assert.Equal("Ready", vm.StatusText);
        Assert.True(vm.StartCommand.CanExecute(null));
        Assert.False(vm.BackwardCommand.CanExecute(null));
        Assert.False(vm.PauseCommand.CanExecute(null));
        Assert.False(vm.ResetCommand.CanExecute(null));
        Assert.False(vm.StopRingCommand.CanExecute(null));
        Assert.False(_ticker.IsRunning);
    }

    [Fact]
    public void Start_RunsTickerAndCountsForward()
    {
        var vm = CreateViewModel();

        vm.StartCommand.Execute(null);
        _ticker.Elapse(1.5);

        Assert.True(_ticker.IsRunning);
        Assert.Equal("00:00:01.5", vm.DisplayTime);
        Assert.Equal("Counting forward", vm.StatusText);
    }

    [Fact]
    public void Backward_DrainsToZero_StopsTickerAndLoopsAlarm()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(2);

        vm.BackwardCommand.Execute(null);
        _ticker.Elapse(1);
        Assert.Equal("00:00:01.0", vm.DisplayTime);
        Assert.Equal("Counting backward", vm.StatusText);

        _ticker.Elapse(1.2);

        Assert.Equal("00:00:00.0", vm.DisplayTime);
        Assert.False(_ticker.IsRunning);
        Assert.True(_alarm.IsPlaying);
        Assert.Equal(1, _alarm.PlayCount);
        Assert.True(vm.IsRinging);
        Assert.Equal("Time's up!", vm.StatusText);
        Assert.True(vm.StopRingCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("start")]
    [InlineData("reset")]
    public void Ring_IsDismissedBy(string action)
    {
        var vm = RingingViewModel();

        switch (action)
        {
            case "stop": vm.StopRingCommand.Execute(null); break;
            case "start": vm.StartCommand.Execute(null); break;
            case "reset": vm.ResetCommand.Execute(null); break;
        }

        Assert.False(_alarm.IsPlaying);
        Assert.False(vm.IsRinging);
    }

    [Fact]
    public void Pause_StopsTickerAndFreezesDisplay()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(3);

        vm.PauseCommand.Execute(null);
        _ticker.Elapse(3);

        Assert.False(_ticker.IsRunning);
        Assert.Equal("00:00:03.0", vm.DisplayTime);
        Assert.Equal("Paused", vm.StatusText);
    }

    [Fact]
    public void Reset_ReturnsToReady()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(3);

        vm.ResetCommand.Execute(null);

        Assert.Equal("00:00:00.0", vm.DisplayTime);
        Assert.Equal("Ready", vm.StatusText);
        Assert.False(_ticker.IsRunning);
    }

    [Fact]
    public void SpeedsApplyPerDirection()
    {
        var vm = CreateViewModel();
        vm.ForwardSpeed = 4;
        vm.BackwardSpeed = 0.5;

        vm.StartCommand.Execute(null);
        _ticker.Elapse(10); // 40 s
        vm.BackwardCommand.Execute(null);
        _ticker.Elapse(10); // -5 s

        Assert.Equal("00:00:35.0", vm.DisplayTime);
    }

    [Fact]
    public void ChangingSpeed_SavesSettings()
    {
        var vm = CreateViewModel();

        vm.ForwardSpeed = 2;
        vm.BackwardSpeed = 8;

        Assert.Equal(new HourglassSettings(2, 8), _settings.Current);
    }

    [Fact]
    public void SettingSameSpeed_DoesNotSave()
    {
        var vm = CreateViewModel();

        vm.ForwardSpeed = 1;

        Assert.Equal(0, _settings.SaveCount);
    }

    [Fact]
    public void SavedSpeeds_AreRestoredOnStartup()
    {
        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(new HourglassSettings(0.25, 4)));

        Assert.Equal(0.25, vm.ForwardSpeed);
        Assert.Equal(4, vm.BackwardSpeed);
    }

    [Fact]
    public void InvalidSavedSpeeds_AreSnappedToPresets()
    {
        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(new HourglassSettings(1000, -3)));

        Assert.Equal(8, vm.ForwardSpeed);
        Assert.Equal(1, vm.BackwardSpeed);
    }

    [Fact]
    public void Countdown_ShowsZeroOnlyWhenItRings()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(1);
        vm.BackwardCommand.Execute(null);

        _ticker.Elapse(0.95);

        Assert.Equal("00:00:00.1", vm.DisplayTime);
        Assert.False(vm.IsRinging);

        _ticker.Elapse(0.05);

        Assert.Equal("00:00:00.0", vm.DisplayTime);
        Assert.True(vm.IsRinging);
    }

    [Fact]
    public void Tick_OnlyRaisesPropertiesThatChanged()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(1);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _ticker.Elapse(1);

        Assert.DoesNotContain(nameof(MainViewModel.State), changed);
        Assert.DoesNotContain(nameof(MainViewModel.StatusText), changed);
        Assert.DoesNotContain(nameof(MainViewModel.Direction), changed);
        Assert.Contains(nameof(MainViewModel.DisplayTime), changed);
    }

    [Fact]
    public void DirectionChange_IsRaisedBeforeSandLevels()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(10);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.BackwardCommand.Execute(null);

        Assert.True(changed.IndexOf(nameof(MainViewModel.Direction)) < changed.IndexOf(nameof(MainViewModel.UpperSand)));
    }

    [Fact]
    public void StopRing_LeavesResetEnabled()
    {
        var vm = RingingViewModel();

        vm.StopRingCommand.Execute(null);

        Assert.True(vm.ResetCommand.CanExecute(null));
    }

    [Fact]
    public void SpeedOptions_ListAllPresetsWithLabels()
    {
        var vm = CreateViewModel();

        Assert.Equal(["0.25×", "0.5×", "1×", "2×", "4×", "8×"], vm.SpeedOptions.Select(o => o.Label));
    }

    [Fact]
    public void BackwardCommand_BecomesEnabledOnceTimeAccumulates()
    {
        var vm = CreateViewModel();
        int raised = 0;
        vm.BackwardCommand.CanExecuteChanged += (_, _) => raised++;

        vm.StartCommand.Execute(null);
        Assert.False(vm.BackwardCommand.CanExecute(null));

        _ticker.Elapse(0.1);

        Assert.True(vm.BackwardCommand.CanExecute(null));
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Tick_RaisesDisplayTimeChanged()
    {
        var vm = CreateViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.StartCommand.Execute(null);
        changed.Clear();

        _ticker.Elapse(1);

        Assert.Contains(nameof(MainViewModel.DisplayTime), changed);
        Assert.Contains(nameof(MainViewModel.LowerSand), changed);
    }

    [Fact]
    public void SandLevels_TrackBackwardDrain()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(10);
        vm.BackwardCommand.Execute(null);
        _ticker.Elapse(5);

        Assert.Equal(TimerDirection.Backward, vm.Direction);
        Assert.Equal(0.5, vm.UpperSand, precision: 6);
        Assert.Equal(0.5, vm.LowerSand, precision: 6);
        Assert.True(vm.IsSandFlowing);
    }

    [Fact]
    public void Dispose_StopsTickerAndAlarm()
    {
        var vm = RingingViewModel();
        vm.StartCommand.Execute(null);

        vm.Dispose();

        Assert.False(_ticker.IsRunning);
        Assert.False(_alarm.IsPlaying);
    }

    [Fact]
    public void SaveState_StoresSpeedsAndTimer()
    {
        var vm = CreateViewModel();
        vm.ForwardSpeed = 2;
        vm.StartCommand.Execute(null);
        _ticker.Elapse(5); // 10 s at 2x

        vm.SaveState();

        Assert.Equal(2, _settings.Current.ForwardSpeed);
        Assert.Equal(
            new TimerSnapshot(TimeSpan.FromSeconds(10), TimerState.Forward, TimerDirection.Forward, TimeSpan.Zero),
            _settings.Current.Timer);
    }

    [Fact]
    public void ChangingSpeed_SavesOnlySpeeds_SoACrashDoesNotRestoreAMidRunTimer()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(3);

        vm.BackwardSpeed = 4;

        Assert.Equal(new HourglassSettings(1, 4), _settings.Current);
    }

    [Fact]
    public void SaveState_CountsTimeSinceTheLastTick()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(3);
        _ticker.Pending = TimeSpan.FromMilliseconds(500);

        vm.SaveState();

        Assert.Equal(TimeSpan.FromSeconds(3.5), _settings.Current.Timer?.Value);
    }

    [Fact]
    public void SaveState_WhenStopped_DoesNotFlushTheTicker()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(3);
        vm.PauseCommand.Execute(null);
        _ticker.Pending = TimeSpan.FromSeconds(10);

        vm.SaveState();

        Assert.Equal(TimeSpan.FromSeconds(3), _settings.Current.Timer?.Value);
    }

    [Fact]
    public void Reopening_ConsumesTheSavedTimer_ButKeepsSpeeds()
    {
        var store = new FakeSettingsStore(new HourglassSettings(2, 4,
            new TimerSnapshot(TimeSpan.FromSeconds(42), TimerState.Paused, TimerDirection.Forward, TimeSpan.Zero)));

        _ = new MainViewModel(_ticker, _alarm, store);

        // If the app now crashed, the next launch would start fresh rather than bring this timer back again.
        Assert.Equal(new HourglassSettings(2, 4), store.Current);
    }

    [Fact]
    public void Reopening_RunningTimer_ResumesTickingFromSavedValue()
    {
        var saved = new HourglassSettings(1, 1,
            new TimerSnapshot(TimeSpan.FromSeconds(42), TimerState.Forward, TimerDirection.Forward, TimeSpan.Zero));

        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(saved));
        Assert.False(_ticker.IsRunning); // window start-up time must not count

        vm.Activate();

        Assert.True(_ticker.IsRunning);
        Assert.Equal("00:00:42.0", vm.DisplayTime);
        Assert.Equal("Counting forward", vm.StatusText);
        Assert.False(vm.StartCommand.CanExecute(null));
        Assert.True(vm.PauseCommand.CanExecute(null));

        _ticker.Elapse(1);
        Assert.Equal("00:00:43.0", vm.DisplayTime);
    }

    [Fact]
    public void Reopening_RunningCountdown_ResumesAndRingsAtZero()
    {
        var saved = new HourglassSettings(1, 2,
            new TimerSnapshot(TimeSpan.FromSeconds(4), TimerState.Backward, TimerDirection.Backward, TimeSpan.FromSeconds(8)));

        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(saved));
        vm.Activate();

        Assert.Equal(0.5, vm.UpperSand, precision: 6);
        _ticker.Elapse(2); // 4 s at 2x

        Assert.True(vm.IsRinging);
        Assert.True(_alarm.IsPlaying);
    }

    [Fact]
    public void Reopening_PausedTimer_StaysPaused()
    {
        var saved = new HourglassSettings(1, 1,
            new TimerSnapshot(TimeSpan.FromSeconds(7), TimerState.Paused, TimerDirection.Forward, TimeSpan.Zero));

        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(saved));
        vm.Activate();

        Assert.False(_ticker.IsRunning);
        Assert.Equal("Paused", vm.StatusText);
        Assert.Equal("00:00:07.0", vm.DisplayTime);

        vm.StartCommand.Execute(null);
        _ticker.Elapse(1);
        Assert.Equal("00:00:08.0", vm.DisplayTime);
    }

    [Fact]
    public void Reopening_AfterCountdownFinished_IsSilentAndResettable()
    {
        var saved = new HourglassSettings(1, 1,
            new TimerSnapshot(TimeSpan.Zero, TimerState.Idle, TimerDirection.Backward, TimeSpan.FromSeconds(8)));

        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(saved));
        vm.Activate();

        Assert.False(_alarm.IsPlaying);
        Assert.False(vm.IsRinging);
        Assert.Equal("Ready", vm.StatusText);
        Assert.Equal(1, vm.LowerSand, precision: 6);
        Assert.True(vm.ResetCommand.CanExecute(null));
    }

    [Fact]
    public void Reopening_WithInvalidSavedTimer_StartsFresh_ButKeepsSpeeds()
    {
        var saved = new HourglassSettings(4, 0.5,
            new TimerSnapshot(TimeSpan.FromSeconds(-5), TimerState.Forward, TimerDirection.Forward, TimeSpan.Zero));

        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(saved));
        vm.Activate();

        Assert.Equal("00:00:00.0", vm.DisplayTime);
        Assert.Equal("Ready", vm.StatusText);
        Assert.False(_ticker.IsRunning);
        Assert.Equal(4, vm.ForwardSpeed);
        Assert.Equal(0.5, vm.BackwardSpeed);
    }

    [Fact]
    public void Reopening_WithNoSavedTimer_StartsFresh()
    {
        var vm = new MainViewModel(_ticker, _alarm, new FakeSettingsStore(new HourglassSettings(2, 2)));

        Assert.Equal("00:00:00.0", vm.DisplayTime);
        Assert.Equal("Ready", vm.StatusText);
        Assert.False(_ticker.IsRunning);
    }

    private MainViewModel RingingViewModel()
    {
        var vm = CreateViewModel();
        vm.StartCommand.Execute(null);
        _ticker.Elapse(1);
        vm.BackwardCommand.Execute(null);
        _ticker.Elapse(1);
        Assert.True(_alarm.IsPlaying);
        return vm;
    }
}
