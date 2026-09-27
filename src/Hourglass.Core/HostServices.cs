namespace Hourglass.Core;

/// <summary>Drives the timer: raises <see cref="Tick"/> with the real time elapsed since the previous tick.</summary>
public interface ITicker
{
    event EventHandler<TimeSpan>? Tick;

    void Start();

    void Stop();

    /// <summary>Raises <see cref="Tick"/> right away for the time since the last tick. Does nothing when stopped.</summary>
    void Flush();
}

/// <summary>Plays the ring sound on a loop until stopped.</summary>
public interface IAlarmPlayer
{
    void Play();

    void Stop();
}
