namespace CadHdScreenshot.Core
{
	public sealed class PixelRiskEvaluation
	{
		public PixelSize Size { get; }

		public long TotalPixels => Size.TotalPixels;

		public PixelRiskLevel Level { get; }

		public bool IsAllowed => Level != PixelRiskLevel.Prohibited;

		public bool RequiresConfirmation
		{
			get
			{
				if (Level != PixelRiskLevel.HighRisk)
				{
					return Level == PixelRiskLevel.Extreme;
				}
				return true;
			}
		}

		internal PixelRiskEvaluation(PixelSize size, PixelRiskLevel level)
		{
			Size = size;
			Level = level;
		}
	}
}
