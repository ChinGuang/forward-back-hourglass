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
    private readonly RelayCommand[] _commands;
    private bool _tickerRunning;
    private bool _alarmPlaying;
    private bool[] _lastCanExecute = [];

    public MainViewModel(ITicker ticker, IAlarmPlayer alarm, ISettingsStore settings)
    {
        _ticker = ticker;
        _alarm = alarm;
        _settings = settings;

        HourglassSettings saved = settings.Load();
        _timer.ForwardSpeed = SpeedPresets.Normalize(saved.ForwardSpeed);
        _timer.BackwardSpeed = SpeedPresets.Normalize(saved.BackwardSpeed);

        StartCommand = new RelayCommand(() => Apply(() => _timer.Start()), () => _timer.CanStart);
        BackwardCommand = new RelayCommand(() => Apply(() => _timer.Backward()), () => _timer.CanGoBackward);
        PauseCommand = new RelayCommand(() => Apply(() => _timer.Pause()), () => _timer.CanPause);
        ResetCommand = new RelayCommand(() => Apply(_timer.Reset), () => _timer.CanReset);
        StopRingCommand = new RelayCommand(() => Apply(_timer.StopRing), () => _timer.IsRinging);
        _commands = [StartCommand, BackwardCommand, PauseCommand, ResetCommand, StopRingCommand];

        _ticker.Tick += OnTick;
        _lastCanExecute = CurrentCanExecute();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

    public string DisplayTime => TimeFormatter.Format(_timer.Value);

    public TimerState State => _timer.State;

    public TimerDirection Direction => _timer.LastDirection;

    public bool IsRinging => _timer.IsRinging;

    public string StatusText => _timer switch
    {
        { IsRinging: true } => "Time's up!",
        { State: TimerState.Forward } => "Counting forward",
        { State: TimerState.Backward } => "Counting backward",
        { State: TimerState.Paused } => "Paused",
        _ => "Ready",
    };

    public double UpperSand => SandLevel.From(_timer).Upper;

    public double LowerSand => SandLevel.From(_timer).Lower;

    public bool IsSandFlowing => SandLevel.From(_timer).IsFlowing;

    public void Dispose()
    {
        _ticker.Tick -= OnTick;
        _ticker.Stop();
        _alarm.Stop();
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

        _settings.Save(new HourglassSettings(_timer.ForwardSpeed, _timer.BackwardSpeed));
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
            }
            else
            {
                _alarm.Stop();
            }
        }

        OnPropertyChanged(nameof(DisplayTime));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(Direction));
        OnPropertyChanged(nameof(IsRinging));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(UpperSand));
        OnPropertyChanged(nameof(LowerSand));
        OnPropertyChanged(nameof(IsSandFlowing));

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

    private bool[] CurrentCanExecute() => Array.ConvertAll(_commands, c => c.CanExecute(null));

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
