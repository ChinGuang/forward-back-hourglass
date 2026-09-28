using System.Net;

namespace Hourglass.Core;

/// <summary>Domain helpers for website rules. Hosts and domains are compared in lower case.</summary>
public static class Domains
{
    // Second-level labels that act like a TLD under two-letter country codes (bbc.co.uk, abc.net.au, …).
    private static readonly HashSet<string> CountrySecondLevels = ["co", "com", "net", "org", "gov", "edu", "ac"];

    /// <summary>
    /// Turns user input into a rule domain: <c>https://www.YouTube.com/watch</c>, <c>*.youtube.com</c> and
    /// <c>youtube.com</c> all become <c>youtube.com</c>. Returns null if it isn't a usable domain.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        string text = input.Trim();
        if (text.StartsWith("*.", StringComparison.Ordinal))
        {
            text = text[2..];
        }

        if (BrowserUrl.Parse(text) is not { Kind: BrowserUrlKind.Site, Host: { } host })
        {
            return null;
        }

        return host.StartsWith("www.", StringComparison.Ordinal) && host.Length > 4 ? host[4..] : host;
    }

    public static bool IsSameOrSubdomain(string host, string domain)
    {
        host = host.ToLowerInvariant();
        domain = domain.ToLowerInvariant();
        return host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);
    }

    /// <summary>
    /// The domain to offer when a site isn't classified yet: the site's own name without subdomains,
    /// e.g. <c>m.youtube.com</c> → <c>youtube.com</c>, <c>news.bbc.co.uk</c> → <c>bbc.co.uk</c>.
    /// </summary>
    public static string Suggest(string host)
    {
        host = host.ToLowerInvariant();
        if (IPAddress.TryParse(host, out _) || !host.Contains('.'))
        {
            return host;
        }

        string[] labels = host.Split('.');
        int keep = labels.Length >= 3 && labels[^1].Length == 2 && CountrySecondLevels.Contains(labels[^2]) ? 3 : 2;
        return string.Join('.', labels[^Math.Min(keep, labels.Length)..]);
    }
}
