namespace Hourglass.Core;

/// <summary>An open app as the "Add running app" list shows it.</summary>
public interface IRunningApp
{
    /// <summary>Program file name, e.g. <c>Code.exe</c>.</summary>
    string FileName { get; }

    /// <summary>Its main window's title, e.g. "Slack – general".</summary>
    string Title { get; }
}

/// <summary>Decides which open apps the "Add running app" list offers, and what it says when there are none.</summary>
public static class RunningAppFilter
{
    /// <summary>
    /// Open apps that can still be added: not already a rule (matched the same way auto mode matches, so
    /// <c>code.exe</c> counts as <c>Code.exe</c>) and not a supported browser (websites decide there).
    /// </summary>
    public static IReadOnlyList<T> Addable<T>(IEnumerable<T> running, AutoRules rules)
        where T : IRunningApp =>
        running
            .Where(app => KnownBrowsers.Find(app.FileName) is null && rules.FindApp(app.FileName) is null)
            .ToList();

    /// <summary>Apps whose program name or window title contains the search text, ignoring case. Blank shows all.</summary>
    public static IReadOnlyList<T> Search<T>(IReadOnlyList<T> apps, string? query)
        where T : IRunningApp
    {
        string text = query?.Trim() ?? "";
        if (text.Length == 0)
        {
            return apps;
        }

        return apps
            .Where(app => app.FileName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || app.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>What to show instead of an empty list, or null when there is something to pick.</summary>
    /// <param name="addableCount">How many open apps could be added at all.</param>
    /// <param name="matchCount">How many of those match the search.</param>
    public static string? EmptyMessage(int addableCount, int matchCount, string? query)
    {
        if (addableCount == 0)
        {
            return "All open apps are already added. Use \"Browse for .exe…\" to add another program.";
        }

        return matchCount == 0 ? $"No open app matches \"{query?.Trim()}\"." : null;
    }
}
