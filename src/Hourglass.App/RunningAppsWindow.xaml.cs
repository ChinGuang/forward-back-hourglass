using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>Lists the apps that currently have a window open, so one can be picked as a tracked app.</summary>
public partial class RunningAppsWindow : Window
{
    private IReadOnlyList<RunningApp> _addable = [];

    /// <param name="existingRules">The rules already set up; those apps are left out of the list.</param>
    public RunningAppsWindow(AutoRules existingRules)
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            SearchBox.Focus();
            List<RunningApp> running = await Task.Run(FindRunningApps);
            _addable = RunningAppFilter.Addable(running, existingRules);
            foreach (RunningApp app in _addable)
            {
                app.Icon = LoadIcon(app.Path);
            }

            LoadingText.Visibility = Visibility.Collapsed;
            ShowMatches();
        };
    }

    private void ShowMatches()
    {
        IReadOnlyList<RunningApp> matches = RunningAppFilter.Search(_addable, SearchBox.Text);
        AppList.ItemsSource = matches;

        // Select the first match so Enter adds it straight away.
        AppList.SelectedIndex = matches.Count > 0 ? 0 : -1;

        string? message = RunningAppFilter.EmptyMessage(_addable.Count, matches.Count, SearchBox.Text);
        MessageText.Text = message ?? "";
        MessageText.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (LoadingText.Visibility != Visibility.Visible)
        {
            ShowMatches();
        }
    }

    /// <summary>Esc clears the search (a second Esc closes); Up/Down move through the list without leaving the box.</summary>
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape when SearchBox.Text.Length > 0:
                SearchBox.Clear();
                e.Handled = true;
                break;
            case Key.Down when AppList.SelectedIndex < AppList.Items.Count - 1:
                AppList.SelectedIndex++;
                AppList.ScrollIntoView(AppList.SelectedItem);
                e.Handled = true;
                break;
            case Key.Up when AppList.SelectedIndex > 0:
                AppList.SelectedIndex--;
                AppList.ScrollIntoView(AppList.SelectedItem);
                e.Handled = true;
                break;
        }
    }

    /// <summary>The picked app's path (or file name) and action, once the window closes with Add.</summary>
    public (string PathOrName, RuleAction Action)? Choice { get; private set; }

    private static List<RunningApp> FindRunningApps()
    {
        int self = Environment.ProcessId;
        var apps = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == self || process.MainWindowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(process.MainWindowTitle))
                    {
                        continue;
                    }

                    string path = NativeMethods.GetProcessPath((uint)process.Id) ?? process.ProcessName + ".exe";
                    string fileName = Path.GetFileName(path);
                    if (KnownBrowsers.Find(fileName) is not null)
                    {
                        // Inside supported browsers the website decides; see the Websites tab.
                        continue;
                    }

                    apps.TryAdd(fileName, new RunningApp(fileName, path, process.MainWindowTitle));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // The process exited while we looked at it.
                }
            }
        }

        return apps.Values.OrderBy(app => app.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static ImageSource? LoadIcon(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var small = new IntPtr[1];
        if (NativeMethods.ExtractIconEx(path, 0, null, small, 1) == 0 || small[0] == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            BitmapSource icon = Imaging.CreateBitmapSourceFromHIcon(small[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            icon.Freeze();
            return icon;
        }
        finally
        {
            NativeMethods.DestroyIcon(small[0]);
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is RunningApp app && ActionBox.SelectedItem is RuleAction action)
        {
            Choice = (app.Path, action);
            DialogResult = true;
        }
    }

    public sealed record RunningApp(string FileName, string Path, string Title) : IRunningApp
    {
        public ImageSource? Icon { get; set; }
    }
}
