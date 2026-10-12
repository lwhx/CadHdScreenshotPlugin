using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using CadHdScreenshot.Core;

namespace CadHdScreenshot.Plugin
{
	internal sealed class CaptureCoordinator
	{
		private readonly AutoCadOffscreenRenderer _renderer = new AutoCadOffscreenRenderer();

		private readonly AutoCadViewportRenderer _viewportRenderer = new AutoCadViewportRenderer();

		private readonly ImageFileOutput _fileOutput = new ImageFileOutput();

		public async Task<CaptureOutcome> CaptureAsync(ScreenshotSettings settings)
		{
			ScreenshotSettings normalized = settings.Normalize();
			DocumentCollection documents = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager;
			Document document = documents.MdiActiveDocument;
			if (document == null)
			{
				return CaptureOutcome.Completed("没有活动图纸");
			}
			string stage = "进入命令上下文";
			try
			{
				return await CommandContextRunner.ExecuteAsync(async delegate(Func<object, Task> callback, object data)
				{
					await documents.ExecuteInCommandContextAsync(callback, data);
				}, () => (documents.MdiActiveDocument != document) ? CaptureOutcome.Completed("活动图纸已切换，请重新截图") : Capture(normalized, delegate(string value)
				{
					stage = value;
				}));
			}
			catch (Exception ex)
			{
				string text = CaptureDiagnostics.TryWrite(stage, ex);
				return CaptureOutcome.Completed("截图失败（" + stage + "）：" + ex.Message + ((text == null) ? "；诊断日志写入失败" : ("；日志：" + text)));
			}
		}

		private CaptureOutcome Capture(ScreenshotSettings settings, Action<string> setStage)
		{
			Document mdiActiveDocument = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
			if (mdiActiveDocument == null)
			{
				return CaptureOutcome.Completed("没有活动图纸");
			}
			if (!mdiActiveDocument.Database.TileMode)
			{
				return CaptureOutcome.Completed("当前仅支持模型空间");
			}
			setStage("选择截图范围");
			CaptureSelection captureSelection = ((settings.CaptureMode == CaptureSourceMode.Entities) ? EntitySelector.Select(mdiActiveDocument.Editor, mdiActiveDocument.Database) : WindowSelector.Select(mdiActiveDocument.Editor));
			if (captureSelection == null)
			{
				return CaptureOutcome.Cancelled();
			}
			setStage("读取视图");
			using (new ViewStateGuard(mdiActiveDocument.Editor))
			{
				using (RasterDisplayQualityGuard.Enable(mdiActiveDocument.Editor))
				{
					setStage("计算输出尺寸");
					PixelSize sourceAspect = ScreenPixelMapper.Measure(mdiActiveDocument.Editor, captureSelection);
					PixelSize bounds = ScreenPixelMapper.MeasureViewport();
					PixelSize pixelSize = PixelScaleCalculator.Scale(PixelScaleCalculator.FitWithin(sourceAspect, bounds), settings.PixelScale);
					PixelSize pixelSize2 = ImageProcessor.CalculateOutputSize(pixelSize, settings.BorderPixels);
					PixelRiskEvaluation pixelRiskEvaluation = PixelRiskEvaluator.Evaluate(pixelSize2);
					if (!pixelRiskEvaluation.IsAllowed)
					{
						return CaptureOutcome.Completed("目标尺寸超过 5 亿像素，已禁止执行");
					}
					if (pixelRiskEvaluation.RequiresConfirmation && !ConfirmRisk(pixelSize2, pixelRiskEvaluation))
					{
						return CaptureOutcome.Cancelled();
					}
					Color background = ((settings.WhiteToTransparent || settings.ColorMode == ScreenshotColorMode.WhiteBackground) ? Color.White : Color.Black);
					setStage("离屏渲染");
					using (Bitmap source = Render(mdiActiveDocument, captureSelection, pixelSize, background, settings))
					{
						using (Bitmap image = ImageProcessor.Process(source, CreateProcessingOptions(settings)))
						{
							setStage("图片输出");
							CaptureOutcome result = WriteOutputs(mdiActiveDocument, image, settings, pixelSize2);
							setStage("恢复视图");
							return result;
						}
					}
				}
			}
		}

		private Bitmap Render(Document document, CaptureSelection selection, PixelSize targetSize, Color background, ScreenshotSettings settings)
		{
			if (settings.CaptureMode == CaptureSourceMode.Region && !settings.WhiteToTransparent)
			{
				bool useWhiteBackground = settings.ColorMode == ScreenshotColorMode.BlackAndWhite || settings.ColorMode == ScreenshotColorMode.WhiteBackground || settings.ColorMode == ScreenshotColorMode.Grayscale;
				return _viewportRenderer.Render(document, selection, targetSize, useWhiteBackground);
			}
			return _renderer.Render(document, selection, targetSize, background);
		}

		private CaptureOutcome WriteOutputs(Document document, Bitmap image, ScreenshotSettings settings, PixelSize targetSize)
		{
			List<string> list = new List<string>();
			if (settings.AutoSave)
			{
				try
				{
					string text = SaveAutomatically(document, image, settings);
					list.Add("已保存 " + text);
				}
				catch (Exception ex)
				{
					list.Add("自动保存失败：" + ex.Message);
				}
			}
			if (settings.CopyToClipboard)
			{
				try
				{
					ClipboardOutput.Copy(image);
					list.Add("已复制到剪贴板");
					document.Editor.WriteMessage("\n截图成功，已复制到剪贴板（" + targetSize.Width + " × " + targetSize.Height + "）。");
				}
				catch (Exception ex2)
				{
					list.Add("剪贴板失败：" + ex2.Message);
					document.Editor.WriteMessage("\n截图失败：无法写入剪贴板，" + ex2.Message);
				}
			}
			if (!settings.AutoSave && !settings.CopyToClipboard)
			{
				list.Add("未设置输出目标");
			}
			return CaptureOutcome.Completed(targetSize.Width + " × " + targetSize.Height + "；" + string.Join("；", list));
		}

		private string SaveAutomatically(Document document, Image image, ScreenshotSettings settings)
		{
			string outputDirectory = settings.OutputDirectory;
			string baseName = (string.IsNullOrWhiteSpace(document.Name) ? "CadHdScreenshot" : Path.GetFileNameWithoutExtension(document.Name));
			return _fileOutput.SaveUnique(image, outputDirectory, baseName, settings.OutputFormat, settings.JpegQuality, DateTime.Now);
		}

		private static ImageProcessingOptions CreateProcessingOptions(ScreenshotSettings settings)
		{
			return new ImageProcessingOptions
			{
				ColorMode = settings.ColorMode,
				WhiteToTransparent = settings.WhiteToTransparent,
				BorderPixels = settings.BorderPixels,
				BorderColor = Color.White
			};
		}

		private static bool ConfirmRisk(PixelSize size, PixelRiskEvaluation risk)
		{
			string text = ((risk.Level == PixelRiskLevel.Extreme) ? "极限" : "高风险");
			return MessageBox.Show("目标图片为 " + size.Width + " × " + size.Height + "，属于" + text + "像素范围。是否继续？", "高清截图", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes;
		}
	}
}
