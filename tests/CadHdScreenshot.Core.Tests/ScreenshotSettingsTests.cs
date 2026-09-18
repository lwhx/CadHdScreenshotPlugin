using System;
using System.IO;
using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class ScreenshotSettingsTests
    {
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "CadHdScreenshot.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, true);
            }
        }

        [Test]
        public void CreateDefault_UsesClipboardOnlyOutput()
        {
            ScreenshotSettings settings = ScreenshotSettings.CreateDefault();

            Assert.Multiple(() =>
            {
                Assert.That(settings.CopyToClipboard, Is.True);
                Assert.That(settings.AutoSave, Is.False);
                Assert.That(settings.PixelScale, Is.EqualTo(2));
                Assert.That(settings.TargetLongEdge, Is.EqualTo(0));
                Assert.That(settings.CustomCommand, Is.EqualTo("QQ"));
            });
        }

        [TestCase(" jt ", "JT")]
        [TestCase("CAD_HD-2", "CAD_HD-2")]
        [TestCase("2JT", "QQ")]
        [TestCase("截图", "QQ")]
        [TestCase("", "QQ")]
        public void NormalizeCommandName_ValidatesAutoCadCommand(string input, string expected)
        {
            Assert.That(ScreenshotSettings.NormalizeCommandName(input), Is.EqualTo(expected));
        }

        [Test]
        public void Normalize_WhiteToTransparentForcesPng()
        {
            ScreenshotSettings settings = ScreenshotSettings.CreateDefault();
            settings.OutputFormat = ImageOutputFormat.Jpeg;
            settings.WhiteToTransparent = true;

            ScreenshotSettings normalized = settings.Normalize();

            Assert.That(normalized.OutputFormat, Is.EqualTo(ImageOutputFormat.Png));
        }

        [Test]
        public void Normalize_MissingJpegQualityUsesDefault()
        {
            ScreenshotSettings settings = ScreenshotSettings.CreateDefault();
            settings.JpegQuality = 0;

            ScreenshotSettings normalized = settings.Normalize();

            Assert.That(normalized.JpegQuality, Is.EqualTo(92));
        }

        [Test]
        public void SaveAndLoad_RoundTripsNormalizedSettings()
        {
            string path = Path.Combine(_temporaryDirectory, "nested", "settings.json");
            FileSettingsStore store = new FileSettingsStore(path);
            ScreenshotSettings settings = ScreenshotSettings.CreateDefault();
            settings.CaptureMode = CaptureSourceMode.Entities;
            settings.PixelScale = 3;
            settings.ColorMode = ScreenshotColorMode.Grayscale;
            settings.BorderPixels = 24;
            settings.JpegQuality = 500;
            settings.TargetLongEdge = 8000;
            settings.CustomCommand = "jt";

            store.Save(settings);
            ScreenshotSettings loaded = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.CaptureMode, Is.EqualTo(CaptureSourceMode.Entities));
                Assert.That(loaded.PixelScale, Is.EqualTo(3));
                Assert.That(loaded.ColorMode, Is.EqualTo(ScreenshotColorMode.Grayscale));
                Assert.That(loaded.BorderPixels, Is.EqualTo(24));
                Assert.That(loaded.JpegQuality, Is.EqualTo(100));
                Assert.That(loaded.TargetLongEdge, Is.EqualTo(8000));
                Assert.That(loaded.CustomCommand, Is.EqualTo("JT"));
            });
        }

        [Test]
        public void Load_CorruptJsonReturnsDefaults()
        {
            string path = Path.Combine(_temporaryDirectory, "settings.json");
            File.WriteAllText(path, "not-json");
            FileSettingsStore store = new FileSettingsStore(path);

            ScreenshotSettings loaded = store.Load();

            Assert.That(loaded.PixelScale, Is.EqualTo(2));
            Assert.That(loaded.OutputFormat, Is.EqualTo(ImageOutputFormat.Png));
            Assert.That(loaded.TargetLongEdge, Is.EqualTo(0));
        }
    }
}
