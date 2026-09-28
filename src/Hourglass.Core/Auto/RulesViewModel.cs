using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Hourglass.Core;

/// <summary>One editable rule row (an app or a website) in the "Apps &amp; sites" window.</summary>
public sealed class RuleItem : INotifyPropertyChanged
{
    private readonly Action _changed;
    private RuleAction _action;

    internal RuleItem(string name, RuleAction action, Action changed)
    {
        Name = name;
        _action = action;
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public RuleAction Action
    {
        get => _action;
        set
        {
            if (_action == value || !Enum.IsDefined(value))
            {
                return;
            }

            _action = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Action)));
            _changed();
        }
    }
}

/// <summary>The tracked apps and websites, editable from the UI. Every change is saved and applied at once.</summary>
public sealed class RulesViewModel
{
    private readonly Action _changed;
    private AutoRules? _current;

    internal RulesViewModel(Action changed)
    {
        _changed = changed;
    }

    public static IReadOnlyList<RuleAction> ActionChoices { get; } = [RuleAction.Forward, RuleAction.Pause, RuleAction.Backward];

    public ObservableCollection<RuleItem> Apps { get; } = [];

    public ObservableCollection<RuleItem> Sites { get; } = [];

    /// <summary>Websites seen this session that have no rule yet (they count backward until classified).</summary>
    /// <remarks>The single list of unclassified sites; auto mode adds to it, classifying removes from it.</remarks>
    public ObservableCollection<string> Unclassified { get; } = [];

    /// <summary>
    /// Adds an app from a path or file name, or updates its action if it's already listed. Supported browsers are
    /// refused: inside them the website decides, so an app rule would never apply.
    /// </summary>
    public bool AddApp(string pathOrName, RuleAction action)
    {
        string? name = AppRule.NormalizeFileName(pathOrName);
        if (name is null || !Enum.IsDefined(action) || KnownBrowsers.Find(name) is not null)
        {
            return false;
        }

        Upsert(Apps, name, action, StringComparer.OrdinalIgnoreCase);
        Changed();
        return true;
    }

    /// <summary>Adds a website rule (any URL or domain the user typed), or updates it if it exists.</summary>
    public bool AddSite(string urlOrDomain, RuleAction action)
    {
        string? domain = Domains.Normalize(urlOrDomain);
        if (domain is null || !Enum.IsDefined(action))
        {
            return false;
        }

        Upsert(Sites, domain, action, StringComparer.Ordinal);
        foreach (string seen in Unclassified.Where(seen => Domains.IsSameOrSubdomain(seen, domain)).ToList())
        {
            Unclassified.Remove(seen);
        }

        Changed();
        return true;
    }

    public void Remove(RuleItem item)
    {
        if (Apps.Remove(item) || Sites.Remove(item))
        {
            Changed();
        }
    }

    /// <summary>The rules as a snapshot; rebuilt only after an edit, since auto mode reads it on every window change.</summary>
    public AutoRules ToRules() => _current ??= new AutoRules(
        Apps.Select(item => new AppRule(item.Name, item.Action)).ToArray(),
        Sites.Select(item => new SiteRule(item.Name, item.Action)).ToArray());

    internal void Load(AutoRules rules)
    {
        _current = null;
        Apps.Clear();
        Sites.Clear();
        foreach (AppRule app in rules.Apps)
        {
            Apps.Add(new RuleItem(app.FileName, app.Action, Changed));
        }

        foreach (SiteRule site in rules.Sites)
        {
            Sites.Add(new RuleItem(site.Domain, site.Action, Changed));
        }
    }

    internal void NoteUnclassified(string domain)
    {
        if (!Unclassified.Contains(domain))
        {
            Unclassified.Add(domain);
        }
    }

    private void Changed()
    {
        _current = null;
        _changed();
    }

    private void Upsert(ObservableCollection<RuleItem> items, string name, RuleAction action, StringComparer comparer)
    {
        RuleItem? existing = items.FirstOrDefault(item => comparer.Equals(item.Name, name));
        if (existing is null)
        {
            items.Add(new RuleItem(name, action, Changed));
            return;
        }

        // Update without a second save; the caller saves once.
        int index = items.IndexOf(existing);
        items[index] = new RuleItem(existing.Name, action, Changed);
    }
}
