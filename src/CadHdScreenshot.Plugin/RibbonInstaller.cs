using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
			if (ribbon.Tabs.Any((RibbonTab existingTab) => existingTab.Id == "CadHdScreenshot.RibbonTab"))
			{
				return true;
			}
			RibbonTab ribbonTab = new RibbonTab
			{
				Id = "CadHdScreenshot.RibbonTab",
				Title = "家具方案"
			};
			RibbonPanelSource ribbonPanelSource = new RibbonPanelSource
			{
				Title = "工具"
			};
			RibbonButton item = new RibbonButton
			{
				Id = "CadHdScreenshot.ShowWindow",
				Text = "综合工具",
				ShowText = true,
				ShowImage = true,
				Size = RibbonItemSize.Large,
				MinWidth = 72.0,
				ResizeStyle = RibbonItemResizeStyles.NoResize,
				Orientation = Orientation.Vertical,
				Image = CreateScreenshotIcon(),
				LargeImage = CreateScreenshotIcon(),
				ToolTip = "打开 CAD 综合工具窗口",
				CommandHandler = new ShowWindowCommand()
			};
			ribbonPanelSource.Items.Add(item);
			ribbonTab.Panels.Add(new RibbonPanel
			{
				Source = ribbonPanelSource
			});
			ribbon.Tabs.Add(ribbonTab);
			return true;
		}

		private static ImageSource CreateScreenshotIcon()
		{
			DrawingGroup drawingGroup = new DrawingGroup();
			using (DrawingContext drawingContext = drawingGroup.Open())
			{
				Brush brush = new SolidColorBrush(Color.FromRgb(8, 119, 209));
				Pen pen = new Pen(Brushes.White, 2.0);
				pen.StartLineCap = PenLineCap.Round;
				pen.EndLineCap = PenLineCap.Round;
				drawingContext.DrawRoundedRectangle(brush, null, new Rect(0.0, 0.0, 32.0, 32.0), 4.0, 4.0);
				drawingContext.DrawRectangle(null, pen, new Rect(7.0, 9.0, 18.0, 14.0));
				drawingContext.DrawEllipse(Brushes.White, null, new Point(16.0, 16.0), 4.0, 4.0);
				drawingContext.DrawLine(pen, new Point(11.0, 7.0), new Point(15.0, 7.0));
			}
			drawingGroup.Freeze();
			DrawingImage drawingImage = new DrawingImage(drawingGroup);
			drawingImage.Freeze();
			return drawingImage;
		}
	}
}
