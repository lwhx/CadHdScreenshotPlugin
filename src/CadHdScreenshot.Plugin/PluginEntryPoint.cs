using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(CadHdScreenshot.Plugin.PluginEntryPoint))]

namespace CadHdScreenshot.Plugin
{
    public sealed class PluginEntryPoint : IExtensionApplication
    {
        public void Initialize()
        {
            CustomCommandRegistry.Initialize();
            Application.Idle += OnApplicationIdle;
        }

        public void Terminate()
        {
            Application.Idle -= OnApplicationIdle;
            CustomCommandRegistry.Terminate();
            ScreenshotWindowHost.Close();
        }

        private static void OnApplicationIdle(object sender, EventArgs e)
        {
            if (RibbonInstaller.TryInstall())
            {
                Application.Idle -= OnApplicationIdle;
            }
        }
    }
}
