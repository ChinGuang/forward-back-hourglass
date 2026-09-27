using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Hourglass.Core;

namespace Hourglass.App;

/// <summary>Draws the hourglass. Fill levels come from <see cref="SandLevel"/> via the view model.</summary>
public partial class HourglassControl : UserControl
{
    public static readonly DependencyProperty UpperFillProperty = DependencyProperty.Register(
        nameof(UpperFill), typeof(double), typeof(HourglassControl), new PropertyMetadata(1.0, OnLevelChanged));

    public static readonly DependencyProperty LowerFillProperty = DependencyProperty.Register(
        nameof(LowerFill), typeof(double), typeof(HourglassControl), new PropertyMetadata(0.0, OnLevelChanged));

    public static readonly DependencyProperty IsFlowingProperty = DependencyProperty.Register(
        nameof(IsFlowing), typeof(bool), typeof(HourglassControl), new PropertyMetadata(false, OnLevelChanged));

    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction), typeof(TimerDirection), typeof(HourglassControl), new PropertyMetadata(TimerDirection.None, OnDirectionChanged));

    // Geometry of the 200x300 drawing (see HourglassControl.xaml).
    private const double TopOfUpperBulb = 20;
    private const double Neck = 150;
    private const double BottomOfLowerBulb = 280;
    private const double BulbHeight = Neck - TopOfUpperBulb;

    public HourglassControl()
    {
        InitializeComponent();
        UpdateSand();
    }

    public double UpperFill
    {
        get => (double)GetValue(UpperFillProperty);
        set => SetValue(UpperFillProperty, value);
    }

    public double LowerFill
    {
        get => (double)GetValue(LowerFillProperty);
        set => SetValue(LowerFillProperty, value);
    }

    public bool IsFlowing
    {
        get => (bool)GetValue(IsFlowingProperty);
        set => SetValue(IsFlowingProperty, value);
    }

    public TimerDirection Direction
    {
        get => (TimerDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HourglassControl)d).UpdateSand();

    private static void OnDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var oldDirection = (TimerDirection)e.OldValue;
        var newDirection = (TimerDirection)e.NewValue;
        if (oldDirection != TimerDirection.None && newDirection != TimerDirection.None && oldDirection != newDirection)
        {
            ((HourglassControl)d).PlayFlip();
        }
    }

    /// <summary>
    /// Turns the glass over. The new levels are drawn upside down at 180° (so the sand starts where it just was)
    /// and rotate upright, which reads as a physical flip.
    /// </summary>
    private void PlayFlip()
    {
        var flip = new DoubleAnimation(180, 0, TimeSpan.FromMilliseconds(500))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        FlipRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, flip);
    }

    private void UpdateSand()
    {
        double upper = Math.Clamp(UpperFill, 0, 1);
        double lower = Math.Clamp(LowerFill, 0, 1);

        // The bulbs are roughly triangles, so area grows with the square of height. Solving for height keeps
        // the amount of sand you see proportional to the fill level.
        double upperHeight = BulbHeight * Math.Sqrt(upper);
        double lowerHeight = BulbHeight * (1 - Math.Sqrt(1 - lower));

        double upperSurface = Neck - upperHeight;
        double lowerSurface = BottomOfLowerBulb - lowerHeight;

        UpperSandClip.Rect = new Rect(0, upperSurface, 200, upperHeight);
        LowerSandClip.Rect = new Rect(0, lowerSurface, 200, lowerHeight);

        Stream.Y2 = lowerSurface;
        Stream.Visibility = IsFlowing && upper > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
