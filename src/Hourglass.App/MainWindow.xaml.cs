using System.ComponentModel;
using System.Windows;
using Hourglass.Core;

namespace Hourglass.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.SaveState();
        _viewModel.Dispose();
        base.OnClosing(e);
    }
}
