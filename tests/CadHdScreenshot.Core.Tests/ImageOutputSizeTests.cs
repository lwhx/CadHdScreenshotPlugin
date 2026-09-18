using System;
using System.Drawing;
using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class ImageOutputSizeTests
    {
        [TestCase(0, 100, 50)]
        [TestCase(10, 120, 70)]
        public void CalculateOutputSize_IncludesBothBorders(int border, int width, int height)
        {
            Assert.That(ImageProcessor.CalculateOutputSize(new PixelSize(100, 50), border),
                Is.EqualTo(new PixelSize(width, height)));
        }

        [Test]
        public void CalculateOutputSize_RejectsNegativeBorder()
        {
            Assert.That(() => ImageProcessor.CalculateOutputSize(new PixelSize(1, 1), -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(1, int.MaxValue)]
        [TestCase(int.MaxValue, 1)]
        public void CalculateOutputSize_RejectsOverflow(int width, int border)
        {
            Assert.That(() => ImageProcessor.CalculateOutputSize(new PixelSize(width, 1), border),
                Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void Evaluate_CountsBorderTowardHardLimit()
        {
            PixelSize source = new PixelSize(25000, 20000);
            Assert.That(PixelRiskEvaluator.Evaluate(source).IsAllowed, Is.True);
            Assert.That(PixelRiskEvaluator.Evaluate(ImageProcessor.CalculateOutputSize(source, 1)).IsAllowed, Is.False);
        }

        [Test]
        public void Process_RejectsOversizedBorderBeforeAllocatingOutput()
        {
            using (Bitmap source = new Bitmap(1, 1))
            {
                Assert.That(() => ImageProcessor.Process(source,
                    new ImageProcessingOptions { BorderPixels = 12000 }),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(source.Width, Is.EqualTo(1));
            }
        }

        [Test]
        public void Process_TransformsEveryRowAndPreservesAlpha()
        {
            using (Bitmap source = new Bitmap(3, 4))
            {
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 3; x++)
                        source.SetPixel(x, y, Color.FromArgb(64 + y * 32, x * 50, y * 60, 100));
                using (Bitmap result = ImageProcessor.Process(source,
                    new ImageProcessingOptions { ColorMode = ScreenshotColorMode.Grayscale }))
                {
                    for (int y = 0; y < 4; y++)
                        for (int x = 0; x < 3; x++)
                        {
                            Color original = source.GetPixel(x, y);
                            int expected = (int)Math.Round(original.R * .299 + original.G * .587 + original.B * .114);
                            Color actual = result.GetPixel(x, y);
                            Assert.That(actual.A, Is.EqualTo(original.A));
                            Assert.That(actual.R, Is.EqualTo(expected).Within(1));
                            Assert.That(actual.R, Is.EqualTo(actual.G));
                            Assert.That(actual.G, Is.EqualTo(actual.B));
                        }
                }
            }
        }
    }
}
