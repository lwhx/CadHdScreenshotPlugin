using System;

namespace CadHdScreenshot.Core.InputMethod
{
    public enum InputMode { Unknown, English, Chinese, Preserve }
    public enum InputScene { External, ToolPanel, Drawing, CommandLine, TextEntry, FileName, UnknownDialog }

    public sealed class InputContext
    {
        public InputScene Scene { get; set; }
        public string Session { get; set; }
        public InputMode Desired { get; set; }
    }

    public sealed class FocusFacts
    {
        public bool Owned, ToolPanel, Dialog, FileName, Drawing, CommandLine;
        public bool TextPrompt, UnknownEdit;
        public bool PointerInsideCad, TextEditCommandActive, TextEditorControl;
        public IntPtr TextEditorHandle;
        public string DocumentSession, RootSession, EditorSession;
    }

    public static class InputClassifier
    {
        public static InputContext Classify(FocusFacts f)
        {
            if (!f.Owned) return Make(InputScene.External, InputMode.Preserve, "external");
            if (f.ToolPanel) return Make(InputScene.ToolPanel, InputMode.Preserve, "panel");
            if (f.Dialog)
                return f.FileName
                    ? Make(InputScene.FileName, InputMode.Chinese, "file:" + f.RootSession)
                    : Make(InputScene.UnknownDialog, InputMode.Preserve, "dialog:" + f.RootSession);
            // A cached native editor may also outlive an edit session: require command + editor focus.
            bool activeEditor = f.TextEditCommandActive && f.TextEditorControl && f.TextEditorHandle != IntPtr.Zero;
            if ((f.Drawing || f.CommandLine) && (f.TextPrompt || activeEditor))
                return Make(InputScene.TextEntry, InputMode.Chinese, "text:" + f.DocumentSession + ":" + f.EditorSession);
            if (f.PointerInsideCad)
                return Make(InputScene.Drawing, InputMode.English, "input:" + f.DocumentSession);
            // An unrecognized Edit may be a palette text field. Do not force English there.
            if (f.UnknownEdit && !f.CommandLine)
                return Make(InputScene.UnknownDialog, InputMode.Preserve, "edit:" + f.RootSession);
            if (f.CommandLine) return Make(InputScene.CommandLine, InputMode.English, "input:" + f.DocumentSession);
            if (f.Drawing) return Make(InputScene.Drawing, InputMode.English, "input:" + f.DocumentSession);
            return Make(InputScene.UnknownDialog, InputMode.Preserve, "unknown:" + f.RootSession);
        }

        private static InputContext Make(InputScene scene, InputMode mode, string session)
        {
            return new InputContext { Scene = scene, Desired = mode, Session = session };
        }
    }

    // Pure, monotonic-clock state machine. At most one write per semantic session.
    // Returning from our panel or another application resumes the existing session.
    public sealed class InputPolicy
    {
        private string session;
        private InputMode observed = InputMode.Unknown;
        private InputMode pending = InputMode.Unknown;
        private long stableSince, pendingUntil, nextAttempt;
        private bool attempted;
        public bool ManualOverride { get; private set; }

        public InputMode Observe(InputContext context, InputMode mode, long milliseconds)
        {
            if (context.Desired == InputMode.Preserve) return InputMode.Preserve;
            if (session != context.Session)
            {
                session = context.Session;
                stableSince = milliseconds;
                observed = mode;
                pending = InputMode.Unknown;
                attempted = ManualOverride = false;
            }
            else if (mode != InputMode.Unknown)
            {
                if (pending != InputMode.Unknown && milliseconds <= pendingUntil)
                {
                    if (mode == pending) pending = InputMode.Unknown;
                }
                else
                {
                    pending = InputMode.Unknown;
                    if (observed != InputMode.Unknown && mode != observed) ManualOverride = true;
                }
                observed = mode;
            }
            if (mode == InputMode.Unknown || ManualOverride || attempted ||
                milliseconds - stableSince < 100 || milliseconds < nextAttempt || mode == context.Desired)
                return InputMode.Preserve;
            attempted = true;
            pending = context.Desired;
            pendingUntil = milliseconds + 450;
            nextAttempt = milliseconds + 300;
            return pending;
        }

        public void CompleteAttempt(InputMode verified, bool success, long milliseconds)
        {
            if (success && verified == pending)
            {
                observed = verified;
                pending = InputMode.Unknown;
            }
            // The target thread applies an input-language request
            // asynchronously, so a confirmed write can still report Unknown.
            // Trust the write and let the next poll observe the settled state.
            else if (success && verified == InputMode.Unknown)
            {
                // WM_INPUTLANGCHANGEREQUEST is asynchronous. Keep the
                // semantic session eligible for a follow-up observation
                // instead of permanently marking the attempt complete.
                attempted = false;
                pending = InputMode.Unknown;
                stableSince = milliseconds;
                nextAttempt = milliseconds + 150;
            }
            else nextAttempt = Math.Max(nextAttempt, milliseconds + 1500);
        }

        public void CancelUnwrittenAttempt(long milliseconds)
        {
            // Focus changed after Observe but before the native write. This was not a failure.
            attempted = false;
            pending = InputMode.Unknown;
            stableSince = milliseconds;
        }

        public void Reset()
        {
            session = null;
            observed = pending = InputMode.Unknown;
            attempted = ManualOverride = false;
            stableSince = pendingUntil = nextAttempt = 0;
        }
    }
}
