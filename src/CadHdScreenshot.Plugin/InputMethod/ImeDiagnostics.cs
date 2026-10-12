using System;
using System.IO;

namespace CadHdScreenshot.Plugin.InputMethod
{
    internal static class ImeDiagnostics
    {
        internal static string LogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CadHdScreenshot", "Logs", "ime.log");
        internal static void Write(bool enabled, string metadata)
        {
            if (!enabled) return;
            try
            {
                string path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path) && new FileInfo(path).Length >= 5 * 1024 * 1024)
                {
                    if (File.Exists(path + ".2")) File.Delete(path + ".2");
                    if (File.Exists(path + ".1")) File.Move(path + ".1", path + ".2");
                    File.Move(path, path + ".1");
                }
                File.AppendAllText(path, DateTime.Now.ToString("O") + " " + metadata + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
