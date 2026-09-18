using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadHdScreenshot.Plugin
{
    internal sealed class CaptureSelection
    {
        public CaptureSelection(Extents3d extents, ObjectId[] objectIds)
            : this(extents, objectIds, null, null)
        {
        }

        private CaptureSelection(
            Extents3d extents,
            ObjectId[] objectIds,
            Point3d? firstWindowPoint,
            Point3d? secondWindowPoint)
        {
            Extents = extents;
            ObjectIds = objectIds ?? new ObjectId[0];
            FirstWindowPoint = firstWindowPoint;
            SecondWindowPoint = secondWindowPoint;
        }

        public Extents3d Extents { get; }

        public ObjectId[] ObjectIds { get; }

        public bool RenderEntireModelSpace => ObjectIds.Length == 0;

        public bool HasWindowCorners => FirstWindowPoint.HasValue && SecondWindowPoint.HasValue;

        private Point3d? FirstWindowPoint { get; }

        private Point3d? SecondWindowPoint { get; }

        public static CaptureSelection FromWindow(Point3d first, Point3d second)
        {
            return new CaptureSelection(
                ExtentsUtilities.FromPoints(first, second),
                objectIds: null,
                firstWindowPoint: first,
                secondWindowPoint: second);
        }

        public Extents3d GetDisplayExtents(Matrix3d worldToDisplay)
        {
            if (HasWindowCorners)
            {
                return ExtentsUtilities.FromPoints(
                    FirstWindowPoint.Value.TransformBy(worldToDisplay),
                    SecondWindowPoint.Value.TransformBy(worldToDisplay));
            }

            Extents3d displayExtents = Extents;
            displayExtents.TransformBy(worldToDisplay);
            return displayExtents;
        }
    }
}
