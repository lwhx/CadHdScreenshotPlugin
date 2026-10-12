using System;
using System.IO;
using CadHdScreenshot.Plugin.InputMethod;
using CadHdScreenshot.Core.InputMethod;

internal static class Program
{
    private static int passed, failed;
    private static InputContext Context(string key = "drawing", InputMode target = InputMode.English)
        => new InputContext { Session = key, Desired = target };
    private static void Equal<T>(T expected, T actual)
    { if (!Equals(expected, actual)) throw new Exception("Expected " + expected + ", actual " + actual); }
    private static void Test(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    private static int Main()
    {
        Test("stable focus debounces before switching", () => {
            var p = new InputPolicy(); var c = Context();
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 0));
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 99));
            Equal(InputMode.English, p.Observe(c, InputMode.Chinese, 100));
        });
        Test("already-correct mode requires no write", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.English, 0);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.English, 1000));
        });
        Test("unknown IME state never writes", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.Unknown, 0);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Unknown, 1000));
        });
        Test("manual switch before initial write wins", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.English, 0);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 200));
            Equal(true, p.ManualOverride);
        });
        Test("manual switch after verified write wins", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.Chinese, 0); p.Observe(c, InputMode.Chinese, 100);
            p.CompleteAttempt(InputMode.English, true, 101);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 200));
            Equal(true, p.ManualOverride);
        });
        Test("delayed programmatic acknowledgment is not manual", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.Chinese, 0); p.Observe(c, InputMode.Chinese, 100);
            p.CompleteAttempt(InputMode.Unknown, false, 101);
            p.Observe(c, InputMode.English, 300);
            Equal(false, p.ManualOverride);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.English, 500));
        });
        Test("failed switch is not retried in a loop", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.Chinese, 0); p.Observe(c, InputMode.Chinese, 100);
            p.CompleteAttempt(InputMode.Unknown, false, 101);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 10000));
        });
        Test("backoff applies across scene changes", () => {
            var p = new InputPolicy(); var a = Context(); var b = Context("file", InputMode.Chinese);
            p.Observe(a, InputMode.Chinese, 0); p.Observe(a, InputMode.Chinese, 100);
            p.CompleteAttempt(InputMode.Unknown, false, 101);
            p.Observe(b, InputMode.English, 200);
            Equal(InputMode.Preserve, p.Observe(b, InputMode.English, 500));
            Equal(InputMode.Chinese, p.Observe(b, InputMode.English, 1601));
        });
        Test("new semantic scene clears manual override", () => {
            var p = new InputPolicy(); var a = Context(); var b = Context("text", InputMode.Chinese);
            p.Observe(a, InputMode.English, 0); p.Observe(a, InputMode.Chinese, 200);
            p.Observe(b, InputMode.English, 300);
            Equal(InputMode.Chinese, p.Observe(b, InputMode.English, 500));
            Equal(false, p.ManualOverride);
        });
        Test("background and tool panel do not clear manual choice", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.English, 0); p.Observe(c, InputMode.Chinese, 200);
            p.Observe(Context("external", InputMode.Preserve), InputMode.Unknown, 300);
            p.Observe(Context("panel", InputMode.Preserve), InputMode.Unknown, 500);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 900));
            Equal(true, p.ManualOverride);
        });
        Test("disable/reset forgets pending switch", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.Chinese, 0); p.Observe(c, InputMode.Chinese, 100);
            p.Reset();
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 200));
            Equal(InputMode.English, p.Observe(c, InputMode.Chinese, 400));
        });
        Test("cancelled write after focus change may retry safely", () => {
            var p = new InputPolicy(); var c = Context();
            p.Observe(c, InputMode.Chinese, 0); p.Observe(c, InputMode.Chinese, 100);
            p.CancelUnwrittenAttempt(101);
            Equal(InputMode.Preserve, p.Observe(c, InputMode.Chinese, 150));
            Equal(InputMode.English, p.Observe(c, InputMode.Chinese, 450));
        });
        Test("TEXT rectangle or height stage remains English", () => {
            Equal(InputMode.English, InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true }).Desired);
        });
        Test("actual text content becomes Chinese", () => {
            Equal(InputMode.Chinese, InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true, TextPrompt = true }).Desired);
        });
        Test("inplace text editor becomes Chinese", () => {
            Equal(InputMode.Chinese, InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true, TextEditCommandActive = true, TextEditorControl = true, TextEditorHandle = new IntPtr(123), UnknownEdit = true }).Desired);
        });
        Test("pointer entering CAD switches English without recognizing an input control", () => {
            var f = new FocusFacts { Owned = true, PointerInsideCad = true, UnknownEdit = true };
            Equal(InputMode.English, InputClassifier.Classify(f).Desired);
        });
        Test("stale nonzero editor handle cannot turn ordinary drawing input Chinese", () => {
            var f = new FocusFacts { Owned = true, Drawing = true, PointerInsideCad = true, TextEditorHandle = new IntPtr(123) };
            Equal(InputMode.English, InputClassifier.Classify(f).Desired);
        });
        Test("MTEXT rectangle dynamic field is English until real text editor has focus", () => {
            var f = new FocusFacts { Owned = true, Drawing = true, PointerInsideCad = true, TextEditCommandActive = true, TextEditorHandle = new IntPtr(123), UnknownEdit = true };
            Equal(InputMode.English, InputClassifier.Classify(f).Desired);
        });
        Test("confirmed text editor remains Chinese inside CAD", () => {
            var f = new FocusFacts { Owned = true, Drawing = true, PointerInsideCad = true, TextEditCommandActive = true, TextEditorControl = true, TextEditorHandle = new IntPtr(123) };
            Equal(InputMode.Chinese, InputClassifier.Classify(f).Desired);
        });
        Test("pointer over background CAD never affects an external foreground app", () => {
            Equal(InputMode.Preserve, InputClassifier.Classify(new FocusFacts { PointerInsideCad = true }).Desired);
        });
        Test("AutoCAD non-null editor wrapper with null native pointer is command input", () => {
            var facts = new FocusFacts { Owned = true, Drawing = true, TextEditorHandle = IntPtr.Zero };
            var context = InputClassifier.Classify(facts);
            Equal(InputMode.English, context.Desired);
            Equal(InputScene.Drawing, context.Scene);
        });
        Test("external app overrides all CAD hints", () => {
            Equal(InputMode.Preserve, InputClassifier.Classify(new FocusFacts { Drawing = true, TextPrompt = true }).Desired);
        });
        Test("own panel preserves shortcut typing", () => {
            Equal(InputMode.Preserve, InputClassifier.Classify(new FocusFacts { Owned = true, ToolPanel = true, Drawing = true }).Desired);
        });
        Test("confirmed filename field becomes Chinese", () => {
            Equal(InputMode.Chinese, InputClassifier.Classify(new FocusFacts { Owned = true, Dialog = true, FileName = true }).Desired);
        });
        Test("address and unknown dialog fields are preserved", () => {
            Equal(InputMode.Preserve, InputClassifier.Classify(new FocusFacts { Owned = true, Dialog = true, UnknownEdit = true }).Desired);
        });
        Test("palette Edit is not treated as command input", () => {
            Equal(InputMode.Preserve, InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true, UnknownEdit = true }).Desired);
        });
        Test("recognized command line uses English", () => {
            Equal(InputMode.English, InputClassifier.Classify(new FocusFacts { Owned = true, CommandLine = true, UnknownEdit = true }).Desired);
        });
        Test("drawing and command-line focus share manual override", () => {
            var a = InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true, DocumentSession = "1" });
            var b = InputClassifier.Classify(new FocusFacts { Owned = true, CommandLine = true, DocumentSession = "1" });
            Equal(a.Session, b.Session);
        });
        Test("document change creates a new session", () => {
            var a = InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true, DocumentSession = "1" });
            var b = InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true, DocumentSession = "2" });
            Equal(false, a.Session == b.Session);
        });
        Test("focus moves inside one text editor share a session", () => {
            var a = InputClassifier.Classify(new FocusFacts { Owned = true, Drawing = true, TextPrompt = true, DocumentSession = "1", EditorSession = "1", RootSession = "a" });
            var b = InputClassifier.Classify(new FocusFacts { Owned = true, CommandLine = true, TextPrompt = true, DocumentSession = "1", EditorSession = "1", RootSession = "b" });
            Equal(a.Session, b.Session);
        });
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-data", Guid.NewGuid().ToString("N"));
        string config = Path.Combine(folder, "ime.xml");
        Test("first run is opt-in with diagnostics disabled", () => {
            var value = ImeSettings.Load(config);
            Equal(false, value.Enabled); Equal(false, value.Diagnostics);
        });
        Test("enabled and diagnostics survive restart", () => {
            new ImeSettings { Enabled = true, Diagnostics = true }.Save(config);
            var value = ImeSettings.Load(config);
            Equal(true, value.Enabled); Equal(true, value.Diagnostics);
        });
        Test("disabling atomically replaces persisted settings", () => {
            new ImeSettings { Enabled = false, Diagnostics = false }.Save(config);
            var value = ImeSettings.Load(config);
            Equal(false, value.Enabled); Equal(false, value.Diagnostics);
            Equal(false, File.Exists(config + ".tmp"));
        });
        Test("malformed config reports failure instead of enabling", () => {
            File.WriteAllText(config, "<broken>");
            bool rejected = false;
            try { ImeSettings.Load(config); } catch (InvalidOperationException) { rejected = true; }
            Equal(true, rejected);
        });
        Console.WriteLine("RESULT: " + passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
