using System.Linq;
using System.Windows;
using System.Windows.Media;
using Autodesk.Windows;

namespace CadHdScreenshot.Plugin
{
    internal static class RibbonInstaller
    {
        private const string TabId = "CadHdScreenshot.RibbonTab";

        public static bool TryInstall()
        {
            RibbonControl ribbon = ComponentManager.Ribbon;
            if (ribbon == null)
            {
                return false;
            }

            if (ribbon.Tabs.Any(existingTab => existingTab.Id == TabId))
            {
                return true;
            }

            RibbonTab tab = new RibbonTab
            {
                Id = TabId,
                Title = "家具方案"
            };

            RibbonPanelSource source = new RibbonPanelSource
            {
                Title = "工具"
            };

            RibbonButton button = new RibbonButton
            {
                Id = "CadHdScreenshot.ShowWindow",
                Text = "综合工具",
                ShowText = true,
                ShowImage = true,
                Size = RibbonItemSize.Large,
                MinWidth = 72,
                ResizeStyle = RibbonItemResizeStyles.NoResize,
                Orientation = System.Windows.Controls.Orientation.Vertical,
                Image = CreateScreenshotIcon(),
                LargeImage = CreateScreenshotIcon(),
                ToolTip = "打开 CAD 综合工具窗口",
                CommandHandler = new ShowWindowCommand()
            };

            source.Items.Add(button);
            tab.Panels.Add(new RibbonPanel { Source = source });
            ribbon.Tabs.Add(tab);
            return true;
        }

        private static ImageSource CreateScreenshotIcon()
        {
            DrawingGroup drawing = new DrawingGroup();
            using (DrawingContext context = drawing.Open())
            {
                Brush blue = new SolidColorBrush(Color.FromRgb(8, 119, 209));
                Pen white = new Pen(Brushes.White, 2);
                white.StartLineCap = PenLineCap.Round;
                white.EndLineCap = PenLineCap.Round;

                context.DrawRoundedRectangle(blue, null, new Rect(0, 0, 32, 32), 4, 4);
                context.DrawRectangle(null, white, new Rect(7, 9, 18, 14));
                context.DrawEllipse(Brushes.White, null, new Point(16, 16), 4, 4);
                context.DrawLine(white, new Point(11, 7), new Point(15, 7));
            }

            drawing.Freeze();
            DrawingImage image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }
    }
}
