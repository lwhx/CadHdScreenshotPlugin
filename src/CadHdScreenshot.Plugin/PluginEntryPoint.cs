using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

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
			InputMethod.InputMethodService.Current.Dispose();
			CustomCommandRegistry.Terminate();
			ScreenshotWindowHost.Close();
		}

		private static void OnApplicationIdle(object sender, EventArgs e)
		{
			InputMethod.InputMethodService.Current.Initialize();
			if (RibbonInstaller.TryInstall())
			{
				Application.Idle -= OnApplicationIdle;
			}
		}
	}
}
