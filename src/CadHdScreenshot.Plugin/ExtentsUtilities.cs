using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadHdScreenshot.Plugin
{
	internal static class ExtentsUtilities
	{
		public static Extents3d Expand(Extents3d extents, double ratio)
		{
			if (ratio < 0.0)
			{
				throw new ArgumentOutOfRangeException("ratio");
			}
			Vector3d vector3d = extents.MaxPoint - extents.MinPoint;
			double num = Math.Max(Math.Abs(vector3d.X) * ratio, Tolerance.Global.EqualPoint);
			double num2 = Math.Max(Math.Abs(vector3d.Y) * ratio, Tolerance.Global.EqualPoint);
			double num3 = Math.Max(Math.Abs(vector3d.Z) * ratio, Tolerance.Global.EqualPoint);
			return new Extents3d(new Point3d(extents.MinPoint.X - num, extents.MinPoint.Y - num2, extents.MinPoint.Z - num3), new Point3d(extents.MaxPoint.X + num, extents.MaxPoint.Y + num2, extents.MaxPoint.Z + num3));
		}

		public static Extents3d FromPoints(Point3d first, Point3d second)
		{
			return new Extents3d(new Point3d(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Min(first.Z, second.Z)), new Point3d(Math.Max(first.X, second.X), Math.Max(first.Y, second.Y), Math.Max(first.Z, second.Z)));
		}
	}
}
