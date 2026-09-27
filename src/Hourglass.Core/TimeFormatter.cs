using System.Globalization;

namespace Hourglass.Core;

public static class TimeFormatter
{
    private const long TicksPerTenth = TimeSpan.TicksPerMillisecond * 100;

    /// <summary>
    /// Formats as <c>HH:MM:SS.t</c>. Hours keep growing past 99 because forward is unbounded.
    /// Tenths are truncated, or rounded up when <paramref name="roundUp"/> is set so a countdown
    /// only shows <c>00:00:00.0</c> once it has truly reached zero.
    /// </summary>
    public static string Format(TimeSpan value, bool roundUp = false)
    {
        long ticks = Math.Max(value.Ticks, 0);
        long wholeTenths = ticks / TicksPerTenth;
        if (roundUp && ticks % TicksPerTenth != 0 && wholeTenths < TimeSpan.MaxValue.Ticks / TicksPerTenth)
        {
            wholeTenths++;
        }

        value = TimeSpan.FromTicks(wholeTenths * TicksPerTenth);
        long totalHours = (long)value.TotalHours;
        int tenths = value.Milliseconds / 100;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{tenths}");
    }
}
