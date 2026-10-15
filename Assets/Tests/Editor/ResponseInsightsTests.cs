using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;

namespace MysteryGame.Tests
{
    /// <summary>The numbers line under an NPC's reply.</summary>
    public class ResponseInsightsTests
    {
        [TestCase(HintLevel.None, 0)]
        [TestCase(HintLevel.Vague, 33)]
        [TestCase(HintLevel.Normal, 67)]
        [TestCase(HintLevel.Explicit, 100)]
        public void HintShareFollowsTheLevelTheFrameworkAllowed(HintLevel level, int percent)
        {
            Assert.That(ResponseInsights.HintPercent(level), Is.EqualTo(percent));
        }

        [Test]
        public void KindnessIsCentredOnNeutralAndCappedAtTheMessageLimits()
        {
            Assert.That(ResponseInsights.KindPercent(0), Is.EqualTo(50));
            Assert.That(ResponseInsights.KindPercent(RelationshipTuning.MaxGainPerMessage), Is.EqualTo(100));
            Assert.That(ResponseInsights.KindPercent(-RelationshipTuning.MaxLossPerMessage), Is.EqualTo(0));
            Assert.That(ResponseInsights.KindPercent(999), Is.EqualTo(100));
            Assert.That(ResponseInsights.KindPercent(-999), Is.EqualTo(0));
            Assert.That(ResponseInsights.KindPercent(5), Is.GreaterThan(50).And.LessThan(100));
            Assert.That(ResponseInsights.KindPercent(-5), Is.GreaterThan(0).And.LessThan(50));
        }

        [Test]
        public void TheLineShowsHintFriendlinessAndCloseness()
        {
            string line = ResponseInsights.Format(67, 80, 52);
            Assert.That(line, Does.Contain("ใบ้ 67%"));
            Assert.That(line, Does.Contain("ความเป็นมิตรของผู้เล่น 80%"));
            Assert.That(line, Does.Contain("ความสนิทในการตอบ 52%"));
            Assert.That(ResponseInsights.Format(150, -10, 120), Does.Contain("ใบ้ 100%").And.Contain("ความเป็นมิตรของผู้เล่น 0%").And.Contain("100%〕"));
        }
    }
}
