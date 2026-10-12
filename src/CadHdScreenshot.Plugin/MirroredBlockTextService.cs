using System;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace CadHdScreenshot.Plugin
{
    internal sealed class MirroredBlockTextService
    {
        public async Task<string> FixAsync()
        {
            DocumentCollection documents = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
            Document document = documents.MdiActiveDocument;
            if (document == null)
            {
                return "没有活动图纸";
            }

            return await CommandContextRunner.ExecuteAsync(async delegate(Func<object, Task> callback, object data)
            {
                await documents.ExecuteInCommandContextAsync(callback, data);
            }, () => FixSelected(document));
        }

        internal static string FixSelected(Document document)
        {
            Editor editor = document.Editor;
            PromptSelectionResult selection = editor.SelectImplied();
            if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
            {
                PromptSelectionOptions options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择文字反向的镜像块："
                };
                SelectionFilter filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                });
                selection = editor.GetSelection(options, filter);
            }
            else
            {
                editor.SetImpliedSelection(new ObjectId[0]);
            }

            if (selection.Status != PromptStatus.OK)
            {
                return "已取消镜像块文字校正";
            }

            int fixedCount = 0;
            int notMirroredCount = 0;
            int unsupportedCount = 0;
            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in selection.Value.GetObjectIds())
                {
                    BlockReference blockReference = transaction.GetObject(id, OpenMode.ForRead, openErased: false) as BlockReference;
                    if (blockReference == null)
                    {
                        continue;
                    }

                    BlockTableRecord sourceDefinition = transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead, openErased: false) as BlockTableRecord;
                    if (!CanNormalize(blockReference, sourceDefinition) || !CanNormalizeTextTree(blockReference, transaction, 0))
                    {
                        unsupportedCount++;
                        continue;
                    }

                    if (!IsMirrored(blockReference.BlockTransform) && !HasReversedText(blockReference.BlockTableRecord, blockReference.BlockTransform, transaction, 0))
                    {
                        notMirroredCount++;
                        continue;
                    }

                    if (NormalizeInstance(document.Database, blockTable, blockReference, transaction))
                    {
                        fixedCount++;
                    }
                    else
                    {
                        unsupportedCount++;
                    }
                }
                transaction.Commit();
            }
            bool refreshWarning = false;
            if (fixedCount > 0)
            {
                try
                {
                    editor.Regen();
                }
                catch
                {
                    refreshWarning = true;
                }
            }

            string message = fixedCount > 0 ? "已校正 " + fixedCount + " 个块的文字方向" : "未校正任何块";
            if (notMirroredCount > 0)
            {
                message += "，" + notMirroredCount + " 个块未检测到需要校正的方向";
            }
            if (unsupportedCount > 0)
            {
                message += "，跳过 " + unsupportedCount + " 个含不支持数据的块";
            }
            if (refreshWarning)
            {
                message += "；显示刷新未完成，请执行 REGEN";
            }
            try
            {
                editor.WriteMessage("\n" + message + "。");
            }
            catch
            {
            }
            return message;
        }

        private static bool CanNormalize(BlockReference reference, BlockTableRecord definition)
        {
            return definition != null
                && !definition.IsLayout
                && !definition.IsFromExternalReference
                && !reference.IsDynamicBlock
                && !(reference is MInsertBlock)
                && reference.Annotative != AnnotativeStates.True
                && reference.ExtensionDictionary.IsNull
                && reference.AttributeCollection.Count == 0
                && !definition.HasAttributeDefinitions;
        }

        private static bool NormalizeInstance(Database database, BlockTable blockTable, BlockReference source, Transaction transaction)
        {
            var definition = (BlockTableRecord)transaction.GetObject(source.BlockTableRecord, OpenMode.ForRead);
            var ids = new ObjectIdCollection();
            foreach (ObjectId id in definition) ids.Add(id);
            if (ids.Count == 0) return false;

            Point3d insertionPoint = source.Position;
            Matrix3d bakedTransform = Matrix3d.Displacement(insertionPoint.GetVectorTo(Point3d.Origin)) * source.BlockTransform;
            if (!blockTable.IsWriteEnabled) blockTable.UpgradeOpen();
            var replacement = new BlockTableRecord { Name = "*U", Origin = Point3d.Origin };
            try { blockTable.Add(replacement); }
            finally { if (replacement.ObjectId.IsNull) replacement.Dispose(); }
            transaction.AddNewlyCreatedDBObject(replacement, true);

            // Deep cloning retains owned data (including dynamic block state) and
            // translates object references. Never explode text-free nested blocks.
            var mapping = new IdMapping();
            database.DeepCloneObjects(ids, replacement.ObjectId, mapping, false);
            foreach (ObjectId originalId in ids)
            {
                IdPair pair = mapping[originalId];
                if (!pair.IsCloned || pair.Value.IsNull)
                    throw new InvalidOperationException("块内容复制不完整，本次修改已回滚。");
                var entity = (Entity)transaction.GetObject(pair.Value, OpenMode.ForWrite);
                PrepareReadableEntity(entity, bakedTransform);
                if (entity is DBText text) MakeTextReadable(text, database);
                if (entity is Dimension dimension) dimension.RecomputeDimensionBlock(true);
                if (entity is BlockReference nested && ContainsText(nested.BlockTableRecord, transaction, 0))
                {
                    if (!NormalizeInstance(database, blockTable, nested, transaction))
                        throw new InvalidOperationException("嵌套文字块无法校正，本次修改已回滚。");
                }
            }
            if (!source.IsWriteEnabled) source.UpgradeOpen();
            source.BlockTableRecord = replacement.ObjectId;
            source.BlockTransform = Matrix3d.Displacement(Point3d.Origin.GetVectorTo(insertionPoint));
            return true;
        }

        private static bool CanNormalizeTextTree(BlockReference reference, Transaction transaction, int depth)
        {
            if (depth >= 32) return false;
            BlockTableRecord definition = transaction.GetObject(reference.BlockTableRecord, OpenMode.ForRead, openErased: false) as BlockTableRecord;
            if (!CanNormalize(reference, definition)) return false;
            foreach (ObjectId id in definition)
            {
                if (transaction.GetObject(id, OpenMode.ForRead) is BlockReference child
                    && ContainsText(child.BlockTableRecord, transaction, depth + 1)
                    && !CanNormalizeTextTree(child, transaction, depth + 1)) return false;
            }
            return true;
        }

        private static bool ContainsText(ObjectId definitionId, Transaction transaction, int depth)
        {
            if (depth >= 32) return true;
            var definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead);
            if (definition.IsFromExternalReference) return true;
            foreach (ObjectId id in definition)
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead);
                if (entity is Dimension || entity is DBText || entity is MText) return true;
                if (entity is BlockReference child && ContainsText(child.BlockTableRecord, transaction, depth + 1)) return true;
            }
            return false;
        }

        private static void MakeTextReadable(DBText text, Database database)
        {
            if (string.IsNullOrWhiteSpace(text.TextString)) return;
            Point3d center = TextCenter(text);
            text.IsMirroredInX = false;
            text.IsMirroredInY = false;
            if (Math.Abs(text.Normal.Z) > 0.999999)
            {
                Vector3d direction = new Vector3d(Math.Cos(text.Rotation), Math.Sin(text.Rotation), 0).TransformBy(Matrix3d.PlaneToWorld(text.Normal));
                text.Normal = Vector3d.ZAxis;
                text.Rotation = ReadableAngle(Math.Atan2(direction.Y, direction.X));
            }
            text.AdjustAlignment(database);
            text.TransformBy(Matrix3d.Displacement(TextCenter(text).GetVectorTo(center)));
        }

        private static Point3d TextCenter(Entity text)
        {
            Extents3d bounds = text.GeometricExtents;
            return bounds.MinPoint + (bounds.MaxPoint - bounds.MinPoint) * 0.5;
        }

        private static void PrepareReadableEntity(Entity entity, Matrix3d toLocal)
        {
            entity.TransformBy(toLocal);
            if (entity is MText mt && !string.IsNullOrWhiteSpace(mt.Contents) && Math.Abs(mt.Normal.Z) > 0.999999)
            {
                Point3d center = TextCenter(mt);
                double angle = ReadableAngle(Math.Atan2(mt.Direction.Y, mt.Direction.X));
                mt.Normal = Vector3d.ZAxis;
                mt.Direction = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
                mt.TransformBy(Matrix3d.Displacement(TextCenter(mt).GetVectorTo(center)));
            }
            Dimension dimension = entity as Dimension;
            if (dimension != null && dimension.Normal.IsEqualTo(Vector3d.ZAxis))
            {
                // TransformBy bakes the mirror into HorizontalRotation. Recomputing with
                // that mirrored reference axis keeps the annotation upside down.
                // Zero TextRotation means automatic orientation; preserve that mode.
                double textRotation = dimension.TextRotation;
                if (Math.Abs(textRotation) > 1E-10)
                {
                    textRotation = ReadableAngle(textRotation - dimension.HorizontalRotation);
                }
                dimension.HorizontalRotation = 0;
                dimension.TextRotation = textRotation;
            }
        }

        private static bool HasReversedText(ObjectId definitionId, Matrix3d transform, Transaction transaction, int depth)
        {
            if (depth >= 32) return false;
            var definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead);
            foreach (ObjectId id in definition)
            {
                var entity = transaction.GetObject(id, OpenMode.ForRead);
                if (entity is BlockReference child && HasReversedText(child.BlockTableRecord, transform * child.BlockTransform, transaction, depth + 1)) return true;
                if (entity is DBText dbText)
                {
                    using (var text = (DBText)dbText.Clone())
                    {
                        text.TransformBy(transform);
                        if (text.IsMirroredInX || text.IsMirroredInY || text.Normal.Z < -0.99 || IsReverseDirection(new Vector3d(Math.Cos(text.Rotation), Math.Sin(text.Rotation), 0))) return true;
                    }
                }
                if (entity is MText mt && IsReverseGlyph(mt, transform)) return true;
                if (!(entity is Dimension dimension)) continue;
                var glyphs = new DBObjectCollection();
                try
                {
                    dimension.Explode(glyphs);
                    foreach (DBObject glyph in glyphs)
                        if (glyph is MText text && IsReverseGlyph(text, transform)) return true;
                }
                finally { DisposeAll(glyphs); }
            }
            return false;
        }

        private static bool IsReverseGlyph(MText text, Matrix3d transform)
        {
            Vector3d x = text.Direction.TransformBy(transform).GetNormal();
            Vector3d y = text.Normal.CrossProduct(text.Direction).TransformBy(transform).GetNormal();
            return x.CrossProduct(y).Z < -0.99 || IsReverseDirection(x);
        }

        private static bool IsReverseDirection(Vector3d direction)
        {
            return direction.X < -1E-8 || (Math.Abs(direction.X) <= 1E-8 && direction.Y < -1E-8);
        }

        private static double ReadableAngle(double angle)
        {
            angle %= Math.PI * 2;
            if (angle < 0) angle += Math.PI * 2;
            if (angle > Math.PI / 2 + 1E-10 && angle <= Math.PI * 1.5 + 1E-10) angle -= Math.PI;
            return angle;
        }

        private static bool IsMirrored(Matrix3d transform)
        {
            return transform.GetDeterminant() < -1E-10;
        }

        private static void DisposeAll(DBObjectCollection objects)
        {
            foreach (DBObject item in objects)
            {
                if (item.ObjectId.IsNull)
                {
                    item.Dispose();
                }
            }
        }
    }
}
