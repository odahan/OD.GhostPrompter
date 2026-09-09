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
    [LibraryImport("kernel32.dll")] internal static partial uint GetLastError();
}
