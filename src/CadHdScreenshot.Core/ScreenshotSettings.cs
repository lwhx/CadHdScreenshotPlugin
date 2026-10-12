using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace CadHdScreenshot.Core
{
	[DataContract]
	public sealed class ScreenshotSettings
	{
		[DataMember(Order = 1)]
		public CaptureSourceMode CaptureMode { get; set; }

		[DataMember(Order = 2)]
		public double PixelScale { get; set; }

		[DataMember(Order = 3)]
		public ImageOutputFormat OutputFormat { get; set; }

		[DataMember(Order = 4)]
		public ScreenshotColorMode ColorMode { get; set; }

		[DataMember(Order = 5)]
		public bool WhiteToTransparent { get; set; }

		[DataMember(Order = 6)]
		public int BorderPixels { get; set; }

		[DataMember(Order = 7)]
		public bool CopyToClipboard { get; set; }

		[DataMember(Order = 8)]
		public bool AutoSave { get; set; }

		[DataMember(Order = 9)]
		public string OutputDirectory { get; set; }

		[DataMember(Order = 10)]
		public int JpegQuality { get; set; }

		[DataMember(Order = 11)]
		public int TargetLongEdge { get; set; }

		[DataMember(Order = 12)]
		public string CustomCommand { get; set; }

		public static ScreenshotSettings CreateDefault()
		{
			return new ScreenshotSettings
			{
				CaptureMode = CaptureSourceMode.Region,
				PixelScale = 2.0,
				OutputFormat = ImageOutputFormat.Png,
				ColorMode = ScreenshotColorMode.Original,
				WhiteToTransparent = false,
				BorderPixels = 0,
				CopyToClipboard = true,
				AutoSave = false,
				OutputDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
				JpegQuality = 92,
				TargetLongEdge = 0,
				CustomCommand = "QQ"
			};
		}

		public ScreenshotSettings Normalize()
		{
			ScreenshotSettings screenshotSettings = CreateDefault();
			return new ScreenshotSettings
			{
				CaptureMode = (Enum.IsDefined(typeof(CaptureSourceMode), CaptureMode) ? CaptureMode : screenshotSettings.CaptureMode),
				PixelScale = (IsPositiveFinite(PixelScale) ? PixelScale : screenshotSettings.PixelScale),
				OutputFormat = ((!WhiteToTransparent) ? (Enum.IsDefined(typeof(ImageOutputFormat), OutputFormat) ? OutputFormat : screenshotSettings.OutputFormat) : ImageOutputFormat.Png),
				ColorMode = (Enum.IsDefined(typeof(ScreenshotColorMode), ColorMode) ? ColorMode : screenshotSettings.ColorMode),
				WhiteToTransparent = WhiteToTransparent,
				BorderPixels = Math.Max(0, BorderPixels),
				CopyToClipboard = CopyToClipboard,
				AutoSave = AutoSave,
				OutputDirectory = (string.IsNullOrWhiteSpace(OutputDirectory) ? screenshotSettings.OutputDirectory : OutputDirectory),
				JpegQuality = ((JpegQuality <= 0) ? screenshotSettings.JpegQuality : Math.Min(100, JpegQuality)),
				TargetLongEdge = Math.Max(0, TargetLongEdge),
				CustomCommand = NormalizeCommandName(CustomCommand, screenshotSettings.CustomCommand)
			};
		}

		public static string NormalizeCommandName(string value, string fallback = "QQ")
		{
			string text = (value ?? string.Empty).Trim().ToUpperInvariant();
			if (!Regex.IsMatch(text, "^[A-Z][A-Z0-9_-]{0,15}$"))
			{
				return fallback;
			}
			return text;
		}

		private static bool IsPositiveFinite(double value)
		{
			if (!double.IsNaN(value) && !double.IsInfinity(value))
			{
				return value > 0.0;
			}
			return false;
		}
	}
}
