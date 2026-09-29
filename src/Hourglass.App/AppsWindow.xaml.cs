using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hourglass.Core;
using Microsoft.Win32;

namespace Hourglass.App;

/// <summary>Edits the tracked apps and websites. Changes are saved and applied immediately.</summary>
public partial class AppsWindow : Window
{
    private readonly RulesViewModel _rules;

    public AppsWindow(RulesViewModel rules)
    {
        InitializeComponent();
        _rules = rules;
        DataContext = rules;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // A speed still being typed counts: apply it before the window goes.
        if (Keyboard.FocusedElement is DependencyObject focused && FindComboBox(focused) is { } box)
        {
            CommitSpeed(box);
        }

        base.OnClosing(e);
    }

    private static ComboBox? FindComboBox(DependencyObject? element)
    {
        while (element is not null and not ComboBox)
        {
            element = VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }

        return element as ComboBox;
    }

    private static void CommitSpeed(ComboBox box) =>
        box.GetBindingExpression(ComboBox.TextProperty)?.UpdateSource();

    private void OnSpeedKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitSpeed((ComboBox)sender);
            e.Handled = true;
        }
    }

    private void OnSpeedPicked(object? sender, EventArgs e) => CommitSpeed((ComboBox)sender!);

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is RuleItem item)
        {
            _rules.Remove(item);
        }
    }

    private void OnAddRunningApp(object sender, RoutedEventArgs e)
    {
        var picker = new RunningAppsWindow { Owner = this };
        if (picker.ShowDialog() == true && picker.Choice is { } choice)
        {
            AddApp(choice.PathOrName, choice.Action);
        }
    }

    private void OnBrowseApp(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose a program to track",
            Filter = "Programs (*.exe)|*.exe",
        };

        if (dialog.ShowDialog(this) == true)
        {
            // New apps start as Forward; change it in the list.
            AddApp(dialog.FileName, RuleAction.Forward);
        }
    }

    private void AddApp(string pathOrName, RuleAction action)
    {
        if (KnownBrowsers.Find(pathOrName) is { } browser)
        {
            MessageBox.Show(
                this,
                $"{browser.DisplayName} is followed by website instead: add the sites you use on the Websites tab.",
                "Browsers are handled by website",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _rules.AddApp(pathOrName, action);
    }

    private void OnAddSite(object sender, RoutedEventArgs e)
    {
        bool added = NewSiteAction.SelectedItem is RuleAction action && _rules.AddSite(NewSiteText.Text, action);
        SiteError.Visibility = added ? Visibility.Collapsed : Visibility.Visible;
        if (added)
        {
            NewSiteText.Clear();
        }
    }

    private void OnClassify(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (button.DataContext is string domain && Enum.TryParse(button.Tag as string, out RuleAction action))
        {
            _rules.AddSite(domain, action);
        }
    }
}
