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
                throw new ArgumentException("Destination path is required.", nameof(destinationPath));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            string fullPath = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(
                directory,
                "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (FileStream stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    writer(stream);
                    stream.Flush(true);
                }

                if (File.Exists(fullPath))
                {
                    if (!overwrite)
                    {
                        throw new IOException("Destination file already exists: " + fullPath);
                    }

                    File.Replace(temporaryPath, fullPath, null, true);
                }
                else
                {
                    File.Move(temporaryPath, fullPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
