using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hourglass.Core;

public sealed record SpeedOption(double Value, string Label);

/// <summary>
/// Everything the main window binds to. UI-free so it can be unit-tested with fake ticker/alarm/settings.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly HourglassTimer _timer = new();
    private readonly ITicker _ticker;
    private readonly IAlarmPlayer _alarm;
    private readonly ISettingsStore _settings;
    private readonly IForegroundWatcher? _watcher;
    private readonly AutoModeEngine _engine = new();
    private readonly RelayCommand[] _commands;
    private ActiveWindow? _activeWindow;
    private AutoDecision _lastDecision;
    private bool _autoMode;
    private bool _activated;
    private bool _tickerRunning;
    private bool _alarmPlaying;
    private bool[] _lastCanExecute = [];
    private Snapshot _shown;

    public MainViewModel(ITicker ticker, IAlarmPlayer alarm, ISettingsStore settings, IForegroundWatcher? watcher = null)
    {
        _ticker = ticker;
        _alarm = alarm;
        _settings = settings;
        _watcher = watcher;
        Rules = new RulesViewModel(OnRulesChanged, _engine.MarkClassified);

        HourglassSettings saved = settings.Load();
        // Snap to presets here so any store (or a hand-edited file) can't put an invalid speed in the timer.
        _timer.ForwardSpeed = SpeedPresets.Normalize(saved.ForwardSpeed);
        _timer.BackwardSpeed = SpeedPresets.Normalize(saved.BackwardSpeed);
        Rules.Load((saved.Rules ?? AutoRules.Empty).Sanitized());
        _autoMode = saved.AutoMode;
        if (saved.Timer is not null)
        {
            // An invalid saved timer is dropped (the timer starts fresh) without losing the speeds.
            _timer.Restore(saved.Timer);

            // The saved timer is consumed: until the app closes normally again the file holds no timer, so a
            // crash reopens a fresh timer instead of resurrecting this one.
            _settings.Save(CurrentSettings(includeTimer: false));
        }

        // In auto mode the apps in front drive the timer, so the manual direction buttons are off.
        StartCommand = new RelayCommand(() => Apply(() => _timer.Start()), () => !_autoMode && _timer.CanStart);
        BackwardCommand = new RelayCommand(() => Apply(() => _timer.Backward()), () => !_autoMode && _timer.CanGoBackward);
        PauseCommand = new RelayCommand(() => Apply(() => _timer.Pause()), () => !_autoMode && _timer.CanPause);
        ResetCommand = new RelayCommand(() => Apply(_timer.Reset), () => _timer.CanReset);
        StopRingCommand = new RelayCommand(() => Apply(_timer.StopRing), () => _timer.IsRinging);
        _commands = [StartCommand, BackwardCommand, PauseCommand, ResetCommand, StopRingCommand];

        _ticker.Tick += OnTick;
        if (_watcher is not null)
        {
            _watcher.Changed += OnActiveWindowChanged;
        }

        _lastCanExecute = CurrentCanExecute();
        _shown = TakeSnapshot();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>A countdown reached zero in auto mode: the window should come to the front.</summary>
    public event EventHandler? AttentionRequested;

    /// <summary>A website without a rule was seen for the first time this session: ask the user to classify it.</summary>
    public event EventHandler<string>? ClassifyRequested;

    public RulesViewModel Rules { get; }

    /// <summary>When on, the app or website in front drives the timer (see <see cref="AutoModeEngine"/>).</summary>
    public bool IsAutoMode
    {
        get => _autoMode;
        set
        {
            if (_autoMode == value)
            {
                return;
            }

            _autoMode = value;
            _settings.Save(CurrentSettings(includeTimer: false));
            OnPropertyChanged();
            if (_activated)
            {
                StartOrStopAuto();
            }
            else
            {
                // Not on screen yet: only the buttons change; Activate starts auto mode.
                RefreshCommands();
            }
        }
    }

    public string AutoStatusText => _shown.AutoStatusText;

    public RelayCommand StartCommand { get; }

    public RelayCommand BackwardCommand { get; }

    public RelayCommand PauseCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand StopRingCommand { get; }

    public IReadOnlyList<SpeedOption> SpeedOptions { get; } =
        SpeedPresets.All.Select(s => new SpeedOption(s, SpeedPresets.Label(s))).ToArray();

    public double ForwardSpeed
    {
        get => _timer.ForwardSpeed;
        set => SetSpeed(value, forward: true);
    }

    public double BackwardSpeed
    {
        get => _timer.BackwardSpeed;
        set => SetSpeed(value, forward: false);
    }

    public string DisplayTime => _shown.DisplayTime;

    public TimerState State => _shown.State;

    public TimerDirection Direction => _shown.Direction;

    public bool IsRinging => _shown.IsRinging;

    public string StatusText => _shown.StatusText;

    public double UpperSand => _shown.Sand.Upper;

    public double LowerSand => _shown.Sand.Lower;

    public bool IsSandFlowing => _shown.Sand.IsFlowing;

    /// <summary>
    /// Call once the window is on screen. A timer that was running when the app last closed carries on from
    /// here; waiting until now keeps window start-up time from counting as timer time.
    /// </summary>
    public void Activate()
    {
        _activated = true;
        StartOrStopAuto();
    }

    /// <summary>Saves the speeds and the timer's current position. Called when the app closes.</summary>
    public void SaveState()
    {
        if (_tickerRunning)
        {
            // Count the time since the last tick so closing doesn't lose it.
            _ticker.Flush();
        }

        _settings.Save(CurrentSettings(includeTimer: true));
    }

    public void Dispose()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnActiveWindowChanged;
            _watcher.Stop();
        }

        _ticker.Tick -= OnTick;
        _ticker.Stop();
        _alarm.Stop();
    }

    private void OnActiveWindowChanged(object? sender, ActiveWindow? window)
    {
        _activeWindow = window;
        if (_autoMode && _activated)
        {
            ApplyAuto();
        }
    }

    private void StartOrStopAuto()
    {
        if (_autoMode)
        {
            _watcher?.Start();
            ApplyAuto();
        }
        else
        {
            // Leave the timer where auto mode left it (normally paused); the buttons take over.
            _watcher?.Stop();
            _activeWindow = null;
            _lastDecision = default;
            Sync();
        }
    }

    private void OnRulesChanged()
    {
        _settings.Save(CurrentSettings(includeTimer: false));
        if (_autoMode && _activated)
        {
            ApplyAuto();
        }
    }

    /// <summary>Makes the timer do what the app or website in front calls for.</summary>
    private void ApplyAuto()
    {
        AutoDecision decision = _engine.Decide(_activeWindow, Rules.ToRules());
        _lastDecision = decision;
        switch (decision.Action)
        {
            case RuleAction.Forward:
                _timer.Start();
                break;
            case RuleAction.Backward when _timer.State == TimerState.Backward:
                break;
            case RuleAction.Backward when _timer.CanGoBackward:
                _timer.Backward();
                break;
            default:
                // Pause, or Backward with nothing left to count down.
                _timer.Pause();
                break;
        }

        Rules.NoteUnclassified(_engine.SeenUnclassified);
        Sync();
        if (decision.PromptDomain is { } domain)
        {
            ClassifyRequested?.Invoke(this, domain);
        }
    }

    private void OnTick(object? sender, TimeSpan realElapsed)
    {
        _timer.Advance(realElapsed);
        Sync();
    }

    private void Apply(Action action)
    {
        action();
        Sync();
    }

    private void SetSpeed(double value, bool forward)
    {
        double speed = SpeedPresets.Normalize(value);
        if (speed == (forward ? _timer.ForwardSpeed : _timer.BackwardSpeed))
        {
            return;
        }

        if (forward)
        {
            _timer.ForwardSpeed = speed;
        }
        else
        {
            _timer.BackwardSpeed = speed;
        }

        _settings.Save(CurrentSettings(includeTimer: false));
        OnPropertyChanged(forward ? nameof(ForwardSpeed) : nameof(BackwardSpeed));
    }

    /// <summary>Brings the ticker, alarm, bindings and command states in line with the timer.</summary>
    private void Sync()
    {
        if (_timer.IsRunning != _tickerRunning)
        {
            _tickerRunning = _timer.IsRunning;
            if (_tickerRunning)
            {
                _ticker.Start();
            }
            else
            {
                _ticker.Stop();
            }
        }

        if (_timer.IsRinging != _alarmPlaying)
        {
            _alarmPlaying = _timer.IsRinging;
            if (_alarmPlaying)
            {
                _alarm.Play();
                if (_autoMode)
                {
                    AttentionRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                _alarm.Stop();
            }
        }

        Snapshot previous = _shown;
        _shown = TakeSnapshot();

        // Direction is raised before the sand levels: the hourglass control reads the levels it is
        // still showing when a flip starts.
        RaiseIfChanged(previous.Direction, _shown.Direction, nameof(Direction));
        RaiseIfChanged(previous.DisplayTime, _shown.DisplayTime, nameof(DisplayTime));
        RaiseIfChanged(previous.State, _shown.State, nameof(State));
        RaiseIfChanged(previous.IsRinging, _shown.IsRinging, nameof(IsRinging));
        RaiseIfChanged(previous.StatusText, _shown.StatusText, nameof(StatusText));
        RaiseIfChanged(previous.AutoStatusText, _shown.AutoStatusText, nameof(AutoStatusText));
        RaiseIfChanged(previous.Sand.Upper, _shown.Sand.Upper, nameof(UpperSand));
        RaiseIfChanged(previous.Sand.Lower, _shown.Sand.Lower, nameof(LowerSand));
        RaiseIfChanged(previous.Sand.IsFlowing, _shown.Sand.IsFlowing, nameof(IsSandFlowing));

        RefreshCommands();
    }

    private void RefreshCommands()
    {
        bool[] canExecute = CurrentCanExecute();
        for (int i = 0; i < _commands.Length; i++)
        {
            if (canExecute[i] != _lastCanExecute[i])
            {
                _commands[i].RaiseCanExecuteChanged();
            }
        }

        _lastCanExecute = canExecute;
    }

    /// <summary>The timer is only written on close (<see cref="SaveState"/>); other saves store just the speeds.</summary>
    private HourglassSettings CurrentSettings(bool includeTimer) => new(
        _timer.ForwardSpeed,
        _timer.BackwardSpeed,
        includeTimer ? _timer.ToSnapshot() : null,
        _autoMode,
        Rules.ToRules());

    private Snapshot TakeSnapshot() => new(
        TimeFormatter.Format(_timer.Value, roundUp: _timer.LastDirection == TimerDirection.Backward),
        _timer.State,
        _timer.LastDirection,
        _timer.IsRinging,
        _timer switch
        {
            { IsRinging: true } => "Time's up!",
            { State: TimerState.Forward } => "Counting forward",
            { State: TimerState.Backward } => "Counting backward",
            { State: TimerState.Paused } => "Paused",
            _ => "Ready",
        },
        SandLevel.From(_timer),
        DescribeAuto());

    private string DescribeAuto()
    {
        if (!_autoMode)
        {
            return "";
        }

        AutoDecision d = _lastDecision;
        string doing = d.Action switch
        {
            RuleAction.Forward => "counting forward",
            RuleAction.Backward when _timer.Value == TimeSpan.Zero && !_timer.IsRunning => "nothing left to count down",
            RuleAction.Backward => "counting backward",
            _ => "paused",
        };

        return d.Reason switch
        {
            AutoReason.AppRule or AutoReason.SiteRule => $"Auto · {d.Subject}: {doing}",
            AutoReason.UnclassifiedSite => $"Auto · {d.Subject} (not classified yet): {doing}",
            AutoReason.InternalPage => $"Auto · paused ({d.Subject} page)",
            AutoReason.UnknownPage => $"Auto · paused (can't read {d.Subject}'s address bar yet)",
            AutoReason.UntrackedApp => $"Auto · paused ({d.Subject} isn't tracked)",
            _ => "Auto · paused until a tracked app or site is in front",
        };
    }

    private void RaiseIfChanged<T>(T previous, T current, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(previous, current))
        {
            OnPropertyChanged(name);
        }
    }

    private bool[] CurrentCanExecute() => Array.ConvertAll(_commands, c => c.CanExecute(null));

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>What the window currently shows; compared between syncs so only real changes are raised.</summary>
    private readonly record struct Snapshot(
        string DisplayTime,
        TimerState State,
        TimerDirection Direction,
        bool IsRinging,
        string StatusText,
        SandLevel Sand,
        string AutoStatusText);
}
