namespace Hourglass.Core;

/// <summary>What the timer is doing right now.</summary>
public enum TimerState
{
    /// <summary>Not running: freshly reset, or stopped after draining to zero.</summary>
    Idle,
    Forward,
    Backward,
    Paused,
}

/// <summary>The last direction the timer ran in (used to resume and to draw the hourglass).</summary>
public enum TimerDirection
{
    None,
    Forward,
    Backward,
}

/// <summary>
/// A timer's position, saved when the app closes and restored on the next launch. Ringing is deliberately not
/// part of it: a countdown that finished before closing reopens drained and silent.
/// </summary>
public sealed record TimerSnapshot(TimeSpan Value, TimerState State, TimerDirection LastDirection, TimeSpan BackwardPeak);

/// <summary>
/// The hourglass timer state machine. It has no clock of its own: the host calls
/// <see cref="Advance"/> with the real time that has passed, which keeps it deterministic and testable.
/// </summary>
public sealed class HourglassTimer
{
    private double _forwardSpeed = SpeedPresets.Default;
    private double _backwardSpeed = SpeedPresets.Default;
    private double? _speedOverride;

    // Fractions of a tick not yet counted, so very small speeds still add up over many frames.
    private double _tickRemainder;

    /// <summary>The current hourglass time. Never negative.</summary>
    public TimeSpan Value { get; private set; } = TimeSpan.Zero;

    public TimerState State { get; private set; } = TimerState.Idle;

    public TimerDirection LastDirection { get; private set; } = TimerDirection.None;

    /// <summary>The value when the current backward run began; the hourglass drains relative to it.</summary>
    public TimeSpan BackwardPeak { get; private set; } = TimeSpan.Zero;

    /// <summary>True from the moment backward reaches zero until the ring is dismissed.</summary>
    public bool IsRinging { get; private set; }

    public bool IsRunning => State is TimerState.Forward or TimerState.Backward;

    public bool CanStart => State != TimerState.Forward;

    public bool CanGoBackward => State != TimerState.Backward && Value > TimeSpan.Zero;

    public bool CanPause => IsRunning;

    public bool CanReset => Value > TimeSpan.Zero || State != TimerState.Idle || IsRinging || LastDirection != TimerDirection.None;

    /// <summary>
    /// A speed that replaces <see cref="ForwardSpeed"/>/<see cref="BackwardSpeed"/> for whichever direction is
    /// running (auto mode sets it from the app or website in front); null uses the normal speeds.
    /// </summary>
    public double? SpeedOverride
    {
        get => _speedOverride;
        set => _speedOverride = value is { } speed ? ValidateSpeed(speed) : null;
    }

    /// <summary>The multiplier in effect for the current (or last) direction.</summary>
    public double CurrentSpeed =>
        _speedOverride ?? (LastDirection == TimerDirection.Backward ? BackwardSpeed : ForwardSpeed);

    /// <summary>Speed multiplier while counting forward (e.g. 2 = two timer seconds per real second).</summary>
    public double ForwardSpeed
    {
        get => _forwardSpeed;
        set => _forwardSpeed = ValidateSpeed(value);
    }

    /// <summary>Speed multiplier while counting backward.</summary>
    public double BackwardSpeed
    {
        get => _backwardSpeed;
        set => _backwardSpeed = ValidateSpeed(value);
    }

    /// <summary>Count forward from the current value. Also resumes from pause and flips a backward run.</summary>
    public bool Start()
    {
        if (!CanStart)
        {
            return false;
        }

        IsRinging = false;
        State = TimerState.Forward;
        LastDirection = TimerDirection.Forward;
        _tickRemainder = 0;
        return true;
    }

    /// <summary>Count backward toward zero. Ignored when the value is already zero.</summary>
    public bool Backward()
    {
        if (!CanGoBackward)
        {
            return false;
        }

        // Resuming a paused backward run keeps its peak so the hourglass doesn't jump back to full.
        bool resumingBackward = State == TimerState.Paused && LastDirection == TimerDirection.Backward;
        if (!resumingBackward)
        {
            BackwardPeak = Value;
        }

        IsRinging = false;
        State = TimerState.Backward;
        LastDirection = TimerDirection.Backward;
        _tickRemainder = 0;
        return true;
    }

    public bool Pause()
    {
        if (!CanPause)
        {
            return false;
        }

        State = TimerState.Paused;
        return true;
    }

    /// <summary>Back to zero, stopped, not ringing.</summary>
    public void Reset()
    {
        Value = TimeSpan.Zero;
        BackwardPeak = TimeSpan.Zero;
        State = TimerState.Idle;
        LastDirection = TimerDirection.None;
        IsRinging = false;
        _tickRemainder = 0;
    }

    public void StopRing() => IsRinging = false;

    public TimerSnapshot ToSnapshot() => new(Value, State, LastDirection, BackwardPeak);

    /// <summary>
    /// Puts the timer back where a <see cref="ToSnapshot"/> left it, including running. A snapshot that no real
    /// timer could produce (e.g. from a hand-edited file) is rejected: the timer is reset and this returns false.
    /// </summary>
    public bool Restore(TimerSnapshot snapshot)
    {
        Reset();
        if (!IsConsistent(snapshot))
        {
            return false;
        }

        Value = snapshot.Value;
        State = snapshot.State;
        LastDirection = snapshot.LastDirection;
        // The drain is shown relative to the peak, so it can never be below the time still left.
        BackwardPeak = snapshot.LastDirection == TimerDirection.Backward
            ? (snapshot.BackwardPeak > snapshot.Value ? snapshot.BackwardPeak : snapshot.Value)
            : TimeSpan.Zero;
        return true;
    }

    /// <summary>Moves the timer on by <paramref name="realElapsed"/> of wall-clock time, scaled by the active speed.</summary>
    public void Advance(TimeSpan realElapsed)
    {
        if (realElapsed <= TimeSpan.Zero || !IsRunning)
        {
            return;
        }

        if (State == TimerState.Forward)
        {
            TimeSpan step = Scale(realElapsed, CurrentSpeed, ref _tickRemainder);
            Value = step > TimeSpan.MaxValue - Value ? TimeSpan.MaxValue : Value + step;
            return;
        }

        TimeSpan drain = Scale(realElapsed, CurrentSpeed, ref _tickRemainder);
        if (drain < Value)
        {
            Value -= drain;
            return;
        }

        Value = TimeSpan.Zero;
        State = TimerState.Idle;
        IsRinging = true;
    }

    private static bool IsConsistent(TimerSnapshot s) =>
        Enum.IsDefined(s.State)
        && Enum.IsDefined(s.LastDirection)
        && s.Value >= TimeSpan.Zero
        && s.BackwardPeak >= TimeSpan.Zero
        && s.State switch
        {
            TimerState.Forward => s.LastDirection == TimerDirection.Forward,
            TimerState.Backward => s.LastDirection == TimerDirection.Backward && s.Value > TimeSpan.Zero,
            // A countdown that reaches zero goes Idle, so a paused one always has time left.
            TimerState.Paused => s.LastDirection == TimerDirection.Forward
                || (s.LastDirection == TimerDirection.Backward && s.Value > TimeSpan.Zero),
            // Idle only happens fresh (after Reset) or drained to zero by a countdown.
            _ => s.Value == TimeSpan.Zero && s.LastDirection != TimerDirection.Forward,
        };

    /// <summary>
    /// elapsed × speed in whole ticks, carrying the fraction to the next call (so 1e-6× still counts, just slowly)
    /// and saturating at <see cref="TimeSpan.MaxValue"/> instead of overflowing for huge speeds.
    /// </summary>
    private static TimeSpan Scale(TimeSpan elapsed, double speed, ref double remainder)
    {
        double exact = elapsed.Ticks * speed + remainder;
        if (exact >= TimeSpan.MaxValue.Ticks)
        {
            remainder = 0;
            return TimeSpan.MaxValue;
        }

        // Round away float noise (e.g. 2.9999999999 → 3) before splitting off the fraction.
        double whole = Math.Floor(exact + 1e-6);
        remainder = Math.Max(exact - whole, 0);
        return TimeSpan.FromTicks((long)whole);
    }

    private static double ValidateSpeed(double speed)
    {
        if (!SpeedPresets.IsValid(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be a positive, finite number.");
        }

        return speed;
    }
}
