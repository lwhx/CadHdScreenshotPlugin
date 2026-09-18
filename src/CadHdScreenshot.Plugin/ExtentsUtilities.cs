using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadHdScreenshot.Plugin
{
    internal static class ExtentsUtilities
    {
        public static Extents3d Expand(Extents3d extents, double ratio)
        {
            if (ratio < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ratio));
            }

            Vector3d size = extents.MaxPoint - extents.MinPoint;
            double x = Math.Max(Math.Abs(size.X) * ratio, Tolerance.Global.EqualPoint);
            double y = Math.Max(Math.Abs(size.Y) * ratio, Tolerance.Global.EqualPoint);
            double z = Math.Max(Math.Abs(size.Z) * ratio, Tolerance.Global.EqualPoint);

            return new Extents3d(
                new Point3d(extents.MinPoint.X - x, extents.MinPoint.Y - y, extents.MinPoint.Z - z),
                new Point3d(extents.MaxPoint.X + x, extents.MaxPoint.Y + y, extents.MaxPoint.Z + z));
        }

        public static Extents3d FromPoints(Point3d first, Point3d second)
        {
            return new Extents3d(
                new Point3d(
                    Math.Min(first.X, second.X),
                    Math.Min(first.Y, second.Y),
                    Math.Min(first.Z, second.Z)),
                new Point3d(
                    Math.Max(first.X, second.X),
                    Math.Max(first.Y, second.Y),
                    Math.Max(first.Z, second.Z)));
        }
    }
}
