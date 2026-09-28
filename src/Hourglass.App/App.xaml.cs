using System.Windows;
using Hourglass.Core;

namespace Hourglass.App;

public partial class App : Application
{
    private MainViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _viewModel = new MainViewModel(
            new DispatcherTicker(),
            new SoundPlayerAlarm(),
            new JsonSettingsStore(JsonSettingsStore.DefaultPath));

        MainWindow = new MainWindow(_viewModel);
        MainWindow.Show();
    }

    /// <summary>
    /// Windows is logging off or shutting down. WPF does not raise the window's Closing event in this case,
    /// so save here too or the timer would be lost.
    /// </summary>
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _viewModel?.SaveState();
        base.OnSessionEnding(e);
    }
}
