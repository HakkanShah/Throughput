namespace Throughput.Models;

/// <summary>
/// Enum representing available widget types
/// </summary>
public enum WidgetType
{
    /// <summary>
    /// Full widget with header, download/upload speeds, and "More Details" link
    /// </summary>
    Full,

    /// <summary>
    /// Compact widget with download + upload speeds only, transparent, close on hover
    /// </summary>
    Compact,

    /// <summary>
    /// Minimal widget with download speed only, system-tray-icon sized
    /// </summary>
    Minimal,

    /// <summary>
    /// Speed test focused widget
    /// </summary>
    SpeedTest
}
