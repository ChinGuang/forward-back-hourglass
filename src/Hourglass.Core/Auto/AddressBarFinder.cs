namespace Hourglass.Core;

public enum UiControlType
{
    Other,
    Edit,
    Document,
}

/// <summary>A node of a window's accessibility (UI Automation) tree.</summary>
public interface IUiElement
{
    UiControlType ControlType { get; }

    string Name { get; }

    /// <summary>The text value (for edit boxes), or null.</summary>
    string? Value { get; }

    IEnumerable<IUiElement> Children { get; }
}

/// <summary>Finds a browser's address bar in its accessibility tree without crawling the web page itself.</summary>
public static class AddressBarFinder
{
    /// <summary>At most this many nodes are visited, so a slow or unusual browser can't stall the watcher.</summary>
    public const int NodeBudget = 500;

    /// <summary>
    /// Breadth-first search, so the toolbar (shallow) is reached before page content (deep). An edit box with a
    /// known address-bar name wins; otherwise the first edit box whose text looks like a URL.
    /// </summary>
    public static IUiElement? Find(IUiElement window, BrowserProfile browser)
    {
        var queue = new Queue<(IUiElement Element, int DocumentDepth)>();
        queue.Enqueue((window, 0));
        IUiElement? urlLookingEdit = null;
        int visited = 0;

        while (queue.Count > 0 && visited < NodeBudget)
        {
            (IUiElement element, int documentDepth) = queue.Dequeue();
            visited++;

            if (element.ControlType == UiControlType.Edit)
            {
                if (KnownBrowsers.AddressBarNames.Any(name => string.Equals(name, element.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return element;
                }

                if (urlLookingEdit is null && BrowserUrl.Parse(element.Value).Kind != BrowserUrlKind.Unreadable)
                {
                    urlLookingEdit = element;
                }

                continue;
            }

            if (element.ControlType == UiControlType.Document)
            {
                // Never enter web pages. Vivaldi's own toolbar is one document deep, so it may enter exactly one.
                int allowed = browser.SearchInsideDocuments ? 1 : 0;
                if (documentDepth >= allowed)
                {
                    continue;
                }

                documentDepth++;
            }

            foreach (IUiElement child in element.Children)
            {
                queue.Enqueue((child, documentDepth));
            }
        }

        return urlLookingEdit;
    }
}
