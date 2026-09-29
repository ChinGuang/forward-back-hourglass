namespace Hourglass.Core;

/// <summary>A browser whose address bar auto mode can read.</summary>
/// <param name="FileName">Program file name, e.g. <c>brave.exe</c>.</param>
/// <param name="Experimental">
/// True for browsers that weren't asked for but use the same engine and are expected to work (shown as untested).
/// </param>
/// <param name="SearchInsideDocuments">
/// Vivaldi draws its own toolbar as a web page, so its address field sits inside a document; other browsers keep
/// it outside, and skipping documents there avoids crawling the page itself.
/// </param>
public sealed record BrowserProfile(string FileName, string DisplayName, bool Experimental, bool SearchInsideDocuments);

public static class KnownBrowsers
{
    public static IReadOnlyList<BrowserProfile> All { get; } =
    [
        new("brave.exe", "Brave", Experimental: false, SearchInsideDocuments: false),
        new("opera.exe", "Opera", Experimental: false, SearchInsideDocuments: false),
        new("vivaldi.exe", "Vivaldi", Experimental: false, SearchInsideDocuments: true),
        new("chrome.exe", "Chrome", Experimental: true, SearchInsideDocuments: false),
        new("msedge.exe", "Edge", Experimental: true, SearchInsideDocuments: false),
    ];

    /// <summary>English accessible names browsers give their address bar. Other languages fall back to the URL check.</summary>
    public static IReadOnlyList<string> AddressBarNames { get; } =
    [
        "Address and search bar",
        "Address field",
        "Search or enter an address",
        "Search or enter address",
    ];

    public static BrowserProfile? Find(string? processFileName)
    {
        string? name = AppRule.NormalizeFileName(processFileName);
        return name is null
            ? null
            : All.FirstOrDefault(browser => string.Equals(browser.FileName, name, StringComparison.OrdinalIgnoreCase));
    }
}
