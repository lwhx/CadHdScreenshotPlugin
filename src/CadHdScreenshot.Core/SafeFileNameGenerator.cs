using System;
using System.IO;
using System.Linq;

namespace CadHdScreenshot.Core
{
    public static class SafeFileNameGenerator
    {
        public static string GetAvailablePath(
            string directory,
            string baseName,
            ImageOutputFormat format,
            DateTime timestamp)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Output directory is required.", nameof(directory));
            }

            string safeBaseName = SanitizeBaseName(baseName);
            string extension = format == ImageOutputFormat.Jpeg ? ".jpg" : ".png";
            string timestampedName = safeBaseName + "_" + timestamp.ToString("yyyyMMdd_HHmmss");
            string candidate = Path.Combine(directory, timestampedName + extension);
            int suffix = 1;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(directory, timestampedName + "_" + suffix.ToString("000") + extension);
                suffix++;
            }

            return candidate;
        }

        private static string SanitizeBaseName(string baseName)
        {
            string value = string.IsNullOrWhiteSpace(baseName) ? "CadHdScreenshot" : baseName.Trim();
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            char[] sanitized = value.Select(character => invalidCharacters.Contains(character) ? '_' : character).ToArray();
            string result = new string(sanitized).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(result) ? "CadHdScreenshot" : result;
        }
    }
}
