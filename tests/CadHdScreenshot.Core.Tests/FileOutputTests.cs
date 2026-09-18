using System;
using System.Drawing;
using System.IO;
using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class FileOutputTests
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
        public void GetAvailablePath_DoesNotOverwriteSameSecondCapture()
        {
            DateTime timestamp = new DateTime(2026, 9, 12, 13, 45, 30);
            string first = SafeFileNameGenerator.GetAvailablePath(
                _temporaryDirectory,
                "capture",
                ImageOutputFormat.Png,
                timestamp);
            File.WriteAllText(first, "existing");

            string second = SafeFileNameGenerator.GetAvailablePath(
                _temporaryDirectory,
                "capture",
                ImageOutputFormat.Png,
                timestamp);

            Assert.That(Path.GetFileName(second), Is.EqualTo("capture_20260912_134530_001.png"));
        }

        [Test]
        public void SaveUnique_WritesReadablePng()
        {
            using (Bitmap image = new Bitmap(4, 3))
            {
                image.SetPixel(1, 1, Color.Blue);
                ImageFileOutput output = new ImageFileOutput();

                string path = output.SaveUnique(
                    image,
                    _temporaryDirectory,
                    "capture",
                    ImageOutputFormat.Png,
                    92,
                    new DateTime(2026, 9, 12, 13, 45, 30));

                using (Image loaded = Image.FromFile(path))
                {
                    Assert.That(loaded.Size, Is.EqualTo(new Size(4, 3)));
                }
            }
        }

        [Test]
        public void AtomicWrite_WhenWriterFailsPreservesExistingFile()
        {
            string path = Path.Combine(_temporaryDirectory, "capture.png");
            File.WriteAllText(path, "original");

            Assert.That(
                () => AtomicFileWriter.Write(
                    path,
                    stream =>
                    {
                        stream.WriteByte(42);
                        throw new InvalidOperationException("simulated encoder failure");
                    },
                    overwrite: true),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(File.ReadAllText(path), Is.EqualTo("original"));
            Assert.That(Directory.GetFiles(_temporaryDirectory, "*.tmp"), Is.Empty);
        }
    }
}
