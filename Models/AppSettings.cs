using System.IO;
using System.Text.Json;

namespace Throughput.Models;

/// <summary>
/// Represents a widget's screen position
/// </summary>
public class WidgetPosition
{
    public double Left { get; set; }
    public double Top { get; set; }
    public bool HasBeenSet { get; set; }
}

/// <summary>
/// Represents a widget's saved size (width and height in DIPs).
/// </summary>
public class WidgetSize
{
    public double Width { get; set; }
    public double Height { get; set; }
    public bool HasBeenSet { get; set; }
}

/// <summary>
/// User-configurable settings for the border warning-glow animation.
/// </summary>
public class AnimationSettings
{
    public bool Enabled { get; set; } = true;
    public double WarningThreshold { get; set; } = 75.0;
    public double CriticalThreshold { get; set; } = 90.0;
}

/// <summary>
/// Application settings with JSON persistence
/// </summary>
public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Throughput",
        "settings.json"
    );

    /// <summary>
    /// The saved on-screen position of the widget
    /// </summary>
    public WidgetPosition WidgetPosition { get; set; } = new();

    /// <summary>
    /// The saved size of the widget (set when the user resizes it)
    /// </summary>
    public WidgetSize WidgetSize { get; set; } = new();

    /// <summary>
    /// The user's configuration for the border warning-glow animation
    /// </summary>
    public AnimationSettings Animation { get; set; } = new();

    /// <summary>
    /// Gets the saved widget position, or null if it has never been set
    /// </summary>
    public WidgetPosition? GetWidgetPosition()
    {
        return WidgetPosition.HasBeenSet ? WidgetPosition : null;
    }

    /// <summary>
    /// Saves the widget's on-screen position
    /// </summary>
    /// <param name="left">Left position</param>
    /// <param name="top">Top position</param>
    public void SaveWidgetPosition(double left, double top)
    {
        WidgetPosition = new WidgetPosition
        {
            Left = left,
            Top = top,
            HasBeenSet = true
        };
        Save();
    }

    /// <summary>
    /// Gets the saved widget size, or null if the user has never resized it.
    /// </summary>
    public WidgetSize? GetWidgetSize()
    {
        return WidgetSize.HasBeenSet ? WidgetSize : null;
    }

    /// <summary>
    /// Saves the widget's user-chosen size.
    /// </summary>
    public void SaveWidgetSize(double width, double height)
    {
        WidgetSize = new WidgetSize
        {
            Width = width,
            Height = height,
            HasBeenSet = true
        };
        Save();
    }

    /// <summary>
    /// Saves the user's animation preferences. Thresholds are clamped so the
    /// warning threshold stays below the critical threshold.
    /// </summary>
    public void SaveAnimationSettings(bool enabled, double warningThreshold, double criticalThreshold)
    {
        double clampedWarning = Math.Clamp(warningThreshold, 1, 99);
        double clampedCritical = Math.Clamp(criticalThreshold, clampedWarning + 1, 100);

        Animation = new AnimationSettings
        {
            Enabled = enabled,
            WarningThreshold = clampedWarning,
            CriticalThreshold = clampedCritical
        };
        Save();
    }

    /// <summary>
    /// Loads settings from disk, or returns defaults if not found
    /// </summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        
        return new AppSettings();
    }

    /// <summary>
    /// Saves settings to disk
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}

