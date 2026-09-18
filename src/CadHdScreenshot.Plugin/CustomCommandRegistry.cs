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
            ScreenshotSettings settings = FileSettingsStore.CreateForCurrentUser().Load();
            try
            {
                Register(settings.CustomCommand);
            }
            catch (System.Exception exception)
            {
                CaptureDiagnostics.TryWrite("注册自定义命令", exception);
                if (!string.Equals(settings.CustomCommand, "CADHDJT", StringComparison.OrdinalIgnoreCase))
                {
                    Register("CADHDJT");
                }
            }
        }

        public static string Register(string commandName)
        {
            string normalized = ScreenshotSettings.NormalizeCommandName(commandName);
            if (string.Equals(normalized, _registeredCommand, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            Utils.AddCommand(
                CommandGroup,
                normalized,
                normalized,
                CommandFlags.Session,
                Callback);

            if (!string.IsNullOrEmpty(_registeredCommand))
            {
                Utils.RemoveCommand(CommandGroup, _registeredCommand);
            }

            _registeredCommand = normalized;
            return normalized;
        }

        public static void Terminate()
        {
            if (string.IsNullOrEmpty(_registeredCommand))
            {
                return;
            }

            Utils.RemoveCommand(CommandGroup, _registeredCommand);
            _registeredCommand = null;
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
