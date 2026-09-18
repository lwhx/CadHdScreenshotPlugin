using System.Drawing;

namespace CadHdScreenshot.Core
{
    public sealed class ImageProcessingOptions
    {
        public ScreenshotColorMode ColorMode { get; set; } = ScreenshotColorMode.Original;

        public bool WhiteToTransparent { get; set; }

        public byte WhiteThreshold { get; set; } = 250;

        public byte BlackAndWhiteThreshold { get; set; } = 180;

        public int BorderPixels { get; set; }

        public Color BorderColor { get; set; } = Color.White;

        public Color AccentAColor { get; set; } = Color.FromArgb(220, 40, 40);

        public Color AccentBColor { get; set; } = Color.FromArgb(30, 110, 210);

        public int AccentTolerance { get; set; } = 100;
    }
}
