using System;
using System.Drawing;
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
                throw new ArgumentNullException(nameof(source));
            }

            options = options ?? new ImageProcessingOptions();
            if (options.BorderPixels < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options.BorderPixels));
            }

            PixelSize outputSize = CalculateOutputSize(new PixelSize(source.Width, source.Height), options.BorderPixels);
            if (!PixelRiskEvaluator.Evaluate(outputSize).IsAllowed)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "包含白边的图片超过 5 亿像素限制。");
            }

            Bitmap transformed = CopyToArgb(source);
            try
            {
                ApplyPixelTransform(transformed, options);
                if (options.BorderPixels == 0)
                {
                    Bitmap result = transformed;
                    transformed = null;
                    return result;
                }

                return AddBorder(transformed, options.BorderPixels, options.BorderColor);
            }
            finally
            {
                transformed?.Dispose();
            }
        }

        public static PixelSize CalculateOutputSize(PixelSize sourceSize, int borderPixels)
        {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceSize));
            }
            if (borderPixels < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(borderPixels));
            }

            int border = checked(borderPixels * 2);
            return new PixelSize(checked(sourceSize.Width + border), checked(sourceSize.Height + border));
        }

        private static Bitmap CopyToArgb(Image source)
        {
            Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
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
            Rectangle rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rectangle, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            try
            {
                int rowBytes = Math.Abs(data.Stride);
                byte[] pixels = new byte[rowBytes];

                for (int y = 0; y < data.Height; y++)
                {
                    IntPtr row = IntPtr.Add(data.Scan0, checked(y * data.Stride));
                    Marshal.Copy(row, pixels, 0, rowBytes);
                    for (int x = 0; x < data.Width; x++)
                    {
                        int offset = x * 4;
                        byte blue = pixels[offset];
                        byte green = pixels[offset + 1];
                        byte red = pixels[offset + 2];
                        byte alpha = pixels[offset + 3];

                        TransformColor(ref red, ref green, ref blue, ref alpha, options);

                        pixels[offset] = blue;
                        pixels[offset + 1] = green;
                        pixels[offset + 2] = red;
                        pixels[offset + 3] = alpha;
                    }
                    Marshal.Copy(pixels, 0, row, rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static void TransformColor(
            ref byte red,
            ref byte green,
            ref byte blue,
            ref byte alpha,
            ImageProcessingOptions options)
        {
            switch (options.ColorMode)
            {
                case ScreenshotColorMode.BlackAndWhite:
                    byte blackOrWhite = Luminance(red, green, blue) >= options.BlackAndWhiteThreshold
                        ? byte.MaxValue
                        : byte.MinValue;
                    red = green = blue = blackOrWhite;
                    break;

                case ScreenshotColorMode.WhiteBackground:
                    red = CompositeOnWhite(red, alpha);
                    green = CompositeOnWhite(green, alpha);
                    blue = CompositeOnWhite(blue, alpha);
                    alpha = byte.MaxValue;
                    break;

                case ScreenshotColorMode.Grayscale:
                    byte gray = Luminance(red, green, blue);
                    red = green = blue = gray;
                    break;

                case ScreenshotColorMode.AccentA:
                    DesaturateUnlessAccent(ref red, ref green, ref blue, options.AccentAColor, options.AccentTolerance);
                    break;

                case ScreenshotColorMode.AccentB:
                    DesaturateUnlessAccent(ref red, ref green, ref blue, options.AccentBColor, options.AccentTolerance);
                    break;
            }

            if (options.WhiteToTransparent &&
                red >= options.WhiteThreshold &&
                green >= options.WhiteThreshold &&
                blue >= options.WhiteThreshold)
            {
                alpha = 0;
            }
        }

        private static byte Luminance(byte red, byte green, byte blue)
        {
            return (byte)Math.Min(255, Math.Round((red * 0.299) + (green * 0.587) + (blue * 0.114)));
        }

        private static byte CompositeOnWhite(byte color, byte alpha)
        {
            return (byte)(((color * alpha) + (255 * (255 - alpha))) / 255);
        }

        private static void DesaturateUnlessAccent(
            ref byte red,
            ref byte green,
            ref byte blue,
            Color accent,
            int tolerance)
        {
            int redDelta = red - accent.R;
            int greenDelta = green - accent.G;
            int blueDelta = blue - accent.B;
            int distanceSquared = (redDelta * redDelta) + (greenDelta * greenDelta) + (blueDelta * blueDelta);
            int clampedTolerance = Math.Max(0, Math.Min(441, tolerance));

            if (distanceSquared > clampedTolerance * clampedTolerance)
            {
                byte gray = Luminance(red, green, blue);
                red = green = blue = gray;
            }
        }

        private static Bitmap AddBorder(Bitmap source, int borderPixels, Color borderColor)
        {
            int horizontalBorder = checked(borderPixels * 2);
            int width = checked(source.Width + horizontalBorder);
            int height = checked(source.Height + horizontalBorder);
            Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            try
            {
                using (Graphics graphics = Graphics.FromImage(result))
                {
                    graphics.Clear(borderColor);
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    graphics.DrawImageUnscaled(source, borderPixels, borderPixels);
                }
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }
    }
}
