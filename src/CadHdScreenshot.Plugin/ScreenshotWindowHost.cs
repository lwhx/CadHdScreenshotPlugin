using Autodesk.AutoCAD.ApplicationServices.Core;

namespace CadHdScreenshot.Plugin
{
	internal static class ScreenshotWindowHost
	{
		private static ScreenshotWindow _window;

		public static void Show()
		{
			if (_window == null)
			{
				_window = new ScreenshotWindow();
				_window.Closed += delegate
				{
					_window = null;
				};
				Application.ShowModelessWindow(_window);
			}
			else
			{
				_window.Show();
				_window.Activate();
			}
		}

		public static void Close()
		{
			_window?.Close();
			_window = null;
		}

		public static void ShowInputMethod()
		{
			Show();
			_window.SelectInputMethodTab();
		}
	}
}
