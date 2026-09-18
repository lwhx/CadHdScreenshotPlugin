using System;

namespace CadHdScreenshot.Core
{
    public struct PixelSize : IEquatable<PixelSize>
    {
        public PixelSize(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Pixel width must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Pixel height must be positive.");
            }

            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public long TotalPixels => (long)Width * Height;

        public int LongEdge => Math.Max(Width, Height);

        public int ShortEdge => Math.Min(Width, Height);

        public double AspectRatio => (double)Width / Height;

        public bool Equals(PixelSize other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object obj)
        {
            return obj is PixelSize other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Width * 397) ^ Height;
            }
        }

        public override string ToString()
        {
            return Width + " x " + Height;
        }

        public static bool operator ==(PixelSize left, PixelSize right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PixelSize left, PixelSize right)
        {
            return !left.Equals(right);
        }
    }
}
