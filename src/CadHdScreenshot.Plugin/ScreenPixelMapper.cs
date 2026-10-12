using System;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadHdScreenshot.Core;

namespace CadHdScreenshot.Plugin
{
	internal static class ScreenPixelMapper
	{
		public static PixelSize Measure(Editor editor, CaptureSelection selection)
		{
			PixelSize pixelSize = MeasureViewport();
			using (ViewTableRecord viewTableRecord = editor.GetCurrentView())
			{
				if (viewTableRecord.Width <= Tolerance.Global.EqualPoint || viewTableRecord.Height <= Tolerance.Global.EqualPoint)
				{
					throw new InvalidOperationException("AutoCAD 当前视图尺寸无效。");
				}
				Matrix3d matrix3d = Matrix3d.PlaneToWorld(viewTableRecord.ViewDirection);
				matrix3d = Matrix3d.Displacement(viewTableRecord.Target - Point3d.Origin) * matrix3d;
				matrix3d = (Matrix3d.Rotation(0.0 - viewTableRecord.ViewTwist, viewTableRecord.ViewDirection, viewTableRecord.Target) * matrix3d).Inverse();
				Extents3d displayExtents = selection.GetDisplayExtents(matrix3d);
				double num = displayExtents.MaxPoint.X - displayExtents.MinPoint.X;
				double num2 = displayExtents.MaxPoint.Y - displayExtents.MinPoint.Y;
				return checked(new PixelSize(Math.Max(1, (int)Math.Ceiling(num / viewTableRecord.Width * (double)pixelSize.Width)), Math.Max(1, (int)Math.Ceiling(num2 / viewTableRecord.Height * (double)pixelSize.Height))));
			}
		}

		public static PixelSize MeasureViewport()
		{
			if (!(Application.GetSystemVariable("SCREENSIZE") is Point2d point2d))
			{
				throw new InvalidOperationException("AutoCAD 未返回当前绘图区像素尺寸。");
			}
			return checked(new PixelSize(Math.Max(1, (int)Math.Floor(point2d.X)), Math.Max(1, (int)Math.Floor(point2d.Y))));
		}
	}
}
