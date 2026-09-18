using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace CadHdScreenshot.Plugin
{
    internal sealed class DimensionStyleService
    {
        public async Task<IReadOnlyList<string>> GetStyleNamesAsync()
        {
            DocumentCollection documents = AcadApplication.DocumentManager;
            Document document = documents.MdiActiveDocument;
            if (document == null)
            {
                return new string[0];
            }

            return await CommandContextRunner.ExecuteAsync(
                async (callback, data) => { await documents.ExecuteInCommandContextAsync(callback, data); },
                () => ReadStyleNames(document));
        }

        public async Task<string> ApplyAsync(string styleName, bool selectedBlockOnly)
        {
            DocumentCollection documents = AcadApplication.DocumentManager;
            Document document = documents.MdiActiveDocument;
            if (document == null)
            {
                return "没有活动图纸";
            }

            return await CommandContextRunner.ExecuteAsync(
                async (callback, data) => { await documents.ExecuteInCommandContextAsync(callback, data); },
                () => Apply(document, styleName, selectedBlockOnly));
        }

        private static IReadOnlyList<string> ReadStyleNames(Document document)
        {
            List<string> names = new List<string>();
            using (Transaction transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
            {
                DimStyleTable table = (DimStyleTable)transaction.GetObject(
                    document.Database.DimStyleTableId,
                    OpenMode.ForRead);
                foreach (ObjectId id in table)
                {
                    DimStyleTableRecord style = transaction.GetObject(id, OpenMode.ForRead) as DimStyleTableRecord;
                    if (style != null && !style.IsDependent)
                    {
                        names.Add(style.Name);
                    }
                }
            }

            return names.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        private static string Apply(Document document, string styleName, bool selectedBlockOnly)
        {
            if (string.IsNullOrWhiteSpace(styleName))
            {
                return "请先选择目标标注样式";
            }

            Editor editor = document.Editor;
            PromptSelectionResult selection = editor.SelectImplied();
            if (selection.Status != PromptStatus.OK || selection.Value.Count == 0)
            {
                PromptSelectionOptions options = new PromptSelectionOptions
                {
                    MessageForAdding = "\n选择标注和图形（仅修改其中的标注）："
                };
                selection = editor.GetSelection(options);
            }
            else
            {
                editor.SetImpliedSelection(new ObjectId[0]);
            }

            if (selection.Status != PromptStatus.OK)
            {
                return "已取消更换标注样式";
            }

            ObjectId[] selectedIds = selection.Value.GetObjectIds();
            int dimensionCount = 0;
            int changedCount = 0;
            int failedCount = 0;
            int changedBlockCount = 0;
            int unsupportedBlockCount = 0;
            HashSet<ObjectId> processedBlockDefinitions = new HashSet<ObjectId>();

            using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
            {
                DimStyleTable styleTable = (DimStyleTable)transaction.GetObject(
                    document.Database.DimStyleTableId,
                    OpenMode.ForRead);
                if (!styleTable.Has(styleName))
                {
                    return "标注样式不存在，请刷新列表后重试：" + styleName;
                }

                ObjectId targetStyleId = styleTable[styleName];
                BlockTable blockTable = (BlockTable)transaction.GetObject(
                    document.Database.BlockTableId,
                    OpenMode.ForRead);
                foreach (ObjectId objectId in selectedIds)
                {
                    DBObject selectedObject = transaction.GetObject(objectId, OpenMode.ForRead, false);
                    Dimension dimension = selectedObject as Dimension;
                    if (dimension != null)
                    {
                        dimensionCount++;
                        TryApplyStyle(dimension, targetStyleId, ref changedCount, ref failedCount);
                        continue;
                    }

                    BlockReference blockReference = selectedObject as BlockReference;
                    if (blockReference == null)
                    {
                        continue;
                    }

                    ObjectId definitionId = blockReference.BlockTableRecord;
                    BlockTableRecord definition = transaction.GetObject(
                        definitionId,
                        OpenMode.ForRead,
                        false) as BlockTableRecord;
                    if (definition == null || definition.IsLayout || definition.IsFromExternalReference)
                    {
                        unsupportedBlockCount++;
                        continue;
                    }

                    if (selectedBlockOnly)
                    {
                        if (blockReference.IsDynamicBlock)
                        {
                            unsupportedBlockCount++;
                            continue;
                        }

                        int dimensionsToChange = CountDimensionsToChange(
                            definition,
                            transaction,
                            targetStyleId);
                        if (dimensionsToChange == 0)
                        {
                            dimensionCount += CountDimensions(definition, transaction);
                            continue;
                        }

                        ObjectId clonedDefinitionId = CloneBlockDefinition(
                            document.Database,
                            blockTable,
                            definition,
                            transaction);
                        BlockTableRecord clonedDefinition = (BlockTableRecord)transaction.GetObject(
                            clonedDefinitionId,
                            OpenMode.ForRead);
                        ApplyDimensions(
                            clonedDefinition,
                            transaction,
                            targetStyleId,
                            ref dimensionCount,
                            ref changedCount,
                            ref failedCount);

                        blockReference.UpgradeOpen();
                        blockReference.BlockTableRecord = clonedDefinitionId;
                        changedBlockCount++;
                    }
                    else if (processedBlockDefinitions.Add(definitionId))
                    {
                        int before = changedCount;
                        ApplyDimensions(
                            definition,
                            transaction,
                            targetStyleId,
                            ref dimensionCount,
                            ref changedCount,
                            ref failedCount);
                        if (changedCount > before)
                        {
                            changedBlockCount++;
                        }
                    }
                }

                transaction.Commit();
            }

            string message = "已选择 " + selectedIds.Length + " 个对象，识别 " + dimensionCount +
                " 个标注，成功更换 " + changedCount + " 个为“" + styleName + "”";
            if (failedCount > 0)
            {
                message += "，另有 " + failedCount + " 个标注无法修改";
            }
            if (changedBlockCount > 0)
            {
                message += "，已处理 " + changedBlockCount + " 个块";
            }
            if (unsupportedBlockCount > 0)
            {
                message += "，跳过 " + unsupportedBlockCount + " 个动态块、布局块或外部参照";
            }

            editor.WriteMessage("\n" + message + "。");
            return message;
        }

        private static int CountDimensions(BlockTableRecord definition, Transaction transaction)
        {
            int count = 0;
            foreach (ObjectId id in definition)
            {
                if (transaction.GetObject(id, OpenMode.ForRead, false) is Dimension)
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountDimensionsToChange(
            BlockTableRecord definition,
            Transaction transaction,
            ObjectId targetStyleId)
        {
            int count = 0;
            foreach (ObjectId id in definition)
            {
                Dimension dimension = transaction.GetObject(id, OpenMode.ForRead, false) as Dimension;
                if (dimension != null && dimension.DimensionStyle != targetStyleId)
                {
                    count++;
                }
            }
            return count;
        }

        private static ObjectId CloneBlockDefinition(
            Database database,
            BlockTable blockTable,
            BlockTableRecord source,
            Transaction transaction)
        {
            if (!blockTable.IsWriteEnabled)
            {
                blockTable.UpgradeOpen();
            }

            BlockTableRecord clone = new BlockTableRecord { Name = "*U" };
            ObjectId cloneId = blockTable.Add(clone);
            transaction.AddNewlyCreatedDBObject(clone, true);

            ObjectIdCollection sourceObjects = new ObjectIdCollection();
            foreach (ObjectId id in source)
            {
                sourceObjects.Add(id);
            }

            database.DeepCloneObjects(sourceObjects, cloneId, new IdMapping(), false);
            return cloneId;
        }

        private static void ApplyDimensions(
            BlockTableRecord definition,
            Transaction transaction,
            ObjectId targetStyleId,
            ref int dimensionCount,
            ref int changedCount,
            ref int failedCount)
        {
            foreach (ObjectId id in definition)
            {
                Dimension dimension = transaction.GetObject(id, OpenMode.ForRead, false) as Dimension;
                if (dimension == null)
                {
                    continue;
                }

                dimensionCount++;
                TryApplyStyle(dimension, targetStyleId, ref changedCount, ref failedCount);
            }
        }

        private static void TryApplyStyle(
            Dimension dimension,
            ObjectId targetStyleId,
            ref int changedCount,
            ref int failedCount)
        {
            if (dimension.DimensionStyle == targetStyleId)
            {
                return;
            }

            try
            {
                if (!dimension.IsWriteEnabled)
                {
                    dimension.UpgradeOpen();
                }
                dimension.DimensionStyle = targetStyleId;
                dimension.RecomputeDimensionBlock(true);
                changedCount++;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                failedCount++;
            }
        }
    }
}
