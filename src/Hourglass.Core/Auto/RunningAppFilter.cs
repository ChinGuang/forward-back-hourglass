namespace Hourglass.Core;

/// <summary>An open app as the "Add running app" list shows it.</summary>
public interface IRunningApp
{
    /// <summary>Program file name, e.g. <c>Code.exe</c>.</summary>
    string FileName { get; }

    /// <summary>The titles of all its open windows, e.g. "Slack – general". All of them are searchable.</summary>
    IReadOnlyList<string> Titles { get; }
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
        Trackable(running).Where(app => rules.FindApp(app.FileName) is null).ToList();

    /// <summary>Open apps that could be tracked at all: everything except supported browsers.</summary>
    public static IReadOnlyList<T> Trackable<T>(IEnumerable<T> running)
        where T : IRunningApp =>
        running.Where(app => KnownBrowsers.Find(app.FileName) is null).ToList();

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
                || app.Titles.Any(title => title.Contains(text, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>What to show instead of an empty list, or null when there is something to pick.</summary>
    /// <param name="trackableCount">How many open apps there are, browsers aside.</param>
    /// <param name="addableCount">How many of those aren't added yet.</param>
    /// <param name="matchCount">How many of those match the search.</param>
    public static string? EmptyMessage(int trackableCount, int addableCount, int matchCount, string? query)
    {
        if (trackableCount == 0)
        {
            return "No other open apps found. Use \"Browse for .exe…\" to add a program.";
        }

        if (addableCount == 0)
        {
            return "All open apps are already added. Use \"Browse for .exe…\" to add another program.";
        }

        return matchCount == 0 ? $"No open app matches \"{query?.Trim()}\"." : null;
    }
}
