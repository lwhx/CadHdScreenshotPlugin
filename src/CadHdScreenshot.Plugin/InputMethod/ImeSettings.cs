using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace CadHdScreenshot.Plugin.InputMethod
{
    public sealed class ImeSettings
    {
        public bool Enabled { get; set; }
        public bool Diagnostics { get; set; }

        internal static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CadHdScreenshot", "ime-settings.xml");

        internal static ImeSettings Load() => Load(SettingsPath);
        internal static ImeSettings Load(string path)
        {
            if (!File.Exists(path)) return new ImeSettings();
            using (var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                return (ImeSettings)new XmlSerializer(typeof(ImeSettings)).Deserialize(reader);
        }

        internal void Save() => Save(SettingsPath);
        internal void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var writer = XmlWriter.Create(temporary, new XmlWriterSettings { Indent = true }))
                new XmlSerializer(typeof(ImeSettings)).Serialize(writer, this);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
    }
}
