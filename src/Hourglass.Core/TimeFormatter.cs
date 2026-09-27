using System.Globalization;

namespace Hourglass.Core;

public static class TimeFormatter
{
    /// <summary>
    /// Formats as <c>HH:MM:SS.t</c> (tenths truncated). Hours keep growing past 99 because forward is unbounded.
    /// </summary>
    public static string Format(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        long totalHours = (long)value.TotalHours;
        int tenths = value.Milliseconds / 100;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{tenths}");
    }
}
