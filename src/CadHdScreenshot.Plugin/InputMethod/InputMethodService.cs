using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using CadHdScreenshot.Core.InputMethod;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CadHdScreenshot.Plugin.InputMethod
{
    public sealed class InputMethodService : INotifyPropertyChanged, IDisposable
    {
        public static InputMethodService Current { get; } = new InputMethodService();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly uint processId = (uint)Process.GetCurrentProcess().Id;
        private readonly InputPolicy policy = new InputPolicy();
        private readonly FocusProbe probe;
        private InputAdapter adapter;
        private Timer timer;
        private IntPtr focusHook;
        private NativeInput.WinEventCallback focusCallback;
        private ImeSettings settings = new ImeSettings();
        private Document document;
        private long documentSession, editorSession;
        private long pointerSession;
        private bool pointerWasInside;
        private bool initialized, busy, textPrompt, inplace;
        private string status = "未启用", detail = "", lastLog = "";
        internal IntPtr PanelHandle { get; set; }

        private InputMethodService() { probe = new FocusProbe(processId, () => clock.ElapsedMilliseconds); }
        public event PropertyChangedEventHandler PropertyChanged;
        public bool Enabled
        {
            get => settings.Enabled;
            set
            {
                if (settings.Enabled == value) return;
                settings.Enabled = value;
                if (value) Start(); else Stop();
                Save(); Changed(nameof(Enabled));
                WriteCommand(settings.Enabled ? "智能输入法切换已启用。" : "智能输入法切换已关闭。");
            }
        }
        public bool Diagnostics
        {
            get => settings.Diagnostics;
            set { if (settings.Diagnostics == value) return; settings.Diagnostics = value; Save(); Changed(nameof(Diagnostics)); }
        }
        public string Status { get => status; private set { if (status == value) return; status = value; Changed(nameof(Status)); } }
        public string Detail { get => detail; private set { if (detail == value) return; detail = value; Changed(nameof(Detail)); } }
        public string LogPath => ImeDiagnostics.LogPath;

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Reload();
        }
        public void Reload()
        {
            Stop();
            try { settings = ImeSettings.Load(); }
            catch (Exception) { settings = new ImeSettings(); Status = "配置读取失败，已安全停用；可重新勾选启用。"; }
            if (settings.Enabled) Start();
            Changed(nameof(Enabled)); Changed(nameof(Diagnostics));
        }
        private void Save()
        {
            try { settings.Save(); }
            catch (Exception) { Status = "当前开关已生效，但配置保存失败，下次启动不会保留。"; }
        }
        private void Start()
        {
            if (timer != null) return;
            try
            {
                adapter = new InputAdapter(); adapter.Initialize();
                // Process-scoped accessibility focus notification; never a keyboard hook.
                focusCallback = (hook, type, hwnd, obj, child, thread, time) => probe.Reset();
                focusHook = NativeInput.SetWinEventHook(0x8005, 0x8005, IntPtr.Zero, focusCallback, processId, 0, 0);
                if (focusHook == IntPtr.Zero) throw new InvalidOperationException("Focus hook unavailable");
                CadApp.DocumentManager.DocumentActivated += OnDocumentActivated;
                CadApp.DocumentManager.DocumentToBeDestroyed += OnDocumentDestroyed;
                Attach(CadApp.DocumentManager.MdiActiveDocument);
                timer = new Timer { Interval = 100 };
                timer.Tick += Tick; timer.Start();
                Status = "已启用，等待 CAD 输入焦点";
            }
            catch (Exception ex)
            {
                Stop(); settings.Enabled = false;
                Status = "启动失败，已停用；可开启诊断后重试。";
                ImeDiagnostics.Write(settings.Diagnostics, "start_failure=" + ex.GetType().Name);
            }
        }
        private void Stop()
        {
            if (timer != null) { timer.Stop(); timer.Tick -= Tick; timer.Dispose(); timer = null; }
            if (focusHook != IntPtr.Zero) { NativeInput.UnhookWinEvent(focusHook); focusHook = IntPtr.Zero; }
            focusCallback = null;
            CadApp.DocumentManager.DocumentActivated -= OnDocumentActivated;
            CadApp.DocumentManager.DocumentToBeDestroyed -= OnDocumentDestroyed;
            Attach(null);
            adapter?.Dispose(); adapter = null;
            policy.Reset(); probe.Reset();
            pointerWasInside = false;
            Status = "已关闭（不干预输入法）"; Detail = "";
        }
        private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e) { Attach(e.Document); }
        private void OnDocumentDestroyed(object sender, DocumentCollectionEventArgs e) { if (e.Document == document) Attach(null); }
        private void Attach(Document next)
        {
            if (document == next) return;
            if (document != null)
            {
                document.Editor.PromptingForString -= OnString;
                document.Editor.PromptedForString -= OnStringDone;
                document.CommandEnded -= OnCommandDone;
                document.CommandCancelled -= OnCommandDone;
                document.CommandFailed -= OnCommandDone;
            }
            document = next;
            documentSession++; textPrompt = inplace = false;
            policy.Reset(); probe.Reset();
            if (document != null)
            {
                document.Editor.PromptingForString += OnString;
                document.Editor.PromptedForString += OnStringDone;
                document.CommandEnded += OnCommandDone;
                document.CommandCancelled += OnCommandDone;
                document.CommandFailed += OnCommandDone;
            }
        }
        private void OnString(object sender, PromptStringOptionsEventArgs e)
        {
            // Command name alone is insufficient: only the actual string prompt qualifies.
            string command = document?.CommandInProgress?.TrimStart('_', '.').ToUpperInvariant();
            textPrompt = command == "TEXT" || command == "DTEXT" || command == "-MTEXT";
            if (textPrompt) editorSession++;
        }
        private void OnStringDone(object sender, PromptStringResultEventArgs e) { textPrompt = false; }
        private void OnCommandDone(object sender, CommandEventArgs e) { textPrompt = false; }

        private void Tick(object sender, EventArgs args)
        {
            if (busy || !settings.Enabled || adapter == null) return;
            busy = true;
            try { UpdateFocus(); }
            catch (Exception ex)
            {
                // Never let a provider/COM exception escape into AutoCAD's message loop.
                ImeDiagnostics.Write(settings.Diagnostics, "monitor_failure=" + ex.GetType().Name);
                Status = "检测暂不可用，保持当前输入法";
            }
            finally { busy = false; }
        }

        private void UpdateFocus()
        {
            var foreground = NativeInput.GetForegroundWindow();
            uint owner;
            uint focusThread = NativeInput.GetWindowThreadProcessId(foreground, out owner);
            if (owner != processId) { pointerWasInside = false; Status = "已启用 · CAD 在后台，暂停切换"; Detail = "场景：其他软件（保持）"; return; }
            var mainWindow = CadApp.MainWindow.Handle;
            bool pointerInside = PointerInsideMainWindow(mainWindow);
            if (pointerInside && !pointerWasInside) pointerSession++;
            pointerWasInside = pointerInside;
            var focus = NativeInput.Focus(focusThread);
            if (focus == IntPtr.Zero) { Detail = "场景：没有输入焦点（保持）"; return; }
            uint focusOwner;
            NativeInput.GetWindowThreadProcessId(focus, out focusOwner);
            if (focusOwner != processId) return;
            var root = NativeInput.GetAncestor(focus, 2);
            if (root == PanelHandle && !pointerInside) { Status = "已启用 · 工具面板内保持输入法"; Detail = "场景：工具面板（保持）"; return; }
            if (document != CadApp.DocumentManager.MdiActiveDocument) Attach(CadApp.DocumentManager.MdiActiveDocument);
            string command = document?.CommandInProgress?.TrimStart('_', '.').ToUpperInvariant();
            bool textCommand = command == "TEXT" || command == "DTEXT" || command == "MTEXT" ||
                command == "MTEDIT" || command == "DDEDIT" || command == "TEXTEDIT" ||
                command == "TABLEDIT" || command == "ATTEDIT" || command == "EATTEDIT";
            // Mouse entry into normal CAD uses a native path; it must not wait for
            // UIA recognition. AutoCAD's command line is often an Edit child
            // whose two-level ancestor is not the main window, so checking the
            // root here makes command input fall into "unconfirmed" preserve
            // mode even though the pointer is visibly inside CAD.
            bool pointerPath = pointerInside && !textCommand && root != PanelHandle;
            var snapshot = pointerPath ? null : probe.Poll(focus, foreground);
            if (snapshot == null && !pointerPath) { Status = "已启用 · 等待输入框识别"; Detail = "场景：识别中（保持）"; return; }
            IntPtr drawing = document == null ? IntPtr.Zero : document.Window.Handle;
            bool drawingFocus = drawing != IntPtr.Zero && (focus == drawing || NativeInput.IsChild(drawing, focus));
            IntPtr textEditorHandle = IntPtr.Zero;
            // AutoCAD API is called only on its UI thread, after confirming CAD foreground.
            if (drawingFocus && textCommand && snapshot != null && snapshot.TextEditorControl)
            {
                try
                {
                    // Current always constructs a wrapper, including for native NULL.
                    // Its AutoDelete=false wrapper can be disposed without closing the CAD editor.
                    using (var currentEditor = InplaceTextEditor.Current)
                        if (currentEditor != null) textEditorHandle = currentEditor.UnmanagedObject;
                }
                catch (Autodesk.AutoCAD.Runtime.Exception) { }
            }
            bool isInplace = textEditorHandle != IntPtr.Zero;
            if (isInplace && !inplace) editorSession++;
            inplace = isInplace;
            bool commandLine = snapshot != null && snapshot.CommandLine;
            // A native pointer path is authoritative for normal CAD input.
            // Its focused child can look like a dialog to the root-handle
            // heuristic, but it must still be treated as drawing input.
            bool dialog = !pointerPath && root != mainWindow && root != PanelHandle &&
                !drawingFocus && !commandLine;
            var context = InputClassifier.Classify(new FocusFacts
            {
                Owned = true, Dialog = dialog, FileName = snapshot != null && snapshot.FileName,
                Drawing = drawingFocus, CommandLine = commandLine, PointerInsideCad = pointerInside,
                TextEditCommandActive = textCommand, TextEditorControl = snapshot != null && snapshot.TextEditorControl,
                TextPrompt = textPrompt, TextEditorHandle = textEditorHandle, UnknownEdit = snapshot != null && snapshot.Edit,
                DocumentSession = documentSession + ":entry:" + pointerSession, RootSession = root.ToString(), EditorSession = editorSession.ToString()
            });
            string signature = snapshot == null ? "NativePointerInsideCad" : snapshot.Signature;
            if (context.Desired == InputMode.Preserve)
            {
                Status = "已启用 · 未确认的输入区域，保持输入法";
                Detail = "场景：未确认输入框（保持）";
                Log(context, InputMode.Unknown, "preserve", signature); return;
            }
            var mode = adapter.Read(focus);
            var request = policy.Observe(context, mode, clock.ElapsedMilliseconds);
            Detail = "场景：" + SceneLabel(context.Scene) + "　接口：" + adapter.Backend;
            Status = mode == InputMode.Unknown ? "无法可靠读取状态，保持当前输入法" :
                policy.ManualOverride ? "本场景已手动切换，暂停自动干预" : "已启用 · 当前" + ModeLabel(mode);
            if (request != InputMode.Preserve)
            {
                // Last-moment ownership/focus check: no writes after Alt+Tab or dialog change.
                if (NativeInput.GetForegroundWindow() != foreground || NativeInput.Focus(focusThread) != focus ||
                    !settings.Enabled || (snapshot != null && !probe.IsCurrent(snapshot)) ||
                    (pointerPath && !PointerInsideMainWindow(mainWindow)))
                { policy.CancelUnwrittenAttempt(clock.ElapsedMilliseconds); return; }
                InputMode verified;
                bool success = adapter.Switch(focus, request, out verified);
                policy.CompleteAttempt(verified, success, clock.ElapsedMilliseconds);
                Status = success ? "已切换为" + ModeLabel(verified) : "切换未获确认，已停止重试；可手动切换";
                Log(context, verified, success ? "verified" : "unconfirmed", signature);
            }
            else Log(context, mode, policy.ManualOverride ? "manual" : "observe", signature);
        }
        private bool PointerInsideMainWindow(IntPtr mainWindow)
        {
            NativeInput.Point point;
            if (!NativeInput.GetCursorPos(out point)) return false;
            var hovered = NativeInput.WindowFromPoint(point);
            uint owner;
            NativeInput.GetWindowThreadProcessId(hovered, out owner);
            return owner == processId && NativeInput.GetAncestor(hovered, 2) == mainWindow;
        }
        private void Log(InputContext context, InputMode mode, string action, string signature)
        {
            string line = "scene=" + context.Scene + " mode=" + mode + " action=" + action + " backend=" + adapter?.Backend + " ui=" + signature;
            if (line == lastLog) return;
            lastLog = line; ImeDiagnostics.Write(settings.Diagnostics, line);
        }
        private static string ModeLabel(InputMode mode) => mode == InputMode.Chinese ? "中文" : "英文";
        private static string SceneLabel(InputScene scene)
        {
            switch (scene)
            {
                case InputScene.Drawing: return "CAD 窗口 / 英文输入";
                case InputScene.CommandLine: return "命令行";
                case InputScene.TextEntry: return "文字内容编辑";
                case InputScene.FileName: return "文件名";
                default: return "保持";
            }
        }
        private void Changed(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
        internal static void WriteCommand(string message) { CadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\n[CAD 输入法] " + message + "\n"); }
        public void Dispose() { Stop(); }
    }
}
