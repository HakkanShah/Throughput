using System.Runtime.InteropServices;

namespace Throughput.Helpers;

/// <summary>
/// Releases the process's unused resident pages back to the OS. The pages fault
/// back in on demand, so this is safe to call whenever the app goes idle (after
/// startup, after the dashboard closes) to keep the reported working set small.
/// </summary>
internal static class MemoryTrimmer
{
    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("psapi.dll")]
    private static extern bool EmptyWorkingSet(nint hProcess);

    public static void Trim()
    {
        try
        {
            EmptyWorkingSet(GetCurrentProcess());
        }
        catch
        {
            // Best-effort only; never let a trim failure surface to the user.
        }
    }
}
