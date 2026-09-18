using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace CadHdScreenshot.Plugin
{
    internal static class WindowSelector
    {
        public static CaptureSelection Select(Editor editor)
        {
            PromptPointResult first = editor.GetPoint("\n指定截图范围的第一个角点：");
            if (first.Status != PromptStatus.OK)
            {
                return null;
            }

            PromptCornerOptions options = new PromptCornerOptions(
                "\n指定截图范围的另一个角点：",
                first.Value);
            PromptPointResult second = editor.GetCorner(options);
            if (second.Status != PromptStatus.OK)
            {
                return null;
            }

            Vector3d size = second.Value - first.Value;
            if (System.Math.Abs(size.X) <= Tolerance.Global.EqualPoint ||
                System.Math.Abs(size.Y) <= Tolerance.Global.EqualPoint)
            {
                editor.WriteMessage("\n截图范围的宽度和高度必须大于零。");
                return null;
            }

            return CaptureSelection.FromWindow(first.Value, second.Value);
        }
    }
}
