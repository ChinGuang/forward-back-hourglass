using System.Globalization;

namespace Hourglass.Core;

/// <summary>The speed multipliers offered for each direction.</summary>
public static class SpeedPresets
{
    public const double Default = 1.0;

    public static IReadOnlyList<double> All { get; } = [0.25, 0.5, 1.0, 2.0, 4.0, 8.0];

    /// <summary>What a rule without its own speed shows, and types, to use the main Forward/Backward speed.</summary>
    public const string DefaultLabel = "Default";

    /// <summary>Formats a multiplier for display, e.g. 0.25 → "0.25×", 2 → "2×", 1.5 → "1.5×".</summary>
    public static string Label(double speed) => speed.ToString("G6", CultureInfo.InvariantCulture) + "×";

    /// <summary>A rule's speed for display: its own multiplier, or "Default".</summary>
    public static string Label(double? speed) => speed is { } value ? Label(value) : DefaultLabel;

    /// <summary>Any finite multiplier above zero; there is no upper limit (the timer saturates safely).</summary>
    public static bool IsValid(double speed) => double.IsFinite(speed) && speed > 0;

    /// <summary>
    /// Reads a rule speed typed by the user. Empty or "Default" means no override (null). Otherwise a positive
    /// number, decimals allowed, with an optional "x" or "×" and either "." or "," as the decimal point:
    /// "4", "1.5x", "0,25×". Returns false for zero, negatives and anything that isn't a number.
    /// </summary>
    public static bool TryParseRuleSpeed(string? text, out double? speed)
    {
        speed = null;
        string value = (text ?? "").Trim();
        if (value.Length == 0 || value.Equals(DefaultLabel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        value = value.TrimEnd('x', 'X', '×').Trim().Replace(',', '.');
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double parsed)
            || !IsValid(parsed))
        {
            return false;
        }

        speed = parsed;
        return true;
    }

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
