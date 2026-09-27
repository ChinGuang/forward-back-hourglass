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
/// The hourglass timer state machine. It has no clock of its own: the host calls
/// <see cref="Advance"/> with the real time that has passed, which keeps it deterministic and testable.
/// </summary>
public sealed class HourglassTimer
{
    private double _forwardSpeed = SpeedPresets.Default;
    private double _backwardSpeed = SpeedPresets.Default;

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
    }

    public void StopRing() => IsRinging = false;

    /// <summary>Moves the timer on by <paramref name="realElapsed"/> of wall-clock time, scaled by the active speed.</summary>
    public void Advance(TimeSpan realElapsed)
    {
        if (realElapsed <= TimeSpan.Zero || !IsRunning)
        {
            return;
        }

        if (State == TimerState.Forward)
        {
            TimeSpan step = realElapsed * ForwardSpeed;
            Value = step > TimeSpan.MaxValue - Value ? TimeSpan.MaxValue : Value + step;
            return;
        }

        TimeSpan drain = realElapsed * BackwardSpeed;
        if (drain < Value)
        {
            Value -= drain;
            return;
        }

        Value = TimeSpan.Zero;
        State = TimerState.Idle;
        IsRinging = true;
    }

    private static double ValidateSpeed(double speed)
    {
        if (!double.IsFinite(speed) || speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be a positive, finite number.");
        }

        return speed;
    }
}
