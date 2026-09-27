using System.Globalization;

namespace Hourglass.Core;

/// <summary>The speed multipliers offered for each direction.</summary>
public static class SpeedPresets
{
    public const double Default = 1.0;

    public static IReadOnlyList<double> All { get; } = [0.25, 0.5, 1.0, 2.0, 4.0, 8.0];

    /// <summary>Formats a multiplier for display, e.g. 0.25 → "0.25×", 2 → "2×".</summary>
    public static string Label(double speed) => speed.ToString("0.##", CultureInfo.InvariantCulture) + "×";

    /// <summary>Snaps any value (e.g. from an old or hand-edited settings file) to the nearest preset.</summary>
    public static double Normalize(double speed)
    {
        if (!double.IsFinite(speed) || speed <= 0)
        {
            return Default;
        }

        // Compare on a log scale so 0.25→0.5 and 4→8 count as equally far apart.
        return All.MinBy(preset => Math.Abs(Math.Log(preset) - Math.Log(speed)));
    }
}
