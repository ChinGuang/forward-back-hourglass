using System.ComponentModel;
using System.Windows;
using Hourglass.Core;

namespace Hourglass.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private AppsWindow? _appsWindow;
    private ClassifyPromptWindow? _prompt;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.AttentionRequested += OnAttentionRequested;
        viewModel.ClassifyRequested += OnClassifyRequested;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _viewModel.Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.AttentionRequested -= OnAttentionRequested;
        _viewModel.ClassifyRequested -= OnClassifyRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _prompt?.Close();
        _appsWindow?.Close();
        _viewModel.SaveState();
        _viewModel.Dispose();
        base.OnClosing(e);
    }

    private void OnOpenApps(object sender, RoutedEventArgs e)
    {
        if (_appsWindow is not null)
        {
            _appsWindow.Activate();
            return;
        }

        _appsWindow = new AppsWindow(_viewModel.Rules) { Owner = this };
        _appsWindow.Closed += (_, _) => _appsWindow = null;
        _appsWindow.Show();
    }

    /// <summary>
    /// A countdown ran out while you were in another app: come to the front and stay on top until the ring is
    /// stopped. (Exclusive full-screen games can't be covered by any window; the ring still plays.)
    /// </summary>
    private void OnAttentionRequested(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Topmost = true;
        Activate();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRinging) && !_viewModel.IsRinging)
        {
            Topmost = false;
        }
    }

    private void OnClassifyRequested(object? sender, string domain)
    {
        _prompt?.Close();
        _prompt = new ClassifyPromptWindow(domain, (action, speed) => _viewModel.Rules.SetSite(domain, action, speed));
        _prompt.Closed += (_, _) => _prompt = null;
        _prompt.Show();
    }
}
