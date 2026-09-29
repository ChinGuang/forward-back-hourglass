namespace Hourglass.Core;

public enum AutoReason
{
    /// <summary>No window information (e.g. just switched on).</summary>
    NoWindow,

    /// <summary>The app in front has no rule, or it is the hourglass itself.</summary>
    UntrackedApp,

    AppRule,

    SiteRule,

    /// <summary>A website without a rule: counts backward by default and asks the user to classify it.</summary>
    UnclassifiedSite,

    /// <summary>A browser page such as a new tab or settings.</summary>
    InternalPage,

    /// <summary>A browser whose address bar couldn't be read and has no last known site for that window.</summary>
    UnknownPage,
}

/// <summary>What auto mode wants the timer to do, and why.</summary>
/// <param name="Subject">The app, domain or browser the decision is about (for the status line).</param>
/// <param name="PromptDomain">Set once per session for a newly seen unclassified site: ask the user about it.</param>
public readonly record struct AutoDecision(RuleAction Action, AutoReason Reason, string? Subject = null, string? PromptDomain = null);

/// <summary>Turns "which window is in front" into a timer action using the user's rules.</summary>
public sealed class AutoModeEngine
{
    private const int MaxRememberedWindows = 64;

    private readonly Dictionary<long, BrowserUrl> _lastUrlByWindow = [];
    private readonly HashSet<string> _prompted = [];

    public AutoDecision Decide(ActiveWindow? window, AutoRules rules)
    {
        if (window is null)
        {
            return new AutoDecision(RuleAction.Pause, AutoReason.NoWindow);
        }

        if (KnownBrowsers.Find(window.ProcessFileName) is { } browser)
        {
            return DecideForBrowser(window, browser, rules);
        }

        return rules.FindApp(window.ProcessFileName) is { } app
            ? new AutoDecision(app.Action, AutoReason.AppRule, app.FileName)
            : new AutoDecision(RuleAction.Pause, AutoReason.UntrackedApp, AppRule.NormalizeFileName(window.ProcessFileName));
    }

    private AutoDecision DecideForBrowser(ActiveWindow window, BrowserProfile browser, AutoRules rules)
    {
        BrowserUrl url = BrowserUrl.Parse(window.AddressBarText);
        if (url.Kind == BrowserUrlKind.Unreadable)
        {
            // Full-screen video, F11, typing in the address bar…: keep what this window last showed.
            if (!_lastUrlByWindow.TryGetValue(window.WindowHandle, out url))
            {
                return new AutoDecision(RuleAction.Pause, AutoReason.UnknownPage, browser.DisplayName);
            }
        }
        else
        {
            Remember(window.WindowHandle, url);
        }

        if (url.Kind == BrowserUrlKind.Internal || url.Host is null)
        {
            return new AutoDecision(RuleAction.Pause, AutoReason.InternalPage, browser.DisplayName);
        }

        if (rules.FindSite(url.Host) is { } site)
        {
            return new AutoDecision(site.Action, AutoReason.SiteRule, site.Domain);
        }

        string suggested = Domains.Suggest(url.Host);
        string? prompt = _prompted.Add(suggested) ? suggested : null;
        return new AutoDecision(RuleAction.Backward, AutoReason.UnclassifiedSite, suggested, prompt);
    }

    private void Remember(long windowHandle, BrowserUrl url)
    {
        if (_lastUrlByWindow.Count >= MaxRememberedWindows && !_lastUrlByWindow.ContainsKey(windowHandle))
        {
            _lastUrlByWindow.Clear();
        }

        _lastUrlByWindow[windowHandle] = url;
    }
}
