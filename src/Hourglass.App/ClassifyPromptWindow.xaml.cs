using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>
/// "youtube.com isn't classified" popup in the bottom-right corner. It never takes focus, so the browser stays the
/// active window (and the timer keeps following it) even while you click its buttons.
/// </summary>
public partial class ClassifyPromptWindow : Window
{
    private static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(20);

    private readonly Action<RuleAction> _choose;
    private readonly DispatcherTimer _hideTimer;

    public ClassifyPromptWindow(string domain, Action<RuleAction> choose)
    {
        InitializeComponent();
        _choose = choose;
        DomainRun.Text = domain;
        _hideTimer = new DispatcherTimer { Interval = ShowFor };
        _hideTimer.Tick += (_, _) => Close();
        Loaded += (_, _) => PlaceInCorner();
        Closed += (_, _) => _hideTimer.Stop();
        _hideTimer.Start();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeMethods.MakeNonActivating(new WindowInteropHelper(this).Handle);
    }

    private void PlaceInCorner()
    {
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 16;
        Top = workArea.Bottom - ActualHeight - 16;
    }

    private void OnChoose(object sender, RoutedEventArgs e)
    {
        if (Enum.TryParse(((Button)sender).Tag as string, out RuleAction action))
        {
            _choose(action);
        }

        Close();
    }

    private void OnLater(object sender, RoutedEventArgs e) => Close();
}
