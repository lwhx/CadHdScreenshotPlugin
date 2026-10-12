using System;
using System.Drawing;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

namespace CadHdScreenshot.Plugin
{
	internal sealed class ModelSpaceBackgroundGuard : IDisposable
	{
		private readonly Editor _editor;

		private object _displayPreferences;

		private object _originalColor;

		private ModelSpaceBackgroundGuard(Editor editor, object displayPreferences, object originalColor)
		{
			_editor = editor;
			_displayPreferences = displayPreferences;
			_originalColor = originalColor;
		}

		public static ModelSpaceBackgroundGuard SetWhite(Editor editor)
		{
			if (editor == null)
			{
				throw new ArgumentNullException("editor");
			}
			try
			{
				object property = GetProperty(Application.Preferences, "Display");
				object property2 = GetProperty(property, "GraphicsWinModelBackgrndColor");
				SetProperty(property, "GraphicsWinModelBackgrndColor", ColorTranslator.ToOle(Color.White));
				editor.Regen();
				return new ModelSpaceBackgroundGuard(editor, property, property2);
			}
			catch (Exception innerException)
			{
				throw new InvalidOperationException("无法临时切换 CAD 模型空间背景色。", innerException);
			}
		}

		public void Dispose()
		{
			if (_displayPreferences == null)
			{
				return;
			}
			try
			{
				SetProperty(_displayPreferences, "GraphicsWinModelBackgrndColor", _originalColor);
				_editor.Regen();
			}
			finally
			{
				_displayPreferences = null;
				_originalColor = null;
			}
		}

		private static object GetProperty(object target, string name)
		{
			if (target == null)
			{
				throw new InvalidOperationException("AutoCAD 未返回显示首选项。");
			}
			return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
		}

		private static void SetProperty(object target, string name, object value)
		{
			target.GetType().InvokeMember(name, BindingFlags.SetProperty, null, target, new object[1] { value });
		}
	}
}
