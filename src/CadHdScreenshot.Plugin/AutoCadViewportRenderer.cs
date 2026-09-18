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

        public Bitmap Render(
            Document document,
            CaptureSelection selection,
            PixelSize targetSize,
            bool useWhiteBackground)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (useWhiteBackground)
            {
                using (ModelSpaceBackgroundGuard background = ModelSpaceBackgroundGuard.SetWhite(document.Editor))
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
                RectangleF normalizedCrop = CalculateNormalizedCrop(view, displayExtents);
                PixelSize captureSize = CalculateCaptureSize(targetSize, normalizedCrop);

                using (Bitmap captured = document.CapturePreviewImage(
                    checked((uint)captureSize.Width),
                    checked((uint)captureSize.Height)))
                {
                    if (captured == null)
                    {
                        throw new InvalidOperationException("AutoCAD 未返回视口截图。");
                    }

                    Rectangle crop = ToPixelRectangle(normalizedCrop, captured.Size);
                    return captured.Clone(crop, PixelFormat.Format32bppArgb);
                }
            }
        }

        private static void SetViewToSelection(Document document, CaptureSelection selection)
        {
            using (ViewTableRecord view = document.Editor.GetCurrentView())
            {
                Matrix3d worldToDisplay = CreateWorldToDisplay(view);
                Extents3d displayExtents = selection.GetDisplayExtents(worldToDisplay);

                double width = displayExtents.MaxPoint.X - displayExtents.MinPoint.X;
                double height = displayExtents.MaxPoint.Y - displayExtents.MinPoint.Y;
                if (width <= Tolerance.Global.EqualPoint || height <= Tolerance.Global.EqualPoint)
                {
                    throw new InvalidOperationException("截图范围的宽度和高度必须大于零。");
                }

                PixelSize viewportSize = ScreenPixelMapper.MeasureViewport();
                double viewportAspect = viewportSize.AspectRatio;
                if (width / height > viewportAspect)
                {
                    height = width / viewportAspect;
                }
                else
                {
                    width = height * viewportAspect;
                }

                Point3d center = new Point3d(
                    (displayExtents.MinPoint.X + displayExtents.MaxPoint.X) / 2,
                    (displayExtents.MinPoint.Y + displayExtents.MaxPoint.Y) / 2,
                    0);

                view.CenterPoint = new Point2d(center.X, center.Y);
                view.Width = width * EdgeMargin;
                view.Height = height * EdgeMargin;
                document.Editor.SetCurrentView(view);
            }
        }

        private static Matrix3d CreateWorldToDisplay(ViewTableRecord view)
        {
            Matrix3d worldToDisplay = Matrix3d.PlaneToWorld(view.ViewDirection);
            worldToDisplay = Matrix3d.Displacement(view.Target - Point3d.Origin) * worldToDisplay;
            worldToDisplay = Matrix3d.Rotation(
                -view.ViewTwist,
                view.ViewDirection,
                view.Target) * worldToDisplay;
            return worldToDisplay.Inverse();
        }

        private static RectangleF CalculateNormalizedCrop(ViewTableRecord view, Extents3d extents)
        {
            double left = view.CenterPoint.X - view.Width / 2;
            double top = view.CenterPoint.Y + view.Height / 2;
            float x = Clamp01((extents.MinPoint.X - left) / view.Width);
            float y = Clamp01((top - extents.MaxPoint.Y) / view.Height);
            float right = Clamp01((extents.MaxPoint.X - left) / view.Width);
            float bottom = Clamp01((top - extents.MinPoint.Y) / view.Height);

            if (right <= x || bottom <= y)
            {
                throw new InvalidOperationException("无法把截图范围映射到当前视口。");
            }

            return RectangleF.FromLTRB(x, y, right, bottom);
        }

        private static PixelSize CalculateCaptureSize(PixelSize targetSize, RectangleF crop)
        {
            return new PixelSize(
                Math.Max(targetSize.Width, checked((int)Math.Ceiling(targetSize.Width / crop.Width))),
                Math.Max(targetSize.Height, checked((int)Math.Ceiling(targetSize.Height / crop.Height))));
        }

        private static Rectangle ToPixelRectangle(RectangleF normalized, Size bitmapSize)
        {
            int left = Math.Max(0, Math.Min(bitmapSize.Width - 1,
                (int)Math.Floor(normalized.Left * bitmapSize.Width)));
            int top = Math.Max(0, Math.Min(bitmapSize.Height - 1,
                (int)Math.Floor(normalized.Top * bitmapSize.Height)));
            int right = Math.Max(left + 1, Math.Min(bitmapSize.Width,
                (int)Math.Ceiling(normalized.Right * bitmapSize.Width)));
            int bottom = Math.Max(top + 1, Math.Min(bitmapSize.Height,
                (int)Math.Ceiling(normalized.Bottom * bitmapSize.Height)));
            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static float Clamp01(double value)
        {
            return (float)Math.Max(0, Math.Min(1, value));
        }
    }
}
