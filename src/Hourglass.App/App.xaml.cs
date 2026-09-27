using System.Windows;
using Hourglass.Core;

namespace Hourglass.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new MainViewModel(
            new DispatcherTicker(),
            new SoundPlayerAlarm(),
            new JsonSettingsStore(JsonSettingsStore.DefaultPath));

        MainWindow = new MainWindow(viewModel);
        MainWindow.Show();
    }
}
