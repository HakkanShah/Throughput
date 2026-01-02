using System.Windows;
using System.Windows.Forms;

namespace Throughput.Helpers;

/// <summary>
/// Helper class for multi-monitor safe window positioning
/// </summary>
public static class PositionHelper
{
    /// <summary>
    /// Checks if a position is visible on any screen
    /// </summary>
    /// <param name="left">Window left position</param>
    /// <param name="top">Window top position</param>
    /// <param name="width">Window width</param>
    /// <param name="height">Window height</param>
    /// <returns>True if at least 50% of the window would be visible</returns>
    public static bool IsPositionVisible(double left, double top, double width, double height)
    {
        // Check if the center point of the window is on any screen
        var centerX = left + (width / 2);
        var centerY = top + (height / 2);

        foreach (var screen in Screen.AllScreens)
        {
            if (screen.WorkingArea.Contains((int)centerX, (int)centerY))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clamps position to the nearest visible screen area
    /// </summary>
    /// <param name="left">Desired left position</param>
    /// <param name="top">Desired top position</param>
    /// <param name="width">Window width</param>
    /// <param name="height">Window height</param>
    /// <returns>Clamped position that ensures visibility</returns>
    public static (double left, double top) ClampToScreen(double left, double top, double width, double height)
    {
        // Find the screen that the center point would be closest to
        var centerX = left + (width / 2);
        var centerY = top + (height / 2);

        Screen? closestScreen = null;
        double minDistance = double.MaxValue;

        foreach (var screen in Screen.AllScreens)
        {
            // Check if center is on this screen
            if (screen.WorkingArea.Contains((int)centerX, (int)centerY))
            {
                closestScreen = screen;
                break;
            }

            // Calculate distance to screen center
            var screenCenterX = screen.WorkingArea.X + (screen.WorkingArea.Width / 2);
            var screenCenterY = screen.WorkingArea.Y + (screen.WorkingArea.Height / 2);
            var distance = Math.Sqrt(Math.Pow(centerX - screenCenterX, 2) + Math.Pow(centerY - screenCenterY, 2));

            if (distance < minDistance)
            {
                minDistance = distance;
                closestScreen = screen;
            }
        }

        // Default to primary screen if no screens found (shouldn't happen)
        closestScreen ??= Screen.PrimaryScreen ?? Screen.AllScreens[0];

        var workArea = closestScreen.WorkingArea;

        // Clamp position to keep window fully visible within the work area
        var clampedLeft = Math.Max(workArea.Left, Math.Min(left, workArea.Right - width));
        var clampedTop = Math.Max(workArea.Top, Math.Min(top, workArea.Bottom - height));

        return (clampedLeft, clampedTop);
    }

    /// <summary>
    /// Gets the default position for a widget (bottom-right corner of primary work area)
    /// </summary>
    /// <param name="width">Widget width</param>
    /// <param name="height">Widget height</param>
    /// <returns>Default position</returns>
    public static (double left, double top) GetDefaultPosition(double width, double height)
    {
        var workArea = SystemParameters.WorkArea;
        return (workArea.Right - width - 10, workArea.Bottom - height - 10);
    }
}
