using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace CadHdScreenshot.Core
{
    public sealed class FileSettingsStore
    {
        private readonly string _filePath;
        private readonly DataContractJsonSerializer _serializer =
            new DataContractJsonSerializer(typeof(ScreenshotSettings));

        public FileSettingsStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Settings path is required.", nameof(filePath));
            }

            _filePath = Path.GetFullPath(filePath);
        }

        public static FileSettingsStore CreateForCurrentUser()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CadHdScreenshot");
            return new FileSettingsStore(Path.Combine(directory, "settings.json"));
        }

        public ScreenshotSettings Load()
        {
            if (!File.Exists(_filePath))
            {
                return ScreenshotSettings.CreateDefault();
            }

            try
            {
                using (FileStream stream = File.OpenRead(_filePath))
                {
                    ScreenshotSettings settings = (ScreenshotSettings)_serializer.ReadObject(stream);
                    return settings?.Normalize() ?? ScreenshotSettings.CreateDefault();
                }
            }
            catch (IOException)
            {
                return ScreenshotSettings.CreateDefault();
            }
            catch (SerializationException)
            {
                return ScreenshotSettings.CreateDefault();
            }
        }

        public void Save(ScreenshotSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            ScreenshotSettings normalized = settings.Normalize();
            AtomicFileWriter.Write(
                _filePath,
                stream => _serializer.WriteObject(stream, normalized),
                overwrite: true);
        }
    }
}
