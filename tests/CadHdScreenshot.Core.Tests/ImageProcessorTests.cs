using System.Drawing;
using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class ImageProcessorTests
    {
        [Test]
        public void Process_BorderPreservesTransparentInterior()
        {
            using (Bitmap source = new Bitmap(2, 1))
            {
                source.SetPixel(0, 0, Color.White);
                source.SetPixel(1, 0, Color.Red);
                using (Bitmap result = ImageProcessor.Process(source,
                    new ImageProcessingOptions { WhiteToTransparent = true, BorderPixels = 1 }))
                {
                    Assert.That(result.GetPixel(1, 1).A, Is.EqualTo(0));
                    Assert.That(result.GetPixel(2, 1).ToArgb(), Is.EqualTo(Color.Red.ToArgb()));
                    Assert.That(result.GetPixel(0, 0).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
                    Assert.That(source.GetPixel(0, 0).A, Is.EqualTo(255));
                }
            }
        }

        [Test]
        public void Process_BorderPreservesPartialAlpha()
        {
            using (Bitmap source = new Bitmap(1, 1))
            {
                source.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));
                using (Bitmap result = ImageProcessor.Process(source,
                    new ImageProcessingOptions { BorderPixels = 1 }))
                {
                    Assert.That(result.GetPixel(1, 1).A, Is.EqualTo(128));
                }
            }
        }
        [Test]
        public void Process_WhiteToTransparentClearsNearWhitePixels()
        {
            using (Bitmap source = new Bitmap(2, 1))
            {
                source.SetPixel(0, 0, Color.White);
                source.SetPixel(1, 0, Color.FromArgb(249, 249, 249));

                using (Bitmap result = ImageProcessor.Process(
                    source,
                    new ImageProcessingOptions { WhiteToTransparent = true, WhiteThreshold = 250 }))
                {
                    Assert.That(result.GetPixel(0, 0).A, Is.EqualTo(0));
                    Assert.That(result.GetPixel(1, 0).A, Is.EqualTo(255));
                }
            }
        }

        [Test]
        public void Process_GrayscaleProducesEqualColorChannels()
        {
            using (Bitmap source = new Bitmap(1, 1))
            {
                source.SetPixel(0, 0, Color.FromArgb(20, 100, 220));

                using (Bitmap result = ImageProcessor.Process(
                    source,
                    new ImageProcessingOptions { ColorMode = ScreenshotColorMode.Grayscale }))
                {
                    Color color = result.GetPixel(0, 0);
                    Assert.That(color.R, Is.EqualTo(color.G));
                    Assert.That(color.G, Is.EqualTo(color.B));
                }
            }
        }

        [Test]
        public void Process_AddsStableBorderWithoutScalingSource()
        {
            using (Bitmap source = new Bitmap(2, 3))
            {
                source.SetPixel(0, 0, Color.Red);

                using (Bitmap result = ImageProcessor.Process(
                    source,
                    new ImageProcessingOptions { BorderPixels = 2, BorderColor = Color.White }))
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(result.Size, Is.EqualTo(new Size(6, 7)));
                        Assert.That(result.GetPixel(0, 0).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
                        Assert.That(result.GetPixel(2, 2).ToArgb(), Is.EqualTo(Color.Red.ToArgb()));
                    });
                }
            }
        }

        [Test]
        public void Process_WhiteBackgroundFlattensTransparency()
        {
            using (Bitmap source = new Bitmap(1, 1))
            {
                source.SetPixel(0, 0, Color.FromArgb(0, 10, 20, 30));

                using (Bitmap result = ImageProcessor.Process(
                    source,
                    new ImageProcessingOptions { ColorMode = ScreenshotColorMode.WhiteBackground }))
                {
                    Assert.That(result.GetPixel(0, 0).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
                }
            }
        }
    }
}
