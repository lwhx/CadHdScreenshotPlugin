using System;
using System.IO;
using System.Reflection;

namespace CadHdScreenshot.Plugin
{
	internal static class CaptureDiagnostics
	{
		public static string TryWrite(string stage, Exception exception)
		{
			try
			{
				string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CadHdScreenshot", "Logs");
				Directory.CreateDirectory(text);
				string text2 = Path.Combine(text, "capture-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".log");
				Assembly assembly = typeof(CaptureDiagnostics).Assembly;
				File.WriteAllText(text2, "Time: " + DateTimeOffset.Now.ToString("O") + Environment.NewLine + "Stage: " + stage + Environment.NewLine + "Assembly: " + assembly.FullName + Environment.NewLine + "Location: " + assembly.Location + Environment.NewLine + "Module: " + assembly.ManifestModule.ModuleVersionId.ToString() + Environment.NewLine + exception?.ToString() + Environment.NewLine);
				return text2;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
