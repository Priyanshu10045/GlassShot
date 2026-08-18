using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GlassShot;

public static class DesktopIconManager
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_STYLE = -16;
    private const int WS_VISIBLE = 0x10000000;
    private const uint WM_COMMAND = 0x111;

    public static IntPtr GetDesktopListViewHandle()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr shellDllDefView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        IntPtr listView = FindWindowEx(shellDllDefView, IntPtr.Zero, "SysListView32", "FolderView");

        if (listView == IntPtr.Zero)
        {
            EnumWindows((hWnd, lParam) =>
            {
                if (GetClassName(hWnd) == "WorkerW")
                {
                    IntPtr defView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero)
                    {
                        listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
                        if (listView != IntPtr.Zero) return false; // Found, stop enumerating
                    }
                }
                return true;
            }, IntPtr.Zero);
        }

        return listView;
    }

    private static string GetClassName(IntPtr hWnd)
    {
        var buffer = new StringBuilder(256);
        GetClassName(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    public static bool AreIconsVisible()
    {
        IntPtr listView = GetDesktopListViewHandle();
        if (listView != IntPtr.Zero)
        {
            int style = GetWindowLong(listView, GWL_STYLE);
            return (style & WS_VISIBLE) != 0;
        }
        return true; // Default assumption
    }

    public static void ToggleDesktopIcons()
    {
        IntPtr listView = GetDesktopListViewHandle();
        if (listView != IntPtr.Zero)
        {
            // The command 0x7402 (29698) toggles the desktop icons visibility
            SendMessage(listView, WM_COMMAND, new IntPtr(0x7402), IntPtr.Zero);
        }
    }
}
