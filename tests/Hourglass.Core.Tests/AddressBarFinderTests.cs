using Hourglass.Core;
using static Hourglass.Core.Tests.FakeUi;

namespace Hourglass.Core.Tests;

public class AddressBarFinderTests
{
    private static readonly BrowserProfile Brave = KnownBrowsers.Find("brave.exe")!;
    private static readonly BrowserProfile Vivaldi = KnownBrowsers.Find("vivaldi.exe")!;

    [Fact]
    public void ChromiumLayout_FindsTheNamedOmnibox_NotAFieldInThePage()
    {
        FakeUi omnibox = Edit("Address and search bar", "youtube.com/watch?v=1");
        FakeUi window = Pane(
            Pane(Pane(Pane(omnibox))),
            Doc(Edit("Search", "https://evil.example/")));

        Assert.Same(omnibox, AddressBarFinder.Find(window, Brave));
    }

    [Fact]
    public void OperaLayout_FindsAddressField()
    {
        FakeUi field = Edit("Address field", "github.com");
        FakeUi window = Pane(Pane(Pane(new FakeUi(UiControlType.Other, "Tab bar")), Pane(field)), Doc());

        Assert.Same(field, AddressBarFinder.Find(window, KnownBrowsers.Find("opera.exe")!));
    }

    [Fact]
    public void LocalizedName_FallsBackToTheFirstUrlLookingEdit()
    {
        FakeUi omnibox = Edit("Adress- und Suchleiste", "youtube.com");
        FakeUi window = Pane(Pane(Edit("Suche", "katzen videos")), Pane(omnibox));

        Assert.Same(omnibox, AddressBarFinder.Find(window, Brave));
    }

    [Fact]
    public void PageContent_IsNeverEntered()
    {
        FakeUi page = Doc(Edit("Address and search bar", "https://evil.example/"));
        FakeUi window = Pane(page);

        Assert.Null(AddressBarFinder.Find(window, Brave));
        Assert.Equal(0, page.ChildReads);
    }

    [Fact]
    public void VivaldiLayout_EntersItsToolbarDocument_ButNotThePageInside()
    {
        FakeUi address = Edit("Search or enter an address", "vivaldi.com/blog");
        FakeUi page = Doc(Edit("Address and search bar", "https://evil.example/"));
        FakeUi window = Pane(Doc(Pane(address), page));

        Assert.Same(address, AddressBarFinder.Find(window, Vivaldi));
        Assert.Equal(0, page.ChildReads);
    }

    [Fact]
    public void HugeTree_StopsAtTheNodeBudget()
    {
        FakeUi[] many = Enumerable.Range(0, AddressBarFinder.NodeBudget * 2).Select(_ => Pane()).ToArray();
        FakeUi window = Pane(Pane(many), Pane(Edit("Address and search bar", "late.example")));

        Assert.Null(AddressBarFinder.Find(window, Brave));
    }

    [Fact]
    public void NoAddressBar_ReturnsNull()
    {
        Assert.Null(AddressBarFinder.Find(Pane(Pane(), Edit("Find in page", "")), Brave));
    }
}
