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
            // Prefer IMM32 so the reported mode matches what the IME produces.
            var immMode = ReadWithImm32(hwnd);
            if (immMode != InputMode.Unknown)
            {
                Backend = "IMM32";
                return immMode;
            }
            int flags = 0, opened = 0;
            bool tsfReadable = false;
            if (focusThread == thread) tsfReadable = TryTsf(out flags, out opened);
            if (tsfReadable)
            {
                Backend = "TSF";
                return Mode(flags, opened != 0);
            }
            Backend = "无可用输入上下文";
            return InputMode.Unknown;
        }

        private static InputMode ReadWithImm32(IntPtr hwnd)
        {
            var context = NativeInput.ImmGetContext(hwnd);
            if (context == IntPtr.Zero) return InputMode.Unknown;
            try
            {
                uint conv, sentence;
                if (!NativeInput.ImmGetConversionStatus(context, out conv, out sentence))
                    return InputMode.Unknown;
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

            // IMM32 is the switch the IME itself honours. TSF compartments are
            // only a side channel: writing them can report success while the
            // candidate window and the keyboard layout stay Chinese, which is
            // what WeChat/Sogou do. So drive IMM32 first and treat TSF as a
            // follow-up hint, never as the confirming read.
            bool immApplied = SwitchWithImm32(hwnd, target);

            int flags = 0, opened = 0;
            bool tsfAvailable = false;
            if (focusThread == thread) tsfAvailable = TryTsf(out flags, out opened);
            if (tsfAvailable)
            {
                try
                {
                    object value = target == InputMode.Chinese ? (flags | 1) : (flags & ~9);
                    if (target == InputMode.Chinese && opened == 0)
                    {
                        object enabled = 1;
                        open.SetValue(client, ref enabled);
                    }
                    conversion.SetValue(client, ref value);
                }
                catch (COMException) { }
            }

            // Confirm against IMM32. If IMM32 could not be read, fall back to
            // the layout, which reflects what the IME actually receives.
            verified = Read(hwnd);
            if (verified == InputMode.Unknown) verified = ReadLayoutMode(focusThread, target);
            if (verified != target) return false;
            return immApplied || tsfAvailable;
        }

        private static bool SwitchWithImm32(IntPtr hwnd, InputMode target)
        {
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
                // Commit any half-typed composition so the next key is not
                // swallowed by a pending candidate window.
                NativeInput.ImmNotifyIME(context, 0x06, 0, IntPtr.Zero);
                return true;
            }
            finally { NativeInput.ImmReleaseContext(hwnd, context); }
        }

        private InputMode ReadLayoutMode(uint focusThread, InputMode target)
        {
            // The layout identifies the profile, not the per-mode state, so it
            // can only confirm that a Chinese-capable layout is still active.
            return ChineseLayout(focusThread) ? target : InputMode.Unknown;
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
