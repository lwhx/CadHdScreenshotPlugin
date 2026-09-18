using System;
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
            PixelSize viewport = MeasureViewport();
            using (ViewTableRecord view = editor.GetCurrentView())
            {
                if (view.Width <= Tolerance.Global.EqualPoint ||
                    view.Height <= Tolerance.Global.EqualPoint)
                {
                    throw new InvalidOperationException("AutoCAD 当前视图尺寸无效。");
                }

                Matrix3d worldToDisplay = Matrix3d.PlaneToWorld(view.ViewDirection);
                worldToDisplay = Matrix3d.Displacement(view.Target - Point3d.Origin) * worldToDisplay;
                worldToDisplay = Matrix3d.Rotation(
                    -view.ViewTwist,
                    view.ViewDirection,
                    view.Target) * worldToDisplay;
                worldToDisplay = worldToDisplay.Inverse();

                Extents3d displayExtents = selection.GetDisplayExtents(worldToDisplay);
                double displayWidth = displayExtents.MaxPoint.X - displayExtents.MinPoint.X;
                double displayHeight = displayExtents.MaxPoint.Y - displayExtents.MinPoint.Y;

                return new PixelSize(
                    Math.Max(1, checked((int)Math.Ceiling(displayWidth / view.Width * viewport.Width))),
                    Math.Max(1, checked((int)Math.Ceiling(displayHeight / view.Height * viewport.Height))));
            }
        }

        public static PixelSize MeasureViewport()
        {
            object value = Autodesk.AutoCAD.ApplicationServices.Core.Application.GetSystemVariable("SCREENSIZE");
            if (!(value is Point2d screenSize))
            {
                throw new InvalidOperationException("AutoCAD 未返回当前绘图区像素尺寸。");
            }

            return new PixelSize(
                Math.Max(1, checked((int)Math.Floor(screenSize.X))),
                Math.Max(1, checked((int)Math.Floor(screenSize.Y))));
        }

    }
}
