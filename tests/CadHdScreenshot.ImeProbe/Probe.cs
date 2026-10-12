using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using CadHdScreenshot.Plugin.InputMethod;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

[assembly: ExtensionApplication(typeof(Probe))]

// Temporary, read-only diagnostic command. No key injection, text reads or drawing modifications.
public sealed class Probe : IExtensionApplication
{
    private static Timer timer;
    private static DateTime until;
    private static string previous;
    private static string path => @"C:\Users\Administrator\Documents\Codex\2026-09-12\d-db-56-cadhdscreenshotplugin\work\ime-probe.log";
    [DllImport("imm32.dll")] private static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wparam, IntPtr lparam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int GetDlgCtrlID(IntPtr hwnd);
    public void Initialize() { CadApp.Idle += OnIdle; }
    public void Terminate() { CadApp.Idle -= OnIdle; Stop(); }
    private static void OnIdle(object sender, EventArgs e)
    {
        if (CadApp.DocumentManager.MdiActiveDocument == null) return;
        CadApp.Idle -= OnIdle;
        new Probe().Start();
    }
    private static void Stop() { if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; } }
    [CommandMethod("CADIMEPROBE", CommandFlags.Session)]
    public void Start()
    {
        Stop(); previous = null; until = DateTime.UtcNow.AddSeconds(90);
        File.AppendAllText(path, "START " + DateTime.Now.ToString("O") + Environment.NewLine);
        timer = new Timer { Interval = 300 }; timer.Tick += Tick; timer.Start();
        CadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nIME diagnostic started for 90 seconds (no drawing edits).\n");
    }
    private static void Tick(object sender, EventArgs e)
    {
        if (DateTime.UtcNow >= until) { Stop(); return; }
        try
        {
            uint pid;
            var foreground = NativeInput.GetForegroundWindow();
            var thread = NativeInput.GetWindowThreadProcessId(foreground, out pid);
            if (pid != System.Diagnostics.Process.GetCurrentProcess().Id) return;
            var focus = NativeInput.Focus(thread);
            if (focus == IntPtr.Zero) return;
            var ime = ImmGetDefaultIMEWnd(focus);
            IntPtr opened, conv;
            var a = SendMessageTimeout(ime, 0x283, new IntPtr(5), IntPtr.Zero, 2, 100, out opened);
            var b = SendMessageTimeout(ime, 0x283, new IntPtr(1), IntPtr.Zero, 2, 100, out conv);
            var himc = NativeInput.ImmGetContext(focus);
            string imm = "none";
            if (himc != IntPtr.Zero)
            {
                try { uint c, s; var valid = NativeInput.ImmGetConversionStatus(himc, out c, out s); imm = "open=" + NativeInput.ImmGetOpenStatus(himc) + " conv=" + c + " valid=" + valid; }
                finally { NativeInput.ImmReleaseContext(focus, himc); }
            }
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "CadHdScreenshot.Plugin");
            var type = assembly?.GetType("CadHdScreenshot.Plugin.InputMethod.InputMethodService");
            var service = type?.GetProperty("Current").GetValue(null);
            var adapter = type?.GetField("adapter", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(service);
            string tsf = "none";
            if (adapter != null)
            {
                object[] args = { 0, 0 };
                var valid = adapter.GetType().GetMethod("TryTsf", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(adapter, args);
                tsf = "valid=" + valid + " conv=" + args[0] + " open=" + args[1];
            }
            string ancestry = "";
            var parent = focus;
            for (int i = 0; i < 6 && parent != IntPtr.Zero; i++, parent = GetParent(parent))
                ancestry += "/" + NativeInput.ClassName(parent) + "#" + GetDlgCtrlID(parent);
            NativeInput.Point point; NativeInput.GetCursorPos(out point);
            var hovered = NativeInput.WindowFromPoint(point);
            string line = "focus=" + focus + " class=" + ancestry + " hoveredRoot=" + NativeInput.GetAncestor(hovered, 2) +
                " main=" + CadApp.MainWindow.Handle + " focusRoot=" + NativeInput.GetAncestor(focus, 2) + " hkl=" + NativeInput.GetKeyboardLayout(thread).ToInt64().ToString("X") +
                " IMC(open=" + opened + " conv=" + conv + " ok=" + a + "," + b + ") IMM(" + imm + ") TSF(" + tsf + ") status=" + type?.GetProperty("Status").GetValue(service);
            if (line != previous) { File.AppendAllText(path, DateTime.Now.ToString("O") + " " + line + Environment.NewLine); previous = line; }
        }
        catch (System.Exception ex) { File.AppendAllText(path, ex.GetType().Name + Environment.NewLine); Stop(); }
    }
}
