using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>Lists the apps that currently have a window open, so one can be picked as a tracked app.</summary>
public partial class RunningAppsWindow : Window
{
    public RunningAppsWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            List<RunningApp> apps = await Task.Run(FindRunningApps);
            foreach (RunningApp app in apps)
            {
                app.Icon = LoadIcon(app.Path);
            }

            AppList.ItemsSource = apps;
            LoadingText.Visibility = Visibility.Collapsed;
        };
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

    public sealed record RunningApp(string FileName, string Path, string Title)
    {
        public ImageSource? Icon { get; set; }
    }
}
