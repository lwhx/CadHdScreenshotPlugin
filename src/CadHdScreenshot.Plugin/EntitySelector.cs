using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

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
            PromptSelectionResult result = editor.GetSelection(options);
            if (result.Status != PromptStatus.OK)
            {
                return null;
            }

            ObjectId[] objectIds = result.Value.GetObjectIds();
            Extents3d combined = default(Extents3d);
            bool hasExtents = false;

            using (Transaction transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId objectId in objectIds)
                {
                    Entity entity = transaction.GetObject(objectId, OpenMode.ForRead, false) as Entity;
                    if (entity == null)
                    {
                        continue;
                    }

                    try
                    {
                        Extents3d extents = entity.GeometricExtents;
                        if (hasExtents)
                        {
                            combined.AddExtents(extents);
                        }
                        else
                        {
                            combined = extents;
                            hasExtents = true;
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception)
                    {
                        // Some non-graphical or proxy entities do not expose valid extents.
                    }
                }
            }

            if (!hasExtents)
            {
                editor.WriteMessage("\n所选对象没有可用的几何范围。");
                return null;
            }

            return new CaptureSelection(ExtentsUtilities.Expand(combined, SafetyMarginRatio), objectIds);
        }
    }
}
