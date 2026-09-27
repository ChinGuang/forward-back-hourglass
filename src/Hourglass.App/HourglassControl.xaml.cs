using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
    private static readonly TimeSpan FlipDuration = TimeSpan.FromMilliseconds(500);

    // Levels currently drawn, and the levels frozen on screen while the glass turns over.
    private double _shownUpper = 1;
    private double _shownLower;
    private bool _isFlipping;
    private double _heldUpper;
    private double _heldLower;
    private int _flipGeneration;

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
    /// Turns the glass over. While it turns, the sand it was showing is held (swapped between bulbs, since the
    /// whole drawing is upside down at 180°), so the picture is continuous with what was on screen. Once
    /// upright, it settles to the new run's levels. The view model raises Direction before the new levels, so
    /// the "shown" levels here are still the old ones.
    /// </summary>
    private void PlayFlip()
    {
        _heldUpper = _shownLower;
        _heldLower = _shownUpper;
        _isFlipping = true;

        // Start from wherever the glass is (it may be mid-flip), expressed in (-180, 180] so it turns the short way.
        double from = FlipRotation.Angle - 180;
        if (from <= -180)
        {
            from += 360;
        }

        var flip = new DoubleAnimation(from, 0, FlipDuration * (Math.Abs(from) / 180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        int generation = ++_flipGeneration;
        flip.Completed += (_, _) =>
        {
            if (generation == _flipGeneration)
            {
                _isFlipping = false;
                UpdateSand();
            }
        };

        FlipRotation.BeginAnimation(RotateTransform.AngleProperty, flip);
        UpdateSand();
    }

    private void UpdateSand()
    {
        double upper = Math.Clamp(_isFlipping ? _heldUpper : UpperFill, 0, 1);
        double lower = Math.Clamp(_isFlipping ? _heldLower : LowerFill, 0, 1);
        _shownUpper = upper;
        _shownLower = lower;

        // The bulbs are roughly triangles, so area grows with the square of height. Solving for height keeps
        // the amount of sand you see proportional to the fill level.
        double upperHeight = BulbHeight * Math.Sqrt(upper);
        double lowerHeight = BulbHeight * (1 - Math.Sqrt(1 - lower));

        double upperSurface = Neck - upperHeight;
        double lowerSurface = BottomOfLowerBulb - lowerHeight;

        UpperSandClip.Rect = new Rect(0, upperSurface, 200, upperHeight);
        LowerSandClip.Rect = new Rect(0, lowerSurface, 200, lowerHeight);

        Stream.Y2 = lowerSurface;
        Stream.Visibility = IsFlowing && !_isFlipping && upper > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
