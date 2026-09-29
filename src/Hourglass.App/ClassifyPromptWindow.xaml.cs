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

    private readonly Action<RuleAction, double?> _choose;
    private readonly DispatcherTimer _hideTimer;

    public ClassifyPromptWindow(string domain, Action<RuleAction, double?> choose)
    {
        InitializeComponent();
        _choose = choose;
        DomainRun.Text = domain;
        _hideTimer = new DispatcherTimer { Interval = ShowFor };
        _hideTimer.Tick += (_, _) => Close();
        Loaded += (_, _) => PlaceInCorner();
        Closed += (_, _) => _hideTimer.Stop();

        // Don't vanish while you're choosing: the countdown to auto-hide waits while the mouse is over the popup
        // or its speed list is open, and starts over when you move away.
        MouseEnter += (_, _) => _hideTimer.Stop();
        MouseLeave += (_, _) => RestartHideTimerIfIdle();
        _hideTimer.Start();
    }

    private void RestartHideTimerIfIdle()
    {
        if (!IsMouseOver && !SpeedBox.IsDropDownOpen)
        {
            _hideTimer.Stop();
            _hideTimer.Start();
        }
    }

    private void OnSpeedDropDownClosed(object? sender, EventArgs e) => RestartHideTimerIfIdle();

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
        if (Enum.TryParse(((Button)sender).Tag as string, out RuleAction action)
            && SpeedPresets.TryParseRuleSpeed(SpeedBox.SelectedItem as string, out double? speed))
        {
            _choose(action, speed);
        }

        Close();
    }

    private void OnLater(object sender, RoutedEventArgs e) => Close();
}
