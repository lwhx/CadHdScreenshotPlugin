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
			ExternalException innerException = null;
			byte[] buffer;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				image.Save(memoryStream, ImageFormat.Png);
				buffer = memoryStream.ToArray();
			}
			for (int i = 0; i < 5; i++)
			{
				try
				{
					using (Bitmap data = new Bitmap(image))
					{
						using (MemoryStream data2 = new MemoryStream(buffer, writable: false))
						{
							DataObject dataObject = new DataObject();
							dataObject.SetData(DataFormats.Bitmap, autoConvert: true, data);
							dataObject.SetData("PNG", autoConvert: false, data2);
							Clipboard.SetDataObject(dataObject, copy: true);
							return;
						}
					}
				}
				catch (ExternalException ex)
				{
					innerException = ex;
					Thread.Sleep(60 * (i + 1));
				}
			}
			throw new InvalidOperationException("Windows 剪贴板正被其他程序占用。", innerException);
		}
	}
}
