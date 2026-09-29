using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Hourglass.Core;

/// <summary>One editable rule row (an app or a website) in the "Apps &amp; sites" window.</summary>
public sealed class RuleItem : INotifyPropertyChanged
{
    private readonly Action _changed;
    private RuleAction _action;
    private double? _speed;
    private string _speedText;
    private bool _hasSpeedError;

    internal RuleItem(string name, RuleAction action, double? speed, Action changed)
    {
        Name = name;
        _action = action;
        _speed = speed;
        _speedText = SpeedPresets.Label(speed);
        _changed = changed;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>"Default" plus the preset multipliers, for the speed box (any positive number can also be typed).</summary>
    public static IReadOnlyList<string> SpeedChoices { get; } =
        [SpeedPresets.DefaultLabel, .. SpeedPresets.All.Select(SpeedPresets.Label)];

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
            Raise(nameof(Action));
            Raise(nameof(IsSpeedEnabled));
            _changed();
        }
    }

    /// <summary>This rule's own multiplier; null uses the main Forward/Backward speed.</summary>
    public double? Speed => _speed;

    /// <summary>A Pause rule doesn't count, so its speed doesn't matter.</summary>
    public bool IsSpeedEnabled => _action != RuleAction.Pause;

    /// <summary>
    /// The speed box's text. Valid input ("Default", "2×", "1.5", "0,25x") is applied and saved at once; anything
    /// else leaves the current speed unchanged and sets <see cref="HasSpeedError"/>.
    /// </summary>
    public string SpeedText
    {
        get => _speedText;
        set
        {
            _speedText = value ?? "";
            bool valid = SpeedPresets.TryParseRuleSpeed(_speedText, out double? speed);
            if (_hasSpeedError == valid)
            {
                _hasSpeedError = !valid;
                Raise(nameof(HasSpeedError));
            }

            if (valid && speed != _speed)
            {
                _speed = speed;
                Raise(nameof(Speed));
                _changed();
            }
        }
    }

    public bool HasSpeedError => _hasSpeedError;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
    /// Adds an app from a path or file name, or updates its action if it's already listed (keeping its speed).
    /// Supported browsers are refused: inside them the website decides, so an app rule would never apply.
    /// </summary>
    public bool AddApp(string pathOrName, RuleAction action) => AddApp(pathOrName, action, keepSpeed: true, speed: null);

    /// <summary>Adds or updates an app with its own speed (null = the main speed).</summary>
    public bool AddApp(string pathOrName, RuleAction action, double? speed) => AddApp(pathOrName, action, keepSpeed: false, speed);

    /// <summary>Adds a website rule (any URL or domain the user typed), or updates its action if it exists (keeping its speed).</summary>
    public bool AddSite(string urlOrDomain, RuleAction action) => AddSite(urlOrDomain, action, keepSpeed: true, speed: null);

    /// <summary>Adds or updates a website rule with its own speed (null = the main speed).</summary>
    public bool AddSite(string urlOrDomain, RuleAction action, double? speed) => AddSite(urlOrDomain, action, keepSpeed: false, speed);

    private bool AddApp(string pathOrName, RuleAction action, bool keepSpeed, double? speed)
    {
        string? name = AppRule.NormalizeFileName(pathOrName);
        if (name is null || !IsValidRule(action, speed) || KnownBrowsers.Find(name) is not null)
        {
            return false;
        }

        Upsert(Apps, name, action, keepSpeed, speed, StringComparer.OrdinalIgnoreCase);
        Changed();
        return true;
    }

    private bool AddSite(string urlOrDomain, RuleAction action, bool keepSpeed, double? speed)
    {
        string? domain = Domains.Normalize(urlOrDomain);
        if (domain is null || !IsValidRule(action, speed))
        {
            return false;
        }

        Upsert(Sites, domain, action, keepSpeed, speed, StringComparer.Ordinal);
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
        Apps.Select(item => new AppRule(item.Name, item.Action, item.Speed)).ToArray(),
        Sites.Select(item => new SiteRule(item.Name, item.Action, item.Speed)).ToArray());

    internal void Load(AutoRules rules)
    {
        _current = null;
        Apps.Clear();
        Sites.Clear();
        foreach (AppRule app in rules.Apps)
        {
            Apps.Add(new RuleItem(app.FileName, app.Action, app.Speed, Changed));
        }

        foreach (SiteRule site in rules.Sites)
        {
            Sites.Add(new RuleItem(site.Domain, site.Action, site.Speed, Changed));
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

    private static bool IsValidRule(RuleAction action, double? speed) =>
        Enum.IsDefined(action) && (speed is null || SpeedPresets.IsValid(speed.Value));

    private void Upsert(
        ObservableCollection<RuleItem> items, string name, RuleAction action, bool keepSpeed, double? speed, StringComparer comparer)
    {
        RuleItem? existing = items.FirstOrDefault(item => comparer.Equals(item.Name, name));
        if (existing is null)
        {
            items.Add(new RuleItem(name, action, speed, Changed));
            return;
        }

        // Replace the row without a second save; the caller saves once.
        int index = items.IndexOf(existing);
        items[index] = new RuleItem(existing.Name, action, keepSpeed ? existing.Speed : speed, Changed);
    }
}
