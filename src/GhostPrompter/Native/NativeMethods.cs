using System.Runtime.InteropServices;

namespace GhostPrompter.Native;

/// <summary>Contains narrow P/Invoke definitions used by window and hotkey services.</summary>
internal static partial class NativeMethods
{
    internal const int GwlExStyle = -20;
    internal const long WsExTransparent = 0x20L;
    internal const long WsExNoActivate = 0x08000000L;
    internal const uint WmHotkey = 0x0312;
    internal const uint WdaExcludeFromCapture = 0x00000011;

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate bool MonitorEnumProc(nint monitor, nint deviceContext, ref Rect monitorRectangle, nint data);

    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowDisplayAffinity(nint hwnd, out uint affinity);
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hwnd, int index);
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hwnd, int id);
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hwnd, out Rect rectangle);
    [LibraryImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(nint deviceContext, nint clipRectangle, MonitorEnumProc callback, nint data);
    [LibraryImport("kernel32.dll")] internal static partial uint GetLastError();
}
