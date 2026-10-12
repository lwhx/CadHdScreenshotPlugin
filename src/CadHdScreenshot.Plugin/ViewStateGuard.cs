using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace CadHdScreenshot.Plugin
{
	internal sealed class ViewStateGuard : IDisposable
	{
		private readonly Editor _editor;

		private ViewTableRecord _view;

		public ViewStateGuard(Editor editor)
		{
			_editor = editor ?? throw new ArgumentNullException("editor");
			_view = editor.GetCurrentView();
		}

		public void Dispose()
		{
			if (_view == null)
			{
				return;
			}
			try
			{
				_editor.SetCurrentView(_view);
			}
			finally
			{
				_view.Dispose();
				_view = null;
			}
		}
	}
}
