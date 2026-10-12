using System;
using System.IO;

namespace CadHdScreenshot.Core
{
	public static class AtomicFileWriter
	{
		public static void Write(string destinationPath, Action<Stream> writer, bool overwrite)
		{
			if (string.IsNullOrWhiteSpace(destinationPath))
			{
				throw new ArgumentException("Destination path is required.", "destinationPath");
			}
			if (writer == null)
			{
				throw new ArgumentNullException("writer");
			}
			string fullPath = Path.GetFullPath(destinationPath);
			string directoryName = Path.GetDirectoryName(fullPath);
			Directory.CreateDirectory(directoryName);
			string text = Path.Combine(directoryName, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
			try
			{
				using (FileStream fileStream = new FileStream(text, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					writer(fileStream);
					fileStream.Flush(flushToDisk: true);
				}
				if (File.Exists(fullPath))
				{
					if (!overwrite)
					{
						throw new IOException("Destination file already exists: " + fullPath);
					}
					File.Replace(text, fullPath, null, ignoreMetadataErrors: true);
				}
				else
				{
					File.Move(text, fullPath);
				}
			}
			finally
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
		}
	}
}
