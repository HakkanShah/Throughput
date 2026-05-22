using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Color = System.Windows.Media.Color;
using Rectangle = System.Windows.Shapes.Rectangle;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Throughput.Helpers;

/// <summary>
/// Severity level for resource-usage warnings, driven by CPU/RAM thresholds.
/// </summary>
public enum WarningLevel
{
    /// <summary>Usage is below the warning threshold.</summary>
    Normal,

    /// <summary>Usage has crossed 75% - caution.</summary>
    Warning,

    /// <summary>Usage has crossed 90% - critical.</summary>
    Critical
}

/// <summary>
/// Drives an animated "snake" outline around a widget: two bright segments chase
/// around opposite sides of the border. The colour reflects severity (yellow at
/// 75%, red at 90%) and the animation runs continuously while usage stays high,
/// clearing once usage drops back below the threshold.
/// </summary>
public sealed class WarningGlow
{
    /// <summary>Usage percentage at or above which the yellow warning shows.</summary>
    public const double WarningThreshold = 75.0;

    /// <summary>Usage percentage at or above which the red critical warning shows.</summary>
    public const double CriticalThreshold = 90.0;

    private static readonly Color WarningColor = Color.FromRgb(0xFA, 0xCC, 0x15);  // amber
    private static readonly Color CriticalColor = Color.FromRgb(0xEF, 0x44, 0x44); // red

    private readonly Rectangle _outline;
    private readonly DropShadowEffect _glow;
    private readonly System.Windows.FrameworkElement _sizeSource;

    // Perimeter the dash pattern is built for; the running animation is rebuilt
    // if the widget is resized so the segments stay correctly proportioned.
    private double _builtForPerimeter;

    private WarningLevel _currentLevel = WarningLevel.Normal;

    /// <summary>
    /// The active warning level, derived from the most recent usage update.
    /// </summary>
    public WarningLevel CurrentLevel => _currentLevel;

    /// <param name="outline">The overlay Rectangle drawn on top of the widget.</param>
    /// <param name="glow">The DropShadowEffect on that rectangle (its glow halo).</param>
    /// <param name="sizeSource">
    /// The element whose measured size defines the outline perimeter - use the
    /// widget's main Border, which is always measured (the Rectangle's own size
    /// arrives later via binding).
    /// </param>
    public WarningGlow(Rectangle outline, DropShadowEffect glow, System.Windows.FrameworkElement sizeSource)
    {
        _outline = outline;
        _glow = glow;
        _sizeSource = sizeSource;
    }

    /// <summary>
    /// Maps a usage percentage to its warning level.
    /// </summary>
    public static WarningLevel LevelFor(double usagePercent)
    {
        if (usagePercent >= CriticalThreshold) return WarningLevel.Critical;
        if (usagePercent >= WarningThreshold) return WarningLevel.Warning;
        return WarningLevel.Normal;
    }

    private static readonly Color NormalTextColor = Color.FromRgb(0xFF, 0xFF, 0xFF);

    // Cached, frozen brushes - reused across every tick to avoid per-tick allocations.
    // Frozen brushes are also cheaper for WPF to render.
    private static readonly SolidColorBrush NormalBrush = MakeFrozen(NormalTextColor);
    private static readonly SolidColorBrush WarningBrush = MakeFrozen(WarningColor);
    private static readonly SolidColorBrush CriticalBrush = MakeFrozen(CriticalColor);

    private static SolidColorBrush MakeFrozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    /// <summary>
    /// Styles a single percentage readout to reflect its own usage value:
    /// white below 75%, glowing yellow at 75%+, glowing red at 90%+.
    /// Each metric (CPU, RAM) is evaluated independently.
    /// </summary>
    /// <param name="text">The percentage TextBlock.</param>
    /// <param name="textGlow">The DropShadowEffect attached to that TextBlock.</param>
    /// <param name="usagePercent">The current usage value for this metric.</param>
    public static void StylePercentage(TextBlock text, DropShadowEffect textGlow, double usagePercent)
    {
        var level = LevelFor(usagePercent);
        Color color = level switch
        {
            WarningLevel.Critical => CriticalColor,
            WarningLevel.Warning => WarningColor,
            _ => NormalTextColor
        };

        // Cached frozen brush - no per-tick allocation
        var brush = level switch
        {
            WarningLevel.Critical => CriticalBrush,
            WarningLevel.Warning => WarningBrush,
            _ => NormalBrush
        };
        if (text.Foreground != brush) text.Foreground = brush;

        if (level == WarningLevel.Normal)
        {
            // No glow below the warning threshold.
            textGlow.Color = WarningColor;
            textGlow.BlurRadius = 0;
            textGlow.Opacity = 0;
        }
        else
        {
            // Critical glows brighter/larger than the warning state.
            textGlow.Color = color;
            textGlow.BlurRadius = level == WarningLevel.Critical ? 14 : 9;
            textGlow.Opacity = level == WarningLevel.Critical ? 1.0 : 0.85;
        }
    }

    /// <summary>
    /// Updates the outline based on current CPU and memory usage. The higher of the
    /// two values decides the level. The animation only restarts when the level
    /// changes (or the widget size changes), so it runs smoothly tick to tick.
    /// </summary>
    public void Update(double cpuPercent, double memoryPercent)
    {
        var level = LevelFor(Math.Max(cpuPercent, memoryPercent));

        // The snake needs the widget's measured size. If the level wants a snake
        // but layout isn't ready yet (perimeter 0), retry on the next tick.
        bool needsSnake = level != WarningLevel.Normal;
        bool notYetBuilt = needsSnake && _builtForPerimeter <= 0;
        bool sizeChanged = needsSnake && _builtForPerimeter > 0
                           && Math.Abs(CurrentPerimeter() - _builtForPerimeter) > 1.0;

        if (level == _currentLevel && !sizeChanged && !notYetBuilt) return;

        _currentLevel = level;
        Apply(level);
    }

    /// <summary>
    /// Applies the visual state for the given level.
    /// </summary>
    private void Apply(WarningLevel level)
    {
        switch (level)
        {
            case WarningLevel.Warning:
                StartSnake(WarningColor, periodMs: 2200);
                break;
            case WarningLevel.Critical:
                // Faster travel + brighter halo conveys urgency
                StartSnake(CriticalColor, periodMs: 1200);
                break;
            default:
                StopSnake();
                break;
        }
    }

    /// <summary>
    /// Current outline perimeter in device-independent pixels.
    /// </summary>
    private double CurrentPerimeter()
    {
        double w = _sizeSource.ActualWidth;
        double h = _sizeSource.ActualHeight;
        return w > 0 && h > 0 ? 2 * (w + h) : 0;
    }

    /// <summary>
    /// Starts the dual chasing-segment animation in the given colour.
    /// </summary>
    /// <param name="color">Outline / glow colour.</param>
    /// <param name="periodMs">Time for a segment to travel the full loop once.</param>
    private void StartSnake(Color color, double periodMs)
    {
        double perimeter = CurrentPerimeter();
        if (perimeter <= 0)
        {
            // Widget not measured yet. Keep _builtForPerimeter at 0 so the next
            // Update() call retries (notYetBuilt stays true). Level is unchanged.
            _builtForPerimeter = 0;
            _outline.Stroke = new SolidColorBrush(color);
            return;
        }

        _builtForPerimeter = perimeter;

        var brush = new SolidColorBrush(color);
        _outline.Stroke = brush;
        _outline.Visibility = System.Windows.Visibility.Visible;

        _glow.BeginAnimation(DropShadowEffect.ColorProperty, null);
        _glow.Color = color;

        // StrokeDashArray is expressed in multiples of StrokeThickness.
        // Build two equal dashes on opposite sides: dash, gap, dash, gap.
        double thickness = Math.Max(0.1, _outline.StrokeThickness);
        double unitPerimeter = perimeter / thickness;
        double dash = unitPerimeter * 0.16;          // each visible segment ~16% of loop
        double gap = unitPerimeter * 0.5 - dash;     // remainder, split so the two
                                                     // segments sit on opposite sides
        _outline.StrokeDashArray = new DoubleCollection { dash, gap, dash, gap };
        _outline.StrokeDashCap = PenLineCap.Round;

        // Animate the dash offset to make the segments travel around the border.
        var travel = new DoubleAnimation
        {
            From = 0,
            To = -unitPerimeter,        // negative = clockwise travel
            Duration = TimeSpan.FromMilliseconds(periodMs),
            RepeatBehavior = RepeatBehavior.Forever
        };
        _outline.BeginAnimation(Shape.StrokeDashOffsetProperty, travel);

        // Subtle halo pulse so the glow feels alive.
        var halo = new DoubleAnimation
        {
            From = 8,
            To = 16,
            Duration = TimeSpan.FromMilliseconds(periodMs / 2),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        _glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, halo);
    }

    /// <summary>
    /// Stops the snake animation and hides the outline.
    /// </summary>
    private void StopSnake()
    {
        _outline.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        _glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);

        _outline.Visibility = System.Windows.Visibility.Collapsed;
        _builtForPerimeter = 0;
    }
}
