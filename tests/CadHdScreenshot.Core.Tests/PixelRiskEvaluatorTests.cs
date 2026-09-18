using NUnit.Framework;

namespace CadHdScreenshot.Core.Tests
{
    [TestFixture]
    public sealed class PixelRiskEvaluatorTests
    {
        [TestCase(10000, 5000, PixelRiskLevel.Normal)]
        [TestCase(10001, 5000, PixelRiskLevel.HighDefinition)]
        [TestCase(15000, 10000, PixelRiskLevel.HighDefinition)]
        [TestCase(15001, 10000, PixelRiskLevel.HighRisk)]
        [TestCase(20000, 15000, PixelRiskLevel.HighRisk)]
        [TestCase(20001, 15000, PixelRiskLevel.Extreme)]
        [TestCase(25000, 20000, PixelRiskLevel.Extreme)]
        [TestCase(25001, 20000, PixelRiskLevel.Prohibited)]
        public void Evaluate_UsesDocumentedPixelBoundaries(
            int width,
            int height,
            PixelRiskLevel expectedLevel)
        {
            PixelRiskEvaluation result = PixelRiskEvaluator.Evaluate(new PixelSize(width, height));

            Assert.That(result.Level, Is.EqualTo(expectedLevel));
        }

        [Test]
        public void Evaluate_ProhibitsMoreThanFiveHundredMillionPixels()
        {
            PixelRiskEvaluation result = PixelRiskEvaluator.Evaluate(new PixelSize(25001, 20000));

            Assert.That(result.IsAllowed, Is.False);
            Assert.That(result.RequiresConfirmation, Is.False);
        }

        [TestCase(15001, 10000)]
        [TestCase(20001, 15000)]
        public void Evaluate_RequiresConfirmationForRiskZones(int width, int height)
        {
            PixelRiskEvaluation result = PixelRiskEvaluator.Evaluate(new PixelSize(width, height));

            Assert.That(result.RequiresConfirmation, Is.True);
        }
    }
}
