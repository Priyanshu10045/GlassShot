using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace GlassShot;

internal static class DpiInitializer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    [ModuleInitializer]
    internal static void Init()
    {
        try
        {
            // This guarantees DPI awareness is set before the WPF Application is even initialized.
            // Bypasses the dotnet run host virtualization bugs entirely.
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch { }
    }
}
