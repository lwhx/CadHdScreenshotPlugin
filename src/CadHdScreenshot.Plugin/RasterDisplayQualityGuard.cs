using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.EditorInput;

namespace CadHdScreenshot.Plugin
{
	internal sealed class RasterDisplayQualityGuard : IDisposable
	{
		private readonly object _originalValue;

		private readonly bool _restore;

		private RasterDisplayQualityGuard(object originalValue, bool restore)
		{
			_originalValue = originalValue;
			_restore = restore;
		}

		public static RasterDisplayQualityGuard Enable(Editor editor)
		{
			object obj = null;
			bool flag = false;
			try
			{
				obj = Application.GetSystemVariable("IMAGEQUALITY");
				flag = Convert.ToInt32(obj) != 1;
				if (flag)
				{
					Application.SetSystemVariable("IMAGEQUALITY", 1);
				}
				editor.Regen();
				return new RasterDisplayQualityGuard(obj, flag);
			}
			catch
			{
				if (flag)
				{
					TryRestore(obj);
				}
				return new RasterDisplayQualityGuard(null, restore: false);
			}
		}

		public void Dispose()
		{
			if (_restore)
			{
				TryRestore(_originalValue);
			}
		}

		private static void TryRestore(object value)
		{
			try
			{
				Application.SetSystemVariable("IMAGEQUALITY", value);
			}
			catch
			{
			}
		}
	}
}
