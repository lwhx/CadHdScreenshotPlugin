using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CadHdScreenshot.Core
{
	public static class ImageProcessor
	{
		public static Bitmap Process(Image source, ImageProcessingOptions options)
		{
			if (source == null)
			{
				throw new ArgumentNullException("source");
			}
			options = options ?? new ImageProcessingOptions();
			if (options.BorderPixels < 0)
			{
				throw new ArgumentOutOfRangeException("BorderPixels");
			}
			if (!PixelRiskEvaluator.Evaluate(CalculateOutputSize(new PixelSize(source.Width, source.Height), options.BorderPixels)).IsAllowed)
			{
				throw new ArgumentOutOfRangeException("options", "包含白边的图片超过 5 亿像素限制。");
			}
			Bitmap bitmap = CopyToArgb(source);
			try
			{
				ApplyPixelTransform(bitmap, options);
				if (options.BorderPixels == 0)
				{
					Bitmap result = bitmap;
					bitmap = null;
					return result;
				}
				return AddBorder(bitmap, options.BorderPixels, options.BorderColor);
			}
			finally
			{
				bitmap?.Dispose();
			}
		}

		public static PixelSize CalculateOutputSize(PixelSize sourceSize, int borderPixels)
		{
			if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
			{
				throw new ArgumentOutOfRangeException("sourceSize");
			}
			if (borderPixels < 0)
			{
				throw new ArgumentOutOfRangeException("borderPixels");
			}
			checked
			{
				int num = borderPixels * 2;
				return new PixelSize(sourceSize.Width + num, sourceSize.Height + num);
			}
		}

		private static Bitmap CopyToArgb(Image source)
		{
			Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
			try
			{
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					graphics.CompositingMode = CompositingMode.SourceCopy;
					graphics.DrawImageUnscaled(source, 0, 0);
				}
				return bitmap;
			}
			catch
			{
				bitmap.Dispose();
				throw;
			}
		}

		private static void ApplyPixelTransform(Bitmap bitmap, ImageProcessingOptions options)
		{
			Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
			BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
			try
			{
				int num = Math.Abs(bitmapData.Stride);
				byte[] array = new byte[num];
				for (int i = 0; i < bitmapData.Height; i++)
				{
					IntPtr intPtr = IntPtr.Add(bitmapData.Scan0, checked(i * bitmapData.Stride));
					Marshal.Copy(intPtr, array, 0, num);
					for (int j = 0; j < bitmapData.Width; j++)
					{
						int num2 = j * 4;
						byte blue = array[num2];
						byte green = array[num2 + 1];
						byte red = array[num2 + 2];
						byte alpha = array[num2 + 3];
						TransformColor(ref red, ref green, ref blue, ref alpha, options);
						array[num2] = blue;
						array[num2 + 1] = green;
						array[num2 + 2] = red;
						array[num2 + 3] = alpha;
					}
					Marshal.Copy(array, 0, intPtr, num);
				}
			}
			finally
			{
				bitmap.UnlockBits(bitmapData);
			}
		}

		private static void TransformColor(ref byte red, ref byte green, ref byte blue, ref byte alpha, ImageProcessingOptions options)
		{
			switch (options.ColorMode)
			{
			case ScreenshotColorMode.BlackAndWhite:
			{
				byte b2 = (byte)((Luminance(red, green, blue) >= options.BlackAndWhiteThreshold) ? byte.MaxValue : 0);
				red = (green = (blue = b2));
				break;
			}
			case ScreenshotColorMode.WhiteBackground:
				red = CompositeOnWhite(red, alpha);
				green = CompositeOnWhite(green, alpha);
				blue = CompositeOnWhite(blue, alpha);
				alpha = byte.MaxValue;
				break;
			case ScreenshotColorMode.Grayscale:
			{
				byte b = Luminance(red, green, blue);
				red = (green = (blue = b));
				break;
			}
			case ScreenshotColorMode.AccentA:
				DesaturateUnlessAccent(ref red, ref green, ref blue, options.AccentAColor, options.AccentTolerance);
				break;
			case ScreenshotColorMode.AccentB:
				DesaturateUnlessAccent(ref red, ref green, ref blue, options.AccentBColor, options.AccentTolerance);
				break;
			}
			if (options.WhiteToTransparent && red >= options.WhiteThreshold && green >= options.WhiteThreshold && blue >= options.WhiteThreshold)
			{
				alpha = 0;
			}
		}

		private static byte Luminance(byte red, byte green, byte blue)
		{
			return (byte)Math.Min(255.0, Math.Round((double)(int)red * 0.299 + (double)(int)green * 0.587 + (double)(int)blue * 0.114));
		}

		private static byte CompositeOnWhite(byte color, byte alpha)
		{
			return (byte)((color * alpha + 255 * (255 - alpha)) / 255);
		}

		private static void DesaturateUnlessAccent(ref byte red, ref byte green, ref byte blue, Color accent, int tolerance)
		{
			int num = red - accent.R;
			int num2 = green - accent.G;
			int num3 = blue - accent.B;
			int num4 = num * num + num2 * num2 + num3 * num3;
			int num5 = Math.Max(0, Math.Min(441, tolerance));
			if (num4 > num5 * num5)
			{
				byte b = Luminance(red, green, blue);
				red = (green = (blue = b));
			}
		}

		private static Bitmap AddBorder(Bitmap source, int borderPixels, Color borderColor)
		{
			checked
			{
				int num = borderPixels * 2;
				int width = source.Width + num;
				int height = source.Height + num;
				Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
				try
				{
					using (Graphics graphics = Graphics.FromImage(bitmap))
					{
						graphics.Clear(borderColor);
						graphics.CompositingMode = CompositingMode.SourceCopy;
						graphics.DrawImageUnscaled(source, borderPixels, borderPixels);
					}
					return bitmap;
				}
				catch
				{
					bitmap.Dispose();
					throw;
				}
			}
		}
	}
}
