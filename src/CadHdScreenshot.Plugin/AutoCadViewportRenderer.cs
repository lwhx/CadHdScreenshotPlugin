using System;
using System.Drawing;
using System.Drawing.Imaging;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadHdScreenshot.Core;

namespace CadHdScreenshot.Plugin
{
	internal sealed class AutoCadViewportRenderer
	{
		private const double EdgeMargin = 1.01;

		public Bitmap Render(Document document, CaptureSelection selection, PixelSize targetSize, bool useWhiteBackground)
		{
			if (document == null)
			{
				throw new ArgumentNullException("document");
			}
			if (useWhiteBackground)
			{
				using (ModelSpaceBackgroundGuard.SetWhite(document.Editor))
				{
					return Capture(document, selection, targetSize);
				}
			}
			return Capture(document, selection, targetSize);
		}

		private static Bitmap Capture(Document document, CaptureSelection selection, PixelSize targetSize)
		{
			SetViewToSelection(document, selection);
			document.Editor.Regen();
			using (ViewTableRecord view = document.Editor.GetCurrentView())
			{
				Matrix3d worldToDisplay = CreateWorldToDisplay(view);
				Extents3d displayExtents = selection.GetDisplayExtents(worldToDisplay);
				RectangleF rectangleF = CalculateNormalizedCrop(view, displayExtents);
				PixelSize pixelSize = CalculateCaptureSize(targetSize, rectangleF);
				using (Bitmap bitmap = checked(document.CapturePreviewImage((uint)pixelSize.Width, (uint)pixelSize.Height)))
				{
					if (bitmap == null)
					{
						throw new InvalidOperationException("AutoCAD 未返回视口截图。");
					}
					Rectangle rect = ToPixelRectangle(rectangleF, bitmap.Size);
					return bitmap.Clone(rect, PixelFormat.Format32bppArgb);
				}
			}
		}

		private static void SetViewToSelection(Document document, CaptureSelection selection)
		{
			using (ViewTableRecord viewTableRecord = document.Editor.GetCurrentView())
			{
				Matrix3d worldToDisplay = CreateWorldToDisplay(viewTableRecord);
				Extents3d displayExtents = selection.GetDisplayExtents(worldToDisplay);
				double num = displayExtents.MaxPoint.X - displayExtents.MinPoint.X;
				double num2 = displayExtents.MaxPoint.Y - displayExtents.MinPoint.Y;
				if (num <= Tolerance.Global.EqualPoint || num2 <= Tolerance.Global.EqualPoint)
				{
					throw new InvalidOperationException("截图范围的宽度和高度必须大于零。");
				}
				double aspectRatio = ScreenPixelMapper.MeasureViewport().AspectRatio;
				if (num / num2 > aspectRatio)
				{
					num2 = num / aspectRatio;
				}
				else
				{
					num = num2 * aspectRatio;
				}
				Point3d point3d = new Point3d((displayExtents.MinPoint.X + displayExtents.MaxPoint.X) / 2.0, (displayExtents.MinPoint.Y + displayExtents.MaxPoint.Y) / 2.0, 0.0);
				viewTableRecord.CenterPoint = new Point2d(point3d.X, point3d.Y);
				viewTableRecord.Width = num * 1.01;
				viewTableRecord.Height = num2 * 1.01;
				document.Editor.SetCurrentView(viewTableRecord);
			}
		}

		private static Matrix3d CreateWorldToDisplay(ViewTableRecord view)
		{
			Matrix3d matrix3d = Matrix3d.PlaneToWorld(view.ViewDirection);
			matrix3d = Matrix3d.Displacement(view.Target - Point3d.Origin) * matrix3d;
			return (Matrix3d.Rotation(0.0 - view.ViewTwist, view.ViewDirection, view.Target) * matrix3d).Inverse();
		}

		private static RectangleF CalculateNormalizedCrop(ViewTableRecord view, Extents3d extents)
		{
			double num = view.CenterPoint.X - view.Width / 2.0;
			double num2 = view.CenterPoint.Y + view.Height / 2.0;
			float num3 = Clamp01((extents.MinPoint.X - num) / view.Width);
			float num4 = Clamp01((num2 - extents.MaxPoint.Y) / view.Height);
			float num5 = Clamp01((extents.MaxPoint.X - num) / view.Width);
			float num6 = Clamp01((num2 - extents.MinPoint.Y) / view.Height);
			if (num5 <= num3 || num6 <= num4)
			{
				throw new InvalidOperationException("无法把截图范围映射到当前视口。");
			}
			return RectangleF.FromLTRB(num3, num4, num5, num6);
		}

		private static PixelSize CalculateCaptureSize(PixelSize targetSize, RectangleF crop)
		{
			return checked(new PixelSize(Math.Max(targetSize.Width, (int)Math.Ceiling((float)targetSize.Width / crop.Width)), Math.Max(targetSize.Height, (int)Math.Ceiling((float)targetSize.Height / crop.Height))));
		}

		private static Rectangle ToPixelRectangle(RectangleF normalized, Size bitmapSize)
		{
			int num = Math.Max(0, Math.Min(bitmapSize.Width - 1, (int)Math.Floor(normalized.Left * (float)bitmapSize.Width)));
			int num2 = Math.Max(0, Math.Min(bitmapSize.Height - 1, (int)Math.Floor(normalized.Top * (float)bitmapSize.Height)));
			int right = Math.Max(num + 1, Math.Min(bitmapSize.Width, (int)Math.Ceiling(normalized.Right * (float)bitmapSize.Width)));
			int bottom = Math.Max(num2 + 1, Math.Min(bitmapSize.Height, (int)Math.Ceiling(normalized.Bottom * (float)bitmapSize.Height)));
			return Rectangle.FromLTRB(num, num2, right, bottom);
		}

		private static float Clamp01(double value)
		{
			return (float)Math.Max(0.0, Math.Min(1.0, value));
		}
	}
}
