using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Automation;

namespace CadHdScreenshot.Plugin.InputMethod
{
    internal sealed class FocusSnapshot
    {
        internal IntPtr Focus, Root;
        internal bool Valid, Edit, FileName, CommandLine, TextEditorControl;
        internal long Timestamp;
        internal int Generation;
        // Only UI metadata, never Name, Value, Text, window titles or file names.
        internal string Signature = "";
    }

    internal sealed class FocusProbe
    {
        private readonly uint processId;
        private readonly Func<long> now;
        private Task<FocusSnapshot> task;
        private FocusSnapshot last;
        private int generation;
        internal FocusProbe(uint processId, Func<long> now) { this.processId = processId; this.now = now; }
        internal void Reset() { Interlocked.Increment(ref generation); last = null; }
        internal bool IsCurrent(FocusSnapshot snapshot) => snapshot.Generation == Volatile.Read(ref generation);

        internal FocusSnapshot Poll(IntPtr focus, IntPtr root)
        {
            if (task != null && task.IsCompleted)
            {
                if (task.Status == TaskStatus.RanToCompletion) last = task.Result;
                task = null;
            }
            // One in-flight UIA operation. A stuck provider cannot create unbounded tasks.
            if (task == null)
            {
                int epoch = Volatile.Read(ref generation);
                task = Task.Run(() =>
                {
                    var result = Read(focus, root);
                    result.Generation = epoch;
                    if (epoch != Volatile.Read(ref generation)) result.Valid = false;
                    return result;
                });
            }
            return last != null && last.Valid && IsCurrent(last) && last.Focus == focus && last.Root == root &&
                   now() - last.Timestamp <= 700 ? last : null;
        }

        private FocusSnapshot Read(IntPtr focus, IntPtr root)
        {
            var result = new FocusSnapshot { Focus = focus, Root = root };
            try
            {
                var element = AutomationElement.FocusedElement;
                if (element == null || element.Current.ProcessId != processId) return result;
                var focused = element;
                var chain = new List<string>();
                bool nameHost = false, fileDialog = false;
                for (int depth = 0; depth < 10 && element != null; depth++)
                {
                    var info = element.Current;
                    if (info.ProcessId != processId) break;
                    string id = info.AutomationId ?? "";
                    string cls = info.ClassName ?? "";
                    if (depth == 0) result.Edit = info.ControlType == ControlType.Edit || info.ControlType == ControlType.Document;
                    if (cls.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)) result.TextEditorControl = true;
                    // File name Edit/ComboBox in the standard Windows file picker.
                    if (depth > 0 && (id == "1148" || id == "FileNameControlHost")) nameHost = true;
                    if (cls == "DirectUIHWND") fileDialog = true;
                    // AutoCAD's command line input surfaces as a TextBox named
                    // "local:AutoCompleteEdit_N" inside a Custom element named
                    // "CommandLineControl". Older builds exposed "AcCommandLine"
                    // or "CommandLineInput" instead, so accept every variant.
                    if (cls == "AcCommandLine" || cls == "AcCmdLine" ||
                        id == "CommandLineInput" ||
                        id.StartsWith("local:AutoCompleteEdit", StringComparison.OrdinalIgnoreCase) ||
                        id.StartsWith("CommandLineControl", StringComparison.OrdinalIgnoreCase))
                        result.CommandLine = true;
                    // Log an allowlisted classification, not arbitrary provider-supplied strings.
                    // Class names and control ids are technical metadata, never UI Name/Value/Text.
                    chain.Add(info.ControlType.ProgrammaticName + ":" + cls + ":id=" + id);
                    element = TreeWalker.ControlViewWalker.GetParent(element);
                }
                result.FileName = result.Edit && nameHost && fileDialog && NativeInput.ClassName(root) == "#32770";
                uint ignored;
                uint thread = NativeInput.GetWindowThreadProcessId(focus, out ignored);
                var current = AutomationElement.FocusedElement;
                result.Valid = current != null && Automation.Compare(focused, current) &&
                    NativeInput.Focus(thread) == focus && NativeInput.GetForegroundWindow() == root;
                result.Timestamp = now();
                result.Signature = string.Join("/", chain);
            }
            catch (Exception) { result.Valid = false; }
            return result;
        }
    }
}
