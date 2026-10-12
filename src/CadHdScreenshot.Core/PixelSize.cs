using System;

namespace CadHdScreenshot.Core
{
	public struct PixelSize : IEquatable<PixelSize>
	{
		public int Width { get; }

		public int Height { get; }

		public long TotalPixels => (long)Width * (long)Height;

		public int LongEdge => Math.Max(Width, Height);

		public int ShortEdge => Math.Min(Width, Height);

		public double AspectRatio => (double)Width / (double)Height;

		public PixelSize(int width, int height)
		{
			if (width <= 0)
			{
				throw new ArgumentOutOfRangeException("width", "Pixel width must be positive.");
			}
			if (height <= 0)
			{
				throw new ArgumentOutOfRangeException("height", "Pixel height must be positive.");
			}
			Width = width;
			Height = height;
		}

		public bool Equals(PixelSize other)
		{
			if (Width == other.Width)
			{
				return Height == other.Height;
			}
			return false;
		}

		public override bool Equals(object obj)
		{
			if (obj is PixelSize other)
			{
				return Equals(other);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return (Width * 397) ^ Height;
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
