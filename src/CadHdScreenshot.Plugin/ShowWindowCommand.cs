using System;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices.Core;

namespace CadHdScreenshot.Plugin
{
	internal sealed class ShowWindowCommand : ICommand
	{
		public event EventHandler CanExecuteChanged
		{
			add
			{
				CommandManager.RequerySuggested += value;
			}
			remove
			{
				CommandManager.RequerySuggested -= value;
			}
		}

		public bool CanExecute(object parameter)
		{
			return Application.DocumentManager.MdiActiveDocument != null;
		}

		public void Execute(object parameter)
		{
			ScreenshotWindowHost.Show();
		}
	}
}
