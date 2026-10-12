using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CadHdScreenshot.Plugin.InputMethod
{
    internal static class NativeInput
    {
        internal delegate void WinEventCallback(IntPtr hook, uint eventType, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
        [StructLayout(LayoutKind.Sequential)]
        internal struct Point { internal int X, Y; }
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventCallback callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
        [StructLayout(LayoutKind.Sequential)]
        internal struct GuiInfo
        {
            public uint Size, Flags;
            public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
            public int Left, Top, Right, Bottom;
        }
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
        [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] internal static extern bool IsChild(IntPtr parent, IntPtr child);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder text, int count);
        [DllImport("user32.dll")] internal static extern IntPtr GetKeyboardLayout(uint thread);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wparam,
            IntPtr lparam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] internal static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[] layouts);
        [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
        [DllImport("imm32.dll")] internal static extern IntPtr ImmGetContext(IntPtr hwnd);
        [DllImport("imm32.dll")] internal static extern bool ImmReleaseContext(IntPtr hwnd, IntPtr context);
        [DllImport("imm32.dll")] internal static extern bool ImmGetOpenStatus(IntPtr context);
        [DllImport("imm32.dll")] internal static extern bool ImmSetOpenStatus(IntPtr context, bool open);
        [DllImport("imm32.dll")] internal static extern bool ImmGetConversionStatus(IntPtr context, out uint conversion, out uint sentence);
        [DllImport("imm32.dll")] internal static extern bool ImmSetConversionStatus(IntPtr context, uint conversion, uint sentence);
        [DllImport("imm32.dll")] internal static extern bool ImmNotifyIME(IntPtr context, uint action, uint index, IntPtr value);
        internal static string ClassName(IntPtr hwnd)
        {
            var text = new StringBuilder(256);
            GetClassName(hwnd, text, text.Capacity);
            return text.ToString();
        }
        internal static IntPtr Focus(uint thread)
        {
            var info = new GuiInfo { Size = (uint)Marshal.SizeOf(typeof(GuiInfo)) };
            return GetGUIThreadInfo(thread, ref info) ? info.Focus : IntPtr.Zero;
        }
    }
}
