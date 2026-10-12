using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using CadHdScreenshot.Plugin;

[assembly: CommandClass(typeof(CadHdScreenshot.AutoCAD.Tests.MirrorRegression))]
namespace CadHdScreenshot.AutoCAD.Tests
{
    public class MirrorRegression
    {
#if BASELINE
        [CommandMethod("CADMIRRORRED", CommandFlags.Modal | CommandFlags.UsePickSet)]
#else
        [CommandMethod("CADMIRRORGREEN", CommandFlags.Modal | CommandFlags.UsePickSet)]
#endif
        public static void Run()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            var db = document.Database;
            var log = new StringBuilder();
            var selected = document.Editor.SelectImplied();
            int passed = 0, failed = 0;
            Action<string, Action> test = (name, run) =>
            {
                try { run(); passed++; log.AppendLine("PASS " + name); }
                catch (System.Exception e) { failed++; log.AppendLine("FAIL " + name + ": " + e); }
            };
            try
            {
                // Every fixture is inside an outer transaction that is always aborted.
                // The production command's nested transaction cannot persist test copies.
                ObjectId[] sourceIds;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    sourceIds = space.Cast<ObjectId>().Where(id => tr.GetObject(id, OpenMode.ForRead) is BlockReference).ToArray();
                }
                foreach (ObjectId sourceId in sourceIds)
                {
                    test("actual block " + sourceId.Handle + " readable, geometry and sibling preserved", () =>
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var original = (BlockReference)tr.GetObject(sourceId, OpenMode.ForRead);
                            var originalDefinition = original.BlockTableRecord;
                            var originalTransform = original.BlockTransform;
                            var copy = (BlockReference)original.Clone();
                            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            var copyId = space.AppendEntity(copy);
                            tr.AddNewlyCreatedDBObject(copy, true);
                            var before = Geometry(copy);
                            var nestedBefore = NestedData(copy, tr);
                            document.Editor.SetImpliedSelection(new[] { copyId });
                            log.AppendLine(MirroredBlockTextService.FixSelected(document));
                            Require(!copy.IsErased && copy.ObjectId == copyId, "block identity was lost");
                            Require(original.BlockTableRecord == originalDefinition && original.BlockTransform.Equals(originalTransform), "original sibling changed");
                            AssertGeometry(before, Geometry(copy));
                            Require(nestedBefore == NestedData(copy, tr), "dynamic block data changed");
                            AssertReadable(copy, tr);
                        }
                    });
                }
                foreach (var scale in new[] { new Scale3d(-1,1,1), new Scale3d(1,-1,1) })
                foreach (double rotation in new[] { 0.0, Math.PI / 6 })
                foreach (bool nestedCase in new[] { false, true })
                {
                    test("synthetic mirror " + scale + " rotation " + rotation + " nested=" + nestedCase, () =>
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                            var def = new BlockTableRecord { Name = "*U" };
                            var defId = bt.Add(def); tr.AddNewlyCreatedDBObject(def, true);
                            var dims = new Dimension[] {
                                new RotatedDimension(0, new Point3d(0,0,0), new Point3d(77,0,0), new Point3d(0,20,0), "<>cm", db.Dimstyle),
                                new RotatedDimension(Math.PI/2, new Point3d(77,0,0), new Point3d(77,73,0), new Point3d(95,0,0), "<>cm", db.Dimstyle),
                                new AlignedDimension(new Point3d(0,0,0), new Point3d(77,73,0), new Point3d(30,60,0), "<>", db.Dimstyle)
                            };
                            foreach (var dim in dims) { def.AppendEntity(dim); tr.AddNewlyCreatedDBObject(dim, true); dim.RecomputeDimensionBlock(true); }
                            var label = new DBText { TextString = "J1", Height = 5, TextStyleId = db.Textstyle, Position = new Point3d(30,30,0) };
                            def.AppendEntity(label); tr.AddNewlyCreatedDBObject(label, true);
                            var multiline = new MText { Contents = "150cm", TextHeight = 4, TextStyleId = db.Textstyle, Location = new Point3d(50,50,0) };
                            def.AppendEntity(multiline); tr.AddNewlyCreatedDBObject(multiline, true);
                            var br = new BlockReference(Point3d.Origin, defId) { ScaleFactors = scale, Rotation = rotation };
                            if (nestedCase)
                            {
                                var outer = new BlockTableRecord { Name = "*U" };
                                var outerId = bt.Add(outer); tr.AddNewlyCreatedDBObject(outer, true);
                                outer.AppendEntity(br); tr.AddNewlyCreatedDBObject(br, true);
                                br = new BlockReference(new Point3d(200,100,0), outerId);
                            }
                            var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                            var id = space.AppendEntity(br); tr.AddNewlyCreatedDBObject(br, true);
                            var before = Geometry(br);
                            document.Editor.SetImpliedSelection(new[] { id });
                            log.AppendLine(MirroredBlockTextService.FixSelected(document));
                            AssertReadable(br, tr);
                            AssertGeometry(before, Geometry(br));
                            // Repeating the command must not keep creating definitions.
                            var firstDef = br.BlockTableRecord;
                            document.Editor.SetImpliedSelection(new[] { id });
                            MirroredBlockTextService.FixSelected(document);
                            Require(br.BlockTableRecord == firstDef, "repeat repair is not idempotent");
                        }
                    });
                }
            }
            finally
            {
                document.Editor.SetImpliedSelection(selected.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK ? selected.Value.GetObjectIds() : new ObjectId[0]);
                document.Editor.Regen();
                log.AppendLine("RESULT " + passed + " passed, " + failed + " failed");
                string path = Path.Combine(Path.GetDirectoryName(typeof(MirrorRegression).Assembly.Location), "regression.log");
                File.WriteAllText(path, log.ToString());
                document.Editor.WriteMessage("\nMIRROR_REGRESSION: " + passed + " passed, " + failed + " failed");
            }
        }

        private static List<Point3d> Geometry(BlockReference block)
        {
            var points = new List<Point3d>();
            var parts = new DBObjectCollection();
            try
            {
                block.Explode(parts);
                foreach (DBObject obj in parts)
                {
                    if (obj is BlockReference nested) points.AddRange(Geometry(nested));
                    else if (obj is RotatedDimension r) { points.Add(r.XLine1Point); points.Add(r.XLine2Point); points.Add(r.DimLinePoint); }
                    else if (obj is AlignedDimension a) { points.Add(a.XLine1Point); points.Add(a.XLine2Point); points.Add(a.DimLinePoint); }
                    else if (obj is Polyline p) for (int i = 0; i < p.NumberOfVertices; i++) points.Add(p.GetPoint3dAt(i));
                    else if (obj is Curve curve) { points.Add(curve.StartPoint); points.Add(curve.EndPoint); points.Add(curve.GetPointAtParameter((curve.StartParam + curve.EndParam) / 2)); }
                }
            }
            finally { foreach (DBObject obj in parts) obj.Dispose(); }
            return points;
        }
        private static void AssertGeometry(List<Point3d> before, List<Point3d> after)
        {
            Require(before.Count == after.Count, "geometry count changed");
            for (int i=0;i<before.Count;i++) Require(before[i].DistanceTo(after[i]) < 1e-7, "dimension/outline point moved: " + before[i] + " -> " + after[i]);
        }
        private static string NestedData(BlockReference block, Transaction tr)
        {
            var result = new StringBuilder();
            if (block.IsDynamicBlock)
            {
                result.Append(block.DynamicBlockTableRecord.Handle);
                foreach (DynamicBlockReferenceProperty prop in block.DynamicBlockReferencePropertyCollection)
                    result.Append("|").Append(prop.PropertyName).Append("=").Append(prop.Value);
            }
            var def = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
            foreach (ObjectId id in def)
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference nested) result.Append(NestedData(nested,tr));
            return result.ToString();
        }
        private static void AssertReadable(BlockReference block, Transaction tr)
        {
            int count = CheckText(block.BlockTableRecord, block.BlockTransform, tr);
            Require(count > 0, "text glyphs absent");
        }
        private static int CheckText(ObjectId definition, Matrix3d transform, Transaction tr)
        {
            int count = 0;
            foreach (ObjectId id in (BlockTableRecord)tr.GetObject(definition, OpenMode.ForRead))
            {
                var entity = (Entity)tr.GetObject(id, OpenMode.ForRead);
                if (entity is BlockReference nested) count += CheckText(nested.BlockTableRecord, transform * nested.BlockTransform, tr);
                else if (entity is Dimension dim)
                {
                    var glyphs = new DBObjectCollection();
                    try { dim.Explode(glyphs); foreach (DBObject obj in glyphs) if (obj is MText text) { CheckGlyph(text, transform); count++; } }
                    finally { foreach (DBObject obj in glyphs) obj.Dispose(); }
                }
                else if (entity is MText mt) { CheckGlyph(mt, transform); count++; }
                else if (entity is DBText dt)
                {
                    using (var copy = (DBText)dt.Clone())
                    {
                        copy.TransformBy(transform);
                        Require(!copy.IsMirroredInX && !copy.IsMirroredInY && copy.Normal.Z > .99, "DBText mirrored: " + dt.TextString);
                        CheckDirection(new Vector3d(Math.Cos(copy.Rotation), Math.Sin(copy.Rotation), 0));
                    }
                    count++;
                }
            }
            return count;
        }
        private static void CheckGlyph(MText text, Matrix3d transform)
        {
            var x = text.Direction.TransformBy(transform).GetNormal();
            var y = text.Normal.CrossProduct(text.Direction).TransformBy(transform).GetNormal();
            Require(x.CrossProduct(y).Z > .99, "text glyph is reflected: " + text.Contents);
            CheckDirection(x);
        }
        private static void CheckDirection(Vector3d direction)
        {
            Require(direction.X > 1e-8 || (Math.Abs(direction.X) <= 1e-8 && direction.Y > 0), "text upside down: " + direction);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
