using System;
using System.IO;
using System.Linq;

namespace CadHdScreenshot.Core
{
	public static class SafeFileNameGenerator
	{
		public static string GetAvailablePath(string directory, string baseName, ImageOutputFormat format, DateTime timestamp)
		{
			if (string.IsNullOrWhiteSpace(directory))
			{
				throw new ArgumentException("Output directory is required.", "directory");
			}
			string text = SanitizeBaseName(baseName);
			string text2 = ((format == ImageOutputFormat.Jpeg) ? ".jpg" : ".png");
			string text3 = text + "_" + timestamp.ToString("yyyyMMdd_HHmmss");
			string text4 = Path.Combine(directory, text3 + text2);
			int num = 1;
			while (File.Exists(text4))
			{
				text4 = Path.Combine(directory, text3 + "_" + num.ToString("000") + text2);
				num++;
			}
			return text4;
		}

		private static string SanitizeBaseName(string baseName)
		{
			string source = (string.IsNullOrWhiteSpace(baseName) ? "CadHdScreenshot" : baseName.Trim());
			char[] invalidCharacters = Path.GetInvalidFileNameChars();
			string text = new string(source.Select((char character) => (!invalidCharacters.Contains(character)) ? character : '_').ToArray()).Trim().TrimEnd('.');
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			return "CadHdScreenshot";
		}
	}
}
