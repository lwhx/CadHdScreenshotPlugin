using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace CadHdScreenshot.Core
{
	public sealed class ImageFileOutput
	{
		public string SaveUnique(Image image, string directory, string baseName, ImageOutputFormat format, int jpegQuality, DateTime timestamp)
		{
			if (image == null)
			{
				throw new ArgumentNullException("image");
			}
			string availablePath = SafeFileNameGenerator.GetAvailablePath(directory, baseName, format, timestamp);
			Save(image, availablePath, format, jpegQuality, overwrite: false);
			return availablePath;
		}

		public void Save(Image image, string path, ImageOutputFormat format, int jpegQuality, bool overwrite)
		{
			if (image == null)
			{
				throw new ArgumentNullException("image");
			}
			int quality = Math.Max(1, Math.Min(100, jpegQuality));
			AtomicFileWriter.Write(path, delegate(Stream stream)
			{
				Encode(image, stream, format, quality);
			}, overwrite);
		}

		private static void Encode(Image image, Stream stream, ImageOutputFormat format, int jpegQuality)
		{
			if (format == ImageOutputFormat.Png)
			{
				image.Save(stream, ImageFormat.Png);
				return;
			}
			using (Bitmap bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb))
			{
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					using (EncoderParameters encoderParameters = new EncoderParameters(1))
					{
						graphics.Clear(Color.White);
						graphics.DrawImageUnscaled(image, 0, 0);
						encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, jpegQuality);
						ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders().First((ImageCodecInfo item) => item.FormatID == ImageFormat.Jpeg.Guid);
						bitmap.Save(stream, encoder, encoderParameters);
					}
				}
			}
		}
	}
}
