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
    /// The widget type to show on startup
    /// </summary>
    public WidgetType DefaultWidgetType { get; set; } = WidgetType.Full;

    /// <summary>
    /// Stored widget positions by type name
    /// </summary>
    public Dictionary<string, WidgetPosition> WidgetPositions { get; set; } = new();

    /// <summary>
    /// Gets the saved position for a widget type
    /// </summary>
    /// <param name="widgetType">The widget type</param>
    /// <returns>The saved position, or null if not set</returns>
    public WidgetPosition? GetWidgetPosition(WidgetType widgetType)
    {
        var key = widgetType.ToString();
        return WidgetPositions.TryGetValue(key, out var position) && position.HasBeenSet 
            ? position 
            : null;
    }

    /// <summary>
    /// Saves the position for a widget type
    /// </summary>
    /// <param name="widgetType">The widget type</param>
    /// <param name="left">Left position</param>
    /// <param name="top">Top position</param>
    public void SaveWidgetPosition(WidgetType widgetType, double left, double top)
    {
        var key = widgetType.ToString();
        WidgetPositions[key] = new WidgetPosition
        {
            Left = left,
            Top = top,
            HasBeenSet = true
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

