using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CadHdScreenshot.Plugin
{
    internal static class ClipboardOutput
    {
        public static void Copy(Image image)
        {
            ExternalException lastException = null;
            byte[] pngBytes;
            using (MemoryStream encoded = new MemoryStream())
            {
                image.Save(encoded, ImageFormat.Png);
                pngBytes = encoded.ToArray();
            }

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using (Bitmap bitmap = new Bitmap(image))
                    using (MemoryStream png = new MemoryStream(pngBytes, writable: false))
                    {
                        DataObject data = new DataObject();
                        data.SetData(DataFormats.Bitmap, true, bitmap);
                        data.SetData("PNG", false, png);
                        Clipboard.SetDataObject(data, true);
                    }
                    return;
                }
                catch (ExternalException exception)
                {
                    lastException = exception;
                    Thread.Sleep(60 * (attempt + 1));
                }
            }

            throw new InvalidOperationException("Windows 剪贴板正被其他程序占用。", lastException);
        }
    }
}
