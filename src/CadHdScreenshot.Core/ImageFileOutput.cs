using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace CadHdScreenshot.Core
{
    public sealed class ImageFileOutput
    {
        public string SaveUnique(
            Image image,
            string directory,
            string baseName,
            ImageOutputFormat format,
            int jpegQuality,
            DateTime timestamp)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            string path = SafeFileNameGenerator.GetAvailablePath(directory, baseName, format, timestamp);
            Save(image, path, format, jpegQuality, overwrite: false);
            return path;
        }

        public void Save(
            Image image,
            string path,
            ImageOutputFormat format,
            int jpegQuality,
            bool overwrite)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }

            int quality = Math.Max(1, Math.Min(100, jpegQuality));
            AtomicFileWriter.Write(
                path,
                stream => Encode(image, stream, format, quality),
                overwrite);
        }

        private static void Encode(Image image, Stream stream, ImageOutputFormat format, int jpegQuality)
        {
            if (format == ImageOutputFormat.Png)
            {
                image.Save(stream, ImageFormat.Png);
                return;
            }

            using (Bitmap flattened = new Bitmap(image.Width, image.Height, PixelFormat.Format24bppRgb))
            using (Graphics graphics = Graphics.FromImage(flattened))
            using (EncoderParameters parameters = new EncoderParameters(1))
            {
                graphics.Clear(Color.White);
                graphics.DrawImageUnscaled(image, 0, 0);
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)jpegQuality);
                ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders()
                    .First(item => item.FormatID == ImageFormat.Jpeg.Guid);
                flattened.Save(stream, codec, parameters);
            }
        }
    }
}
