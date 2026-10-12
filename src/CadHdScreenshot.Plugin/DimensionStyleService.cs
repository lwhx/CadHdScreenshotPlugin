using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace CadHdScreenshot.Plugin
{
	internal sealed class DimensionStyleService
	{
		public async Task<IReadOnlyList<string>> GetStyleNamesAsync()
		{
			DocumentCollection documents = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
			Document document = documents.MdiActiveDocument;
			if (document == null)
			{
				return new string[0];
			}
			return await CommandContextRunner.ExecuteAsync(async delegate(Func<object, Task> callback, object data)
			{
				await documents.ExecuteInCommandContextAsync(callback, data);
			}, () => ReadStyleNames(document));
		}

		public async Task<string> ApplyAsync(string styleName, bool selectedBlockOnly)
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
			}, () => Apply(document, styleName, selectedBlockOnly));
		}

		private static IReadOnlyList<string> ReadStyleNames(Document document)
		{
			List<string> list = new List<string>();
			using (Transaction transaction = document.Database.TransactionManager.StartOpenCloseTransaction())
			{
				foreach (ObjectId item in (DimStyleTable)transaction.GetObject(document.Database.DimStyleTableId, OpenMode.ForRead))
				{
					DimStyleTableRecord dimStyleTableRecord = transaction.GetObject(item, OpenMode.ForRead) as DimStyleTableRecord;
					if (dimStyleTableRecord != null && !dimStyleTableRecord.IsDependent)
					{
						list.Add(dimStyleTableRecord.Name);
					}
				}
			}
			return list.OrderBy((string name) => name, StringComparer.CurrentCultureIgnoreCase).ToArray();
		}

		private static string Apply(Document document, string styleName, bool selectedBlockOnly)
		{
			if (string.IsNullOrWhiteSpace(styleName))
			{
				return "请先选择目标标注样式";
			}
			Editor editor = document.Editor;
			PromptSelectionResult promptSelectionResult = editor.SelectImplied();
			if (promptSelectionResult.Status != PromptStatus.OK || promptSelectionResult.Value.Count == 0)
			{
				PromptSelectionOptions options = new PromptSelectionOptions
				{
					MessageForAdding = "\n选择标注和图形（仅修改其中的标注）："
				};
				promptSelectionResult = editor.GetSelection(options);
			}
			else
			{
				editor.SetImpliedSelection(new ObjectId[0]);
			}
			if (promptSelectionResult.Status != PromptStatus.OK)
			{
				return "已取消更换标注样式";
			}
			ObjectId[] objectIds = promptSelectionResult.Value.GetObjectIds();
			int dimensionCount = 0;
			int changedCount = 0;
			int failedCount = 0;
			int num = 0;
			int num2 = 0;
			HashSet<ObjectId> hashSet = new HashSet<ObjectId>();
			using (Transaction transaction = document.Database.TransactionManager.StartTransaction())
			{
				DimStyleTable dimStyleTable = (DimStyleTable)transaction.GetObject(document.Database.DimStyleTableId, OpenMode.ForRead);
				if (!dimStyleTable.Has(styleName))
				{
					return "标注样式不存在，请刷新列表后重试：" + styleName;
				}
				ObjectId targetStyleId = dimStyleTable[styleName];
				BlockTable blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
				ObjectId[] array = objectIds;
				foreach (ObjectId id in array)
				{
					DBObject dBObject = transaction.GetObject(id, OpenMode.ForRead, openErased: false);
					Dimension dimension = dBObject as Dimension;
					if (dimension != null)
					{
						dimensionCount++;
						TryApplyStyle(dimension, targetStyleId, ref changedCount, ref failedCount);
						continue;
					}
					BlockReference blockReference = dBObject as BlockReference;
					if (blockReference == null)
					{
						continue;
					}
					ObjectId blockTableRecord = blockReference.BlockTableRecord;
					BlockTableRecord blockTableRecord2 = transaction.GetObject(blockTableRecord, OpenMode.ForRead, openErased: false) as BlockTableRecord;
					if (blockTableRecord2 == null || blockTableRecord2.IsLayout || blockTableRecord2.IsFromExternalReference)
					{
						num2++;
					}
					else if (selectedBlockOnly)
					{
						if (blockReference.IsDynamicBlock)
						{
							num2++;
							continue;
						}
						if (CountDimensionsToChange(blockTableRecord2, transaction, targetStyleId) == 0)
						{
							dimensionCount += CountDimensions(blockTableRecord2, transaction);
							continue;
						}
						ObjectId objectId = CloneBlockDefinition(document.Database, blockTable, blockTableRecord2, transaction);
						ApplyDimensions((BlockTableRecord)transaction.GetObject(objectId, OpenMode.ForRead), transaction, targetStyleId, ref dimensionCount, ref changedCount, ref failedCount);
						blockReference.UpgradeOpen();
						blockReference.BlockTableRecord = objectId;
						num++;
					}
					else if (hashSet.Add(blockTableRecord))
					{
						int num3 = changedCount;
						ApplyDimensions(blockTableRecord2, transaction, targetStyleId, ref dimensionCount, ref changedCount, ref failedCount);
						if (changedCount > num3)
						{
							num++;
						}
					}
				}
				transaction.Commit();
			}
			string text = "已选择 " + objectIds.Length + " 个对象，识别 " + dimensionCount + " 个标注，成功更换 " + changedCount + " 个为“" + styleName + "”";
			if (failedCount > 0)
			{
				text = text + "，另有 " + failedCount + " 个标注无法修改";
			}
			if (num > 0)
			{
				text = text + "，已处理 " + num + " 个块";
			}
			if (num2 > 0)
			{
				text = text + "，跳过 " + num2 + " 个动态块、布局块或外部参照";
			}
			editor.WriteMessage("\n" + text + "。");
			return text;
		}

		private static int CountDimensions(BlockTableRecord definition, Transaction transaction)
		{
			int num = 0;
			foreach (ObjectId item in definition)
			{
				if (transaction.GetObject(item, OpenMode.ForRead, openErased: false) is Dimension)
				{
					num++;
				}
			}
			return num;
		}

		private static int CountDimensionsToChange(BlockTableRecord definition, Transaction transaction, ObjectId targetStyleId)
		{
			int num = 0;
			foreach (ObjectId item in definition)
			{
				Dimension dimension = transaction.GetObject(item, OpenMode.ForRead, openErased: false) as Dimension;
				if (dimension != null && dimension.DimensionStyle != targetStyleId)
				{
					num++;
				}
			}
			return num;
		}

		private static ObjectId CloneBlockDefinition(Database database, BlockTable blockTable, BlockTableRecord source, Transaction transaction)
		{
			if (!blockTable.IsWriteEnabled)
			{
				blockTable.UpgradeOpen();
			}
			BlockTableRecord blockTableRecord = new BlockTableRecord
			{
				Name = "*U"
			};
			ObjectId objectId = blockTable.Add(blockTableRecord);
			transaction.AddNewlyCreatedDBObject(blockTableRecord, add: true);
			ObjectIdCollection objectIdCollection = new ObjectIdCollection();
			foreach (ObjectId item in source)
			{
				objectIdCollection.Add(item);
			}
			database.DeepCloneObjects(objectIdCollection, objectId, new IdMapping(), deferTranslation: false);
			return objectId;
		}

		private static void ApplyDimensions(BlockTableRecord definition, Transaction transaction, ObjectId targetStyleId, ref int dimensionCount, ref int changedCount, ref int failedCount)
		{
			foreach (ObjectId item in definition)
			{
				Dimension dimension = transaction.GetObject(item, OpenMode.ForRead, openErased: false) as Dimension;
				if (!(dimension == null))
				{
					dimensionCount++;
					TryApplyStyle(dimension, targetStyleId, ref changedCount, ref failedCount);
				}
			}
		}

		private static void TryApplyStyle(Dimension dimension, ObjectId targetStyleId, ref int changedCount, ref int failedCount)
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
				dimension.RecomputeDimensionBlock(forceUpdate: true);
				changedCount++;
			}
			catch (Autodesk.AutoCAD.Runtime.Exception)
			{
				failedCount++;
			}
		}
	}
}
