namespace Hourglass.Core;

public enum BrowserUrlKind
{
    /// <summary>Nothing usable: empty, or text being typed into the address bar.</summary>
    Unreadable,

    /// <summary>A browser page such as a new tab, settings, <c>about:</c> or a local file. Never counts.</summary>
    Internal,

    /// <summary>A website with a host name.</summary>
    Site,
}

/// <summary>What the address bar of a browser shows, reduced to what the rules need.</summary>
public readonly record struct BrowserUrl(BrowserUrlKind Kind, string? Host)
{
    private static readonly BrowserUrl Unreadable = new(BrowserUrlKind.Unreadable, null);
    private static readonly BrowserUrl Internal = new(BrowserUrlKind.Internal, null);

    /// <summary>
    /// Parses address bar text. Chromium browsers usually hide the scheme (<c>youtube.com/watch?v=…</c>), so text
    /// without one is treated as https.
    /// </summary>
    public static BrowserUrl Parse(string? addressBarText)
    {
        string text = addressBarText?.Trim() ?? "";
        if (text.Length == 0 || text.Any(char.IsWhiteSpace))
        {
            return Unreadable;
        }

        // A scheme is only a scheme at the very start (letters, digits, + - .); "://" later on is part of the
        // path or query, e.g. web.archive.org/web/2020/https://example.com.
        int schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd >= 0 && !IsScheme(text.AsSpan(0, schemeEnd)))
        {
            schemeEnd = -1;
        }

        if (schemeEnd < 0 && (text.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("view-source:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)))
        {
            return Internal;
        }

        if (schemeEnd >= 0)
        {
            string scheme = text[..schemeEnd].ToLowerInvariant();
            if (scheme is not ("http" or "https"))
            {
                return Internal;
            }
        }
        else
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri.Host))
        {
            return Unreadable;
        }

        // IdnHost is the ASCII (punycode) form, so rules and URLs compare the same way.
        string host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        bool looksLikeHost = host.Contains('.') || host == "localhost" || uri.HostNameType == UriHostNameType.IPv6;
        return looksLikeHost ? new BrowserUrl(BrowserUrlKind.Site, host) : Unreadable;
    }

    private static bool IsScheme(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || !char.IsAsciiLetter(text[0]))
        {
            return false;
        }

        foreach (char c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
