using System.Reflection;

namespace Throughput.Helpers;

/// <summary>
/// Single source of truth for the running app's version, driven by the csproj
/// <c>&lt;Version&gt;</c> property. Used by the dashboard footer and the updater.
/// </summary>
internal static class AppInfo
{
    /// <summary>The running assembly version (e.g. 3.0.1).</summary>
    public static Version Current { get; } =
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>Display form for UI, e.g. "v3.0.1".</summary>
    public static string DisplayVersion => $"v{Current.Major}.{Current.Minor}.{Current.Build}";
}
