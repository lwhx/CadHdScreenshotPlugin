using System;
using System.Runtime.InteropServices;
using CadHdScreenshot.Core.InputMethod;

namespace CadHdScreenshot.Plugin.InputMethod
{
    internal sealed class InputAdapter : IDisposable
    {
        private static readonly Guid Conversion = new Guid("ccf05dd8-4a87-11d7-a6e2-00065b84435c");
        private static readonly Guid OpenClose = new Guid("58273aad-01bb-4164-95c6-755ba0b5162d");
        private ITfThreadMgr manager;
        private ITfCompartment conversion, open;
        private uint client, thread;
        private bool activated;
        internal string Backend { get; private set; } = "未检测";

        internal void Initialize()
        {
            thread = NativeInput.GetCurrentThreadId();
            try
            {
                if (TF_GetThreadMgr(out manager) != 0 || manager == null) return;
                if (manager.Activate(out client) != 0) { Release(ref manager); return; }
                activated = true;
                var compartments = (ITfCompartmentMgr)manager;
                var a = Conversion; var b = OpenClose;
                compartments.GetCompartment(ref a, out conversion);
                compartments.GetCompartment(ref b, out open);
            }
            catch { Dispose(); }
        }

        private static bool ChineseLayout(uint focusThread)
        {
            return ((long)NativeInput.GetKeyboardLayout(focusThread) & 0x3ff) == 4;
        }

        internal InputMode Read(IntPtr hwnd)
        {
            uint pid;
            var focusThread = NativeInput.GetWindowThreadProcessId(hwnd, out pid);
            if (!ChineseLayout(focusThread)) { Backend = "当前不是中文输入法（保持原输入法）"; return InputMode.Unknown; }
            int flags, opened;
            if (focusThread == thread && TryTsf(out flags, out opened))
            {
                Backend = "TSF";
                return Mode(flags, opened != 0);
            }
            var context = NativeInput.ImmGetContext(hwnd);
            if (context == IntPtr.Zero) { Backend = "无可用输入上下文"; return InputMode.Unknown; }
            try
            {
                uint conv, sentence;
                if (!NativeInput.ImmGetConversionStatus(context, out conv, out sentence))
                { Backend = "输入法未公开可查询的状态"; return InputMode.Unknown; }
                Backend = "IMM32";
                return Mode((int)conv, NativeInput.ImmGetOpenStatus(context));
            }
            finally { NativeInput.ImmReleaseContext(hwnd, context); }
        }

        internal bool Switch(IntPtr hwnd, InputMode target, out InputMode verified)
        {
            verified = InputMode.Unknown;
            if (target != InputMode.Chinese && target != InputMode.English) return false;
            uint pid;
            uint focusThread = NativeInput.GetWindowThreadProcessId(hwnd, out pid);
            if (!ChineseLayout(focusThread)) return false;
            int flags, opened;
            if (focusThread == thread && TryTsf(out flags, out opened))
            {
                // Keep the current IME/profile. Only change native/alphanumeric mode.
                object value = target == InputMode.Chinese ? (flags | 1) : (flags & ~9);
                if (target == InputMode.Chinese && opened == 0)
                {
                    object enabled = 1;
                    if (open.SetValue(client, ref enabled) != 0) return false;
                }
                if (conversion.SetValue(client, ref value) != 0) return false;
                verified = Read(hwnd);
                return verified == target;
            }
            var context = NativeInput.ImmGetContext(hwnd);
            if (context == IntPtr.Zero) return false;
            try
            {
                uint conv, sentence;
                if (!NativeInput.ImmGetConversionStatus(context, out conv, out sentence)) return false;
                if (target == InputMode.Chinese && !NativeInput.ImmGetOpenStatus(context) &&
                    !NativeInput.ImmSetOpenStatus(context, true)) return false;
                var desired = target == InputMode.Chinese ? (conv | 1u) : (conv & ~9u);
                if (!NativeInput.ImmSetConversionStatus(context, desired, sentence)) return false;
            }
            finally { NativeInput.ImmReleaseContext(hwnd, context); }
            verified = Read(hwnd);
            return verified == target;
        }

        private bool TryTsf(out int flags, out int opened)
        {
            flags = opened = 0;
            if (conversion == null || open == null) return false;
            try
            {
                object a, b;
                if (conversion.GetValue(out a) != 0 || open.GetValue(out b) != 0 || !(a is int) || !(b is int)) return false;
                flags = (int)a; opened = (int)b;
                return opened == 0 || opened == 1;
            }
            catch (COMException) { return false; }
        }

        private static InputMode Mode(int flags, bool opened)
        { return opened && (flags & 1) != 0 ? InputMode.Chinese : InputMode.English; }

        public void Dispose()
        {
            Release(ref conversion); Release(ref open);
            if (manager != null && activated) { try { manager.Deactivate(); } catch (COMException) { } }
            activated = false;
            client = 0;
            Release(ref manager);
        }
        private static void Release<T>(ref T value) where T : class
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
            value = null;
        }

        [DllImport("msctf.dll", PreserveSig = true)] private static extern int TF_GetThreadMgr(out ITfThreadMgr manager);
        // Only the leading methods we use are declared; order is from Windows SDK msctf.h.
        [ComImport, Guid("aa80e801-2021-11d2-93e0-0060b067b86e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITfThreadMgr
        {
            [PreserveSig] int Activate(out uint client);
            [PreserveSig] int Deactivate();
        }
        [ComImport, Guid("7dcf57ac-18ad-438b-824d-979bffb74b7c"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITfCompartmentMgr
        { [PreserveSig] int GetCompartment(ref Guid id, out ITfCompartment compartment); }
        [ComImport, Guid("bb08f7a9-607a-4384-8623-056892b64371"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITfCompartment
        {
            [PreserveSig] int SetValue(uint client, [In, MarshalAs(UnmanagedType.Struct)] ref object value);
            [PreserveSig] int GetValue([MarshalAs(UnmanagedType.Struct)] out object value);
        }
    }
}
