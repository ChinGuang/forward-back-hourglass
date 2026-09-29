namespace Hourglass.Core;

/// <summary>What the timer does while a tracked app or website is in front.</summary>
public enum RuleAction
{
    Forward,
    Backward,
    Pause,
}

/// <summary>An app matched by its program file name (e.g. <c>Code.exe</c>), case-insensitively.</summary>
/// <param name="Speed">Its own multiplier; null uses the main Forward/Backward speed.</param>
public sealed record AppRule(string FileName, RuleAction Action, double? Speed = null)
{
    /// <summary>
    /// Turns a path or name into the stored form: <c>C:\Apps\Code.exe</c>, <c>Code.exe</c> and <c>Code</c> all
    /// become <c>Code.exe</c>. Returns null when nothing usable is left.
    /// </summary>
    public static string? NormalizeFileName(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return null;
        }

        string name = pathOrName.Trim().Trim('"');
        int slash = name.LastIndexOfAny(['\\', '/']);
        name = name[(slash + 1)..].Trim();
        if (name.Length == 0)
        {
            return null;
        }

        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
    }

    public bool Matches(string processFileName) =>
        string.Equals(NormalizeFileName(processFileName), FileName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A website matched by domain: <c>youtube.com</c> also covers <c>www.youtube.com</c>, <c>m.youtube.com</c>, …</summary>
/// <param name="Speed">Its own multiplier; null uses the main Forward/Backward speed.</param>
public sealed record SiteRule(string Domain, RuleAction Action, double? Speed = null)
{
    public bool Matches(string host) => Domains.IsSameOrSubdomain(host, Domain);
}

/// <summary>All tracked apps and websites. Compared by content so saved settings can be checked for equality.</summary>
public sealed record AutoRules(IReadOnlyList<AppRule> Apps, IReadOnlyList<SiteRule> Sites)
{
    public static AutoRules Empty { get; } = new([], []);

    public AppRule? FindApp(string processFileName) => Apps.FirstOrDefault(rule => rule.Matches(processFileName));

    /// <summary>The most specific matching rule: <c>music.youtube.com</c> beats <c>youtube.com</c>.</summary>
    public SiteRule? FindSite(string host) =>
        Sites.Where(rule => rule.Matches(host)).MaxBy(rule => rule.Domain.Length);

    /// <summary>
    /// Cleans rules from a settings file: normalises names, drops unusable or unknown entries and keeps the last
    /// rule for any duplicate name.
    /// </summary>
    public AutoRules Sanitized()
    {
        var apps = new List<AppRule>();
        foreach (AppRule rule in Apps ?? [])
        {
            string? name = AppRule.NormalizeFileName(rule?.FileName);
            if (rule is null || name is null || !Enum.IsDefined(rule.Action))
            {
                continue;
            }

            apps.RemoveAll(existing => string.Equals(existing.FileName, name, StringComparison.OrdinalIgnoreCase));
            apps.Add(new AppRule(name, rule.Action, ValidSpeedOrDefault(rule.Speed)));
        }

        var sites = new List<SiteRule>();
        foreach (SiteRule rule in Sites ?? [])
        {
            string? domain = Domains.Normalize(rule?.Domain);
            if (rule is null || domain is null || !Enum.IsDefined(rule.Action))
            {
                continue;
            }

            sites.RemoveAll(existing => existing.Domain == domain);
            sites.Add(new SiteRule(domain, rule.Action, ValidSpeedOrDefault(rule.Speed)));
        }

        return new AutoRules(apps, sites);
    }

    /// <summary>A hand-edited speed of 0, a negative number or infinity falls back to the main speed.</summary>
    private static double? ValidSpeedOrDefault(double? speed) =>
        speed is { } value && SpeedPresets.IsValid(value) ? value : null;

    public bool Equals(AutoRules? other) =>
        other is not null && Apps.SequenceEqual(other.Apps) && Sites.SequenceEqual(other.Sites);

    public override int GetHashCode() => HashCode.Combine(Apps.Count, Sites.Count);
}
