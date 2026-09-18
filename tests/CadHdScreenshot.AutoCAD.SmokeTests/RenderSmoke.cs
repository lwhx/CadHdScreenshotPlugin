using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadHdScreenshot.Core;
using CadHdScreenshot.Plugin;

[assembly: CommandClass(typeof(RenderSmoke))]
public class RenderSmoke
{
    [CommandMethod("CADHD_RENDER_SMOKE")]
    public void Run()
    {
        string root = Environment.GetEnvironmentVariable("CADHD_SMOKE_OUTPUT") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CadHdScreenshot", "RenderSmoke");
        Directory.CreateDirectory(root);
        string report = Path.Combine(root, "result.txt");
        try
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || !Path.GetFileNameWithoutExtension(doc.Name).StartsWith("cad-hd-smoke"))
                throw new InvalidOperationException("Smoke test requires its own generated fixture drawing.");
            var selection = new CaptureSelection(new Extents3d(new Point3d(0, 0, 0), new Point3d(100, 60, 0)), null);
            File.WriteAllText(report, "START " + DateTime.Now.ToString("O") + Environment.NewLine);
            for (int scale = 1; scale <= 3; scale++)
            {
                using (Bitmap bitmap = new AutoCadOffscreenRenderer().Render(doc, selection,
                    new PixelSize(800 * scale, 480 * scale), Color.White))
                {
                    bitmap.Save(Path.Combine(root, "render-" + scale + "x.png"), ImageFormat.Png);
                    int nonWhite = 0;
                    for (int y = 0; y < bitmap.Height; y++)
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            Color color = bitmap.GetPixel(x, y);
                            if (color.R < 240 || color.G < 240 || color.B < 240) nonWhite++;
                        }
                    File.AppendAllText(report, scale + "x: " + bitmap.Width + "x" + bitmap.Height +
                        ", nonWhite=" + nonWhite + Environment.NewLine);
                    if (bitmap.Width != 800 * scale || bitmap.Height != 480 * scale || nonWhite < 500 * scale)
                        throw new InvalidOperationException("Rendered output is blank or has incorrect dimensions.");
                }
            }
            File.AppendAllText(report, "PASS" + Environment.NewLine);
        }
        catch (System.Exception exception)
        {
            File.AppendAllText(report, "FAIL " + exception + Environment.NewLine);
        }
    }
}
