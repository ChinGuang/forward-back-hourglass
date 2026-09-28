using System.IO;
using System.Runtime.InteropServices;

namespace Hourglass.App;

/// <summary>The few Win32 calls auto mode needs.</summary>
internal static class NativeMethods
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int GwlExStyle = -20;

    public const int WsExNoActivate = 0x08000000;
    public const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, [Out] char[] path, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr icon);

    /// <summary>
    /// The full path of a process's program. Uses the "limited" access right, which also works for programs
    /// running as administrator (where <c>Process.MainModule</c> would throw).
    /// </summary>
    public static string? GetProcessPath(uint processId)
    {
        IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            char[] buffer = new char[1024];
            uint size = (uint)buffer.Length;
            return QueryFullProcessImageName(process, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public static string? GetProcessFileName(uint processId) =>
        GetProcessPath(processId) is { } path ? Path.GetFileName(path) : null;

    /// <summary>Stops a window from ever taking focus, even when clicked (used for the "classify this site" popup).</summary>
    public static void MakeNonActivating(IntPtr window)
    {
        long style = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        SetWindowLongPtr(window, GwlExStyle, new IntPtr(style | WsExNoActivate | WsExToolWindow));
    }
}
