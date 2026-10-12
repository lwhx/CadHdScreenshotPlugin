using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
using CadHdScreenshot.Core;

namespace CadHdScreenshot.Plugin
{
	internal static class CustomCommandRegistry
	{
		private const string CommandGroup = "CADHDSCREENSHOT";

		private static readonly CommandCallback Callback = ShowScreenshotWindow;

		private static string _registeredCommand;

		public static string RegisteredCommand => _registeredCommand;

		public static void Initialize()
		{
			ScreenshotSettings screenshotSettings = FileSettingsStore.CreateForCurrentUser().Load();
			try
			{
				Register(screenshotSettings.CustomCommand);
			}
			catch (System.Exception exception)
			{
				CaptureDiagnostics.TryWrite("注册自定义命令", exception);
				if (!string.Equals(screenshotSettings.CustomCommand, "CADHDJT", StringComparison.OrdinalIgnoreCase))
				{
					Register("CADHDJT");
				}
			}
		}

		public static string Register(string commandName)
		{
			string text = ScreenshotSettings.NormalizeCommandName(commandName);
			if (string.Equals(text, _registeredCommand, StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
			Utils.AddCommand("CADHDSCREENSHOT", text, text, CommandFlags.Session, Callback);
			if (!string.IsNullOrEmpty(_registeredCommand))
			{
				Utils.RemoveCommand("CADHDSCREENSHOT", _registeredCommand);
			}
			_registeredCommand = text;
			return text;
		}

		public static void Terminate()
		{
			if (!string.IsNullOrEmpty(_registeredCommand))
			{
				Utils.RemoveCommand("CADHDSCREENSHOT", _registeredCommand);
				_registeredCommand = null;
			}
		}

		private static void ShowScreenshotWindow()
		{
			if (Application.DocumentManager.MdiActiveDocument != null)
			{
				ScreenshotWindowHost.Show();
			}
		}
	}
}
