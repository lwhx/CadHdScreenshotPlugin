using System;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace CadHdScreenshot.Plugin
{
	internal static class WindowSelector
	{
		public static CaptureSelection Select(Editor editor)
		{
			PromptPointResult point = editor.GetPoint("\n指定截图范围的第一个角点：");
			if (point.Status != PromptStatus.OK)
			{
				return null;
			}
			PromptCornerOptions options = new PromptCornerOptions("\n指定截图范围的另一个角点：", point.Value);
			PromptPointResult corner = editor.GetCorner(options);
			if (corner.Status != PromptStatus.OK)
			{
				return null;
			}
			Vector3d vector3d = corner.Value - point.Value;
			if (Math.Abs(vector3d.X) <= Tolerance.Global.EqualPoint || Math.Abs(vector3d.Y) <= Tolerance.Global.EqualPoint)
			{
				editor.WriteMessage("\n截图范围的宽度和高度必须大于零。");
				return null;
			}
			return CaptureSelection.FromWindow(point.Value, corner.Value);
		}
	}
}
