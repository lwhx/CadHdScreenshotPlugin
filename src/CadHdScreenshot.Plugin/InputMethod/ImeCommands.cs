using Autodesk.AutoCAD.Runtime;

namespace CadHdScreenshot.Plugin.InputMethod
{
    public sealed class ImeCommands
    {
        [CommandMethod("CADIMEON", CommandFlags.Session)]
        public void Enable() { InputMethodService.Current.Enabled = true; }
        [CommandMethod("CADIMEOFF", CommandFlags.Session)]
        public void Disable() { InputMethodService.Current.Enabled = false; }
        [CommandMethod("CADIMESTATUS", CommandFlags.Session)]
        public void Status() { InputMethodService.WriteCommand(InputMethodService.Current.Status + " " + InputMethodService.Current.Detail); }
        [CommandMethod("CADIMESET", CommandFlags.Session)]
        public void Settings() { ScreenshotWindowHost.ShowInputMethod(); }
        [CommandMethod("CADIMEDIAG", CommandFlags.Session)]
        public void Diagnostics()
        {
            var service = InputMethodService.Current;
            service.Diagnostics = !service.Diagnostics;
            InputMethodService.WriteCommand("诊断日志" + (service.Diagnostics ? "已开启：" + service.LogPath : "已关闭"));
        }
        [CommandMethod("CADIMERELOAD", CommandFlags.Session)]
        public void Reload() { InputMethodService.Current.Reload(); Status(); }
    }
}
