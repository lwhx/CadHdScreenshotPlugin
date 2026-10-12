namespace CadHdScreenshot.Core
{
	public static class PixelRiskEvaluator
	{
		public const long NormalLimit = 50000000L;

		public const long HighDefinitionLimit = 150000000L;

		public const long HighRiskLimit = 300000000L;

		public const long HardLimit = 500000000L;

		public static PixelRiskEvaluation Evaluate(PixelSize size)
		{
			long totalPixels = size.TotalPixels;
			PixelRiskLevel level = ((totalPixels > 50000000) ? ((totalPixels <= 150000000) ? PixelRiskLevel.HighDefinition : ((totalPixels <= 300000000) ? PixelRiskLevel.HighRisk : ((totalPixels > 500000000) ? PixelRiskLevel.Prohibited : PixelRiskLevel.Extreme))) : PixelRiskLevel.Normal);
			return new PixelRiskEvaluation(size, level);
		}
	}
}
