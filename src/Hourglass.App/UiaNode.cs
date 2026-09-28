using System.Windows.Automation;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>Adapts a UI Automation element to the Core's <see cref="IUiElement"/> so the address-bar search is testable.</summary>
internal sealed class UiaNode(AutomationElement element) : IUiElement
{
    public AutomationElement Element { get; } = element;

    public UiControlType ControlType
    {
        get
        {
            System.Windows.Automation.ControlType type = Element.Current.ControlType;
            return type == System.Windows.Automation.ControlType.Edit ? UiControlType.Edit
                : type == System.Windows.Automation.ControlType.Document ? UiControlType.Document
                : UiControlType.Other;
        }
    }

    public string Name => Element.Current.Name ?? "";

    public string? Value => ReadValue(Element);

    public IEnumerable<IUiElement> Children
    {
        get
        {
            TreeWalker walker = TreeWalker.ControlViewWalker;
            AutomationElement? child = walker.GetFirstChild(Element);
            while (child is not null)
            {
                yield return new UiaNode(child);
                child = walker.GetNextSibling(child);
            }
        }
    }

    public static string? ReadValue(AutomationElement element) =>
        element.TryGetCurrentPattern(ValuePattern.Pattern, out object pattern)
            ? ((ValuePattern)pattern).Current.Value
            : null;
}
