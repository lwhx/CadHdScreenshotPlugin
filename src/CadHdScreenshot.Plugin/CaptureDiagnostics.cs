using System;
using System.IO;

namespace CadHdScreenshot.Plugin
{
    internal static class CaptureDiagnostics
    {
        // Diagnostics must never become another unhandled exception in the UI.
        public static string TryWrite(string stage, Exception exception)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CadHdScreenshot", "Logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")
                    + "-" + Guid.NewGuid().ToString("N") + ".log");
                var assembly = typeof(CaptureDiagnostics).Assembly;
                File.WriteAllText(path,
                    "Time: " + DateTimeOffset.Now.ToString("O") + Environment.NewLine +
                    "Stage: " + stage + Environment.NewLine +
                    "Assembly: " + assembly.FullName + Environment.NewLine +
                    "Location: " + assembly.Location + Environment.NewLine +
                    "Module: " + assembly.ManifestModule.ModuleVersionId + Environment.NewLine +
                    exception + Environment.NewLine);
                return path;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
