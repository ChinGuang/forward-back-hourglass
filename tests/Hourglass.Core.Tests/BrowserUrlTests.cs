using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class BrowserUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=1", "www.youtube.com")]
    [InlineData("youtube.com/watch?v=1", "youtube.com")]          // Chromium hides the scheme
    [InlineData("HTTP://Example.COM/Path", "example.com")]
    [InlineData("github.com", "github.com")]
    [InlineData("localhost:3000/app", "localhost")]
    [InlineData("192.168.1.10/admin", "192.168.1.10")]
    [InlineData("https://bücher.de", "xn--bcher-kva.de")]           // stored in ASCII form
    [InlineData("example.com.", "example.com")]
    [InlineData("web.archive.org/web/2020/https://example.com", "web.archive.org")]   // "://" inside the path
    [InlineData("google.com/url?q=https://x.com", "google.com")]
    public void Sites(string text, string host)
    {
        Assert.Equal(new BrowserUrl(BrowserUrlKind.Site, host), BrowserUrl.Parse(text));
    }

    [Theory]
    [InlineData("brave://newtab")]
    [InlineData("chrome://settings")]
    [InlineData("opera://startpage")]
    [InlineData("vivaldi://settings")]
    [InlineData("edge://newtab")]
    [InlineData("about:blank")]
    [InlineData("file:///C:/notes.html")]
    [InlineData("view-source:https://example.com")]
    [InlineData("chrome-extension://abc/popup.html")]
    public void InternalPages(string text)
    {
        Assert.Equal(BrowserUrlKind.Internal, BrowserUrl.Parse(text).Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("how to bake bread")]   // a search being typed
    [InlineData("bread")]               // a single word isn't a host
    public void Unreadable(string? text)
    {
        Assert.Equal(BrowserUrlKind.Unreadable, BrowserUrl.Parse(text).Kind);
    }
}
