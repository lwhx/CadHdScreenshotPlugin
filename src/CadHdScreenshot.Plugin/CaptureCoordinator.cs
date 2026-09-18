using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices.Core;
using CadHdScreenshot.Core;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

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
            var documents = AcadApplication.DocumentManager;
            Document document = documents.MdiActiveDocument;
            if (document == null)
            {
                return CaptureOutcome.Completed("没有活动图纸");
            }

            string stage = "进入命令上下文";
            try
            {
                return await CommandContextRunner.ExecuteAsync(
                    async (callback, data) => { await documents.ExecuteInCommandContextAsync(callback, data); },
                    () =>
                    {
                        if (documents.MdiActiveDocument != document)
                        {
                            return CaptureOutcome.Completed("活动图纸已切换，请重新截图");
                        }
                        return Capture(normalized, value => stage = value);
                    });
            }
            catch (Exception exception)
            {
                string logPath = CaptureDiagnostics.TryWrite(stage, exception);
                return CaptureOutcome.Completed("截图失败（" + stage + "）：" + exception.Message +
                    (logPath == null ? "；诊断日志写入失败" : "；日志：" + logPath));
            }
        }

        private CaptureOutcome Capture(ScreenshotSettings settings, Action<string> setStage)
        {
            Document document = AcadApplication.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return CaptureOutcome.Completed("没有活动图纸");
            }

            if (!document.Database.TileMode)
            {
                return CaptureOutcome.Completed("当前仅支持模型空间");
            }

            setStage("选择截图范围");
            CaptureSelection selection = settings.CaptureMode == CaptureSourceMode.Entities
                ? EntitySelector.Select(document.Editor, document.Database)
                : WindowSelector.Select(document.Editor);
            if (selection == null)
            {
                return CaptureOutcome.Cancelled();
            }

            setStage("读取视图");
            using (ViewStateGuard viewState = new ViewStateGuard(document.Editor))
            using (RasterDisplayQualityGuard rasterQuality = RasterDisplayQualityGuard.Enable(document.Editor))
            {
                setStage("计算输出尺寸");
                PixelSize projectedSize = ScreenPixelMapper.Measure(document.Editor, selection);
                PixelSize viewportSize = ScreenPixelMapper.MeasureViewport();
                PixelSize baseSize = PixelScaleCalculator.FitWithin(projectedSize, viewportSize);
                PixelSize targetSize = PixelScaleCalculator.Scale(baseSize, settings.PixelScale);
                PixelSize outputSize = ImageProcessor.CalculateOutputSize(targetSize, settings.BorderPixels);
                PixelRiskEvaluation risk = PixelRiskEvaluator.Evaluate(outputSize);

                if (!risk.IsAllowed)
                {
                    return CaptureOutcome.Completed("目标尺寸超过 5 亿像素，已禁止执行");
                }

                if (risk.RequiresConfirmation && !ConfirmRisk(outputSize, risk))
                {
                    return CaptureOutcome.Cancelled();
                }

                Color background = settings.WhiteToTransparent || settings.ColorMode == ScreenshotColorMode.WhiteBackground
                    ? Color.White
                    : Color.Black;

                setStage("离屏渲染");
                using (Bitmap rendered = Render(document, selection, targetSize, background, settings))
                using (Bitmap processed = ImageProcessor.Process(rendered, CreateProcessingOptions(settings)))
                {
                    setStage("图片输出");
                    CaptureOutcome outcome = WriteOutputs(document, processed, settings, outputSize);
                    setStage("恢复视图");
                    return outcome;
                }
            }
        }

        private Bitmap Render(
            Document document,
            CaptureSelection selection,
            PixelSize targetSize,
            Color background,
            ScreenshotSettings settings)
        {
            // Every region mode uses AutoCAD's native viewport capture so RasterImage,
            // text and line display stay identical across original, white and gray modes.
            if (settings.CaptureMode == CaptureSourceMode.Region &&
                !settings.WhiteToTransparent)
            {
                bool useWhiteBackground =
                    settings.ColorMode == ScreenshotColorMode.BlackAndWhite ||
                    settings.ColorMode == ScreenshotColorMode.WhiteBackground ||
                    settings.ColorMode == ScreenshotColorMode.Grayscale;
                return _viewportRenderer.Render(document, selection, targetSize, useWhiteBackground);
            }

            return _renderer.Render(document, selection, targetSize, background);
        }

        private CaptureOutcome WriteOutputs(
            Document document,
            Bitmap image,
            ScreenshotSettings settings,
            PixelSize targetSize)
        {
            List<string> messages = new List<string>();

            if (settings.AutoSave)
            {
                try
                {
                    string savedPath = SaveAutomatically(document, image, settings);
                    messages.Add("已保存 " + savedPath);
                }
                catch (Exception exception)
                {
                    messages.Add("自动保存失败：" + exception.Message);
                }
            }

            if (settings.CopyToClipboard)
            {
                try
                {
                    ClipboardOutput.Copy(image);
                    messages.Add("已复制到剪贴板");
                    document.Editor.WriteMessage(
                        "\n截图成功，已复制到剪贴板（" +
                        targetSize.Width + " × " + targetSize.Height + "）。");
                }
                catch (Exception exception)
                {
                    messages.Add("剪贴板失败：" + exception.Message);
                    document.Editor.WriteMessage(
                        "\n截图失败：无法写入剪贴板，" + exception.Message);
                }
            }

            if (!settings.AutoSave && !settings.CopyToClipboard)
            {
                messages.Add("未设置输出目标");
            }

            return CaptureOutcome.Completed(
                targetSize.Width + " × " + targetSize.Height + "；" + string.Join("；", messages));
        }

        private string SaveAutomatically(Document document, Image image, ScreenshotSettings settings)
        {
            string directory = settings.OutputDirectory;
            string drawingName = string.IsNullOrWhiteSpace(document.Name)
                ? "CadHdScreenshot"
                : Path.GetFileNameWithoutExtension(document.Name);

            return _fileOutput.SaveUnique(
                image,
                directory,
                drawingName,
                settings.OutputFormat,
                settings.JpegQuality,
                DateTime.Now);
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
            string level = risk.Level == PixelRiskLevel.Extreme ? "极限" : "高风险";
            MessageBoxResult result = MessageBox.Show(
                "目标图片为 " + size.Width + " × " + size.Height + "，属于" + level + "像素范围。是否继续？",
                "高清截图",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }
    }
}
