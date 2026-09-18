namespace CadHdScreenshot.Core
{
    public enum PixelRiskLevel
    {
        Normal,
        HighDefinition,
        HighRisk,
        Extreme,
        Prohibited
    }

    public sealed class PixelRiskEvaluation
    {
        internal PixelRiskEvaluation(PixelSize size, PixelRiskLevel level)
        {
            Size = size;
            Level = level;
        }

        public PixelSize Size { get; }

        public long TotalPixels => Size.TotalPixels;

        public PixelRiskLevel Level { get; }

        public bool IsAllowed => Level != PixelRiskLevel.Prohibited;

        public bool RequiresConfirmation => Level == PixelRiskLevel.HighRisk || Level == PixelRiskLevel.Extreme;
    }

    public static class PixelRiskEvaluator
    {
        public const long NormalLimit = 50_000_000;
        public const long HighDefinitionLimit = 150_000_000;
        public const long HighRiskLimit = 300_000_000;
        public const long HardLimit = 500_000_000;

        public static PixelRiskEvaluation Evaluate(PixelSize size)
        {
            long pixels = size.TotalPixels;
            PixelRiskLevel level;

            if (pixels <= NormalLimit)
            {
                level = PixelRiskLevel.Normal;
            }
            else if (pixels <= HighDefinitionLimit)
            {
                level = PixelRiskLevel.HighDefinition;
            }
            else if (pixels <= HighRiskLimit)
            {
                level = PixelRiskLevel.HighRisk;
            }
            else if (pixels <= HardLimit)
            {
                level = PixelRiskLevel.Extreme;
            }
            else
            {
                level = PixelRiskLevel.Prohibited;
            }

            return new PixelRiskEvaluation(size, level);
        }
    }
}
