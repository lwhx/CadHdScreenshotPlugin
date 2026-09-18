using System;
using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class PixelScaleCalculatorTests
    {
        [TestCase(1, 1823, 782)]
        [TestCase(2, 3646, 1564)]
        [TestCase(3, 5469, 2346)]
        public void Scale_UsesCurrentViewportSizeAsOneX(double scale, int expectedWidth, int expectedHeight)
        {
            PixelSize result = PixelScaleCalculator.Scale(new PixelSize(1823, 782), scale);

            Assert.That(result, Is.EqualTo(new PixelSize(expectedWidth, expectedHeight)));
        }

        [Test]
        public void Scale_RoundsHalfPixelsAwayFromZero()
        {
            PixelSize result = PixelScaleCalculator.Scale(new PixelSize(3, 5), 1.5);

            Assert.That(result, Is.EqualTo(new PixelSize(5, 8)));
        }

        [TestCase(1600, 900, 3200, 3200, 1800)]
        [TestCase(900, 1600, 3200, 1800, 3200)]
        public void FitLongEdge_PreservesOrientationAndAspectRatio(
            int width,
            int height,
            int target,
            int expectedWidth,
            int expectedHeight)
        {
            PixelSize result = PixelScaleCalculator.FitLongEdge(new PixelSize(width, height), target);

            Assert.That(result, Is.EqualTo(new PixelSize(expectedWidth, expectedHeight)));
        }

        [TestCase(109, 103, 1920, 1000, 1058, 1000)]
        [TestCase(1600, 900, 1200, 1000, 1200, 675)]
        [TestCase(900, 1600, 1200, 1000, 562, 1000)]
        public void FitWithin_FillsViewportWhilePreservingAspectRatio(
            int sourceWidth,
            int sourceHeight,
            int boundsWidth,
            int boundsHeight,
            int expectedWidth,
            int expectedHeight)
        {
            PixelSize result = PixelScaleCalculator.FitWithin(
                new PixelSize(sourceWidth, sourceHeight),
                new PixelSize(boundsWidth, boundsHeight));

            Assert.That(result, Is.EqualTo(new PixelSize(expectedWidth, expectedHeight)));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void Scale_RejectsInvalidScale(double scale)
        {
            Assert.That(
                () => PixelScaleCalculator.Scale(new PixelSize(100, 100), scale),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
