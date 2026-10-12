using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace CadHdScreenshot.Plugin
{
	internal static class EntitySelector
	{
		private const double SafetyMarginRatio = 0.02;

		public static CaptureSelection Select(Editor editor, Database database)
		{
			PromptSelectionOptions options = new PromptSelectionOptions
			{
				MessageForAdding = "\n选择要截图的对象："
			};
			PromptSelectionResult selection = editor.GetSelection(options);
			if (selection.Status != PromptStatus.OK)
			{
				return null;
			}
			ObjectId[] objectIds = selection.Value.GetObjectIds();
			Extents3d extents = default(Extents3d);
			bool flag = false;
			using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
			{
				ObjectId[] array = objectIds;
				foreach (ObjectId id in array)
				{
					Entity entity = transaction.GetObject(id, OpenMode.ForRead, openErased: false) as Entity;
					if (entity == null)
					{
						continue;
					}
					try
					{
						Extents3d geometricExtents = entity.GeometricExtents;
						if (flag)
						{
							extents.AddExtents(geometricExtents);
							continue;
						}
						extents = geometricExtents;
						flag = true;
					}
					catch (Exception)
					{
					}
				}
			}
			if (!flag)
			{
				editor.WriteMessage("\n所选对象没有可用的几何范围。");
				return null;
			}
			return new CaptureSelection(ExtentsUtilities.Expand(extents, 0.02), objectIds);
		}
	}
}
