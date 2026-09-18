using System;

namespace CadHdScreenshot.Core
{
    public static class PixelScaleCalculator
    {
        public static PixelSize Scale(PixelSize baseSize, double scale)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), "Pixel scale must be a finite positive number.");
            }

            return new PixelSize(
                ScaleDimension(baseSize.Width, scale),
                ScaleDimension(baseSize.Height, scale));
        }

        public static PixelSize FitLongEdge(PixelSize baseSize, int targetLongEdge)
        {
            if (targetLongEdge <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetLongEdge), "Target long edge must be positive.");
            }

            if (baseSize.Width >= baseSize.Height)
            {
                return new PixelSize(
                    targetLongEdge,
                    ScaleProportionally(baseSize.Height, targetLongEdge, baseSize.Width));
            }

            return new PixelSize(
                ScaleProportionally(baseSize.Width, targetLongEdge, baseSize.Height),
                targetLongEdge);
        }

        public static PixelSize FitWithin(PixelSize sourceAspect, PixelSize bounds)
        {
            if (sourceAspect.AspectRatio >= bounds.AspectRatio)
            {
                return new PixelSize(
                    bounds.Width,
                    Math.Max(1, (int)Math.Floor(bounds.Width / sourceAspect.AspectRatio)));
            }

            return new PixelSize(
                Math.Max(1, (int)Math.Floor(bounds.Height * sourceAspect.AspectRatio)),
                bounds.Height);
        }

        private static int ScaleDimension(int dimension, double scale)
        {
            double scaled = dimension * scale;
            if (scaled > int.MaxValue)
            {
                throw new OverflowException("Scaled pixel dimension exceeds Int32.MaxValue.");
            }

            return Math.Max(1, checked((int)Math.Round(scaled, MidpointRounding.AwayFromZero)));
        }

        private static int ScaleProportionally(int dimension, int targetLongEdge, int originalLongEdge)
        {
            double scaled = (double)dimension * targetLongEdge / originalLongEdge;
            return Math.Max(1, checked((int)Math.Round(scaled, MidpointRounding.AwayFromZero)));
        }
    }
}
