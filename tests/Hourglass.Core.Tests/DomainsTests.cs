using Hourglass.Core;

namespace Hourglass.Core.Tests;

public class DomainsTests
{
    [Theory]
    [InlineData("youtube.com", "youtube.com")]
    [InlineData("https://www.YouTube.com/watch?v=1", "youtube.com")]
    [InlineData("*.youtube.com", "youtube.com")]
    [InlineData(" music.youtube.com ", "music.youtube.com")]
    [InlineData("www.bbc.co.uk", "bbc.co.uk")]
    [InlineData("localhost", "localhost")]
    [InlineData("www.localhost", "localhost")]
    [InlineData("192.168.1.10", "192.168.1.10")]
    [InlineData("t.co", "t.co")]
    public void Normalize_AcceptsUrlsAndDomains(string input, string expected)
    {
        Assert.Equal(expected, Domains.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a domain")]
    [InlineData("brave://newtab")]
    [InlineData("word")]
    [InlineData("www.com")]      // would become "com" and match every .com site
    [InlineData("www.co.uk")]    // would become "co.uk"
    [InlineData("com.au")]
    public void Normalize_RejectsNonDomains(string? input)
    {
        Assert.Null(Domains.Normalize(input));
    }

    [Theory]
    [InlineData("youtube.com", "youtube.com", true)]
    [InlineData("www.youtube.com", "youtube.com", true)]
    [InlineData("m.YouTube.com", "youtube.com", true)]
    [InlineData("notyoutube.com", "youtube.com", false)]
    [InlineData("youtube.com.evil.io", "youtube.com", false)]
    [InlineData("youtube.com", "music.youtube.com", false)]
    public void IsSameOrSubdomain(string host, string domain, bool expected)
    {
        Assert.Equal(expected, Domains.IsSameOrSubdomain(host, domain));
    }

    [Theory]
    [InlineData("www.youtube.com", "youtube.com")]
    [InlineData("m.youtube.com", "youtube.com")]
    [InlineData("youtube.com", "youtube.com")]
    [InlineData("news.bbc.co.uk", "bbc.co.uk")]
    [InlineData("abc.net.au", "abc.net.au")]
    [InlineData("localhost", "localhost")]
    [InlineData("192.168.1.10", "192.168.1.10")]
    public void Suggest_OffersTheSiteWithoutSubdomains(string host, string expected)
    {
        Assert.Equal(expected, Domains.Suggest(host));
    }
}
