namespace Hourglass.Core;

/// <summary>How full each bulb looks on screen, from 0 (empty) to 1 (full).</summary>
/// <param name="Upper">Fill of the bulb currently on top.</param>
/// <param name="Lower">Fill of the bulb currently at the bottom.</param>
/// <param name="IsFlowing">Whether sand is visibly falling (the timer is running).</param>
public readonly record struct SandLevel(double Upper, double Lower, bool IsFlowing)
{
    /// <summary>Forward runs fill the lower bulb once every this many timer seconds, then start over.</summary>
    public static readonly TimeSpan ForwardCycle = TimeSpan.FromSeconds(60);

    public static SandLevel From(HourglassTimer timer) =>
        Compute(timer.LastDirection, timer.Value, timer.BackwardPeak, timer.IsRunning);

    public static SandLevel Compute(TimerDirection direction, TimeSpan value, TimeSpan backwardPeak, bool isRunning)
    {
        switch (direction)
        {
            case TimerDirection.Forward:
            {
                double cycleFraction = (double)(value.Ticks % ForwardCycle.Ticks) / ForwardCycle.Ticks;
                return new SandLevel(1 - cycleFraction, cycleFraction, isRunning);
            }

            case TimerDirection.Backward:
            {
                double remaining = backwardPeak > TimeSpan.Zero
                    ? Math.Clamp((double)value.Ticks / backwardPeak.Ticks, 0, 1)
                    : 0;
                return new SandLevel(remaining, 1 - remaining, isRunning);
            }

            default:
                // Fresh hourglass: all sand on top, ready to start.
                return new SandLevel(1, 0, false);
        }
    }
}
