using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class CompanionPresentationTests
    {
        [TestCase("moveAcceleration")]
        [TestCase("moveDeceleration")]
        [TestCase("followStartBuffer")]
        [TestCase("activityLabelDistance")]
        [TestCase("walkAnimationPace")]
        public void InvalidPresentationSettingsAreRejected(string field)
        {
            var scenario = Object.Instantiate(KnowledgeLibrary.GetRoom("Room01").companionScenario);
            try
            {
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0f })
                {
                    typeof(CompanionScenarioData).GetField(field).SetValue(scenario, value);
                    Assert.That(scenario.ValidateConfiguration(), Is.Not.Empty, field + "=" + value);
                }
            }
            finally { Object.DestroyImmediate(scenario); }
        }

        [Test]
        public void Room01UsesSlowWalkingAndASeparateFollowRestartThreshold()
        {
            var scenario = KnowledgeLibrary.GetRoom("Room01").companionScenario;
            Assert.That(scenario.ValidateConfiguration(), Is.Empty);
            Assert.That(scenario.moveSpeed, Is.EqualTo(2.6f));
            Assert.That(scenario.moveAcceleration, Is.GreaterThan(0f));
            Assert.That(scenario.moveDeceleration, Is.GreaterThan(0f));
            Assert.That(scenario.followStartBuffer, Is.GreaterThan(0.06f));
            Assert.That(scenario.walkAnimationPace, Is.InRange(0.1f, 0.99f));
        }

        [Test]
        public void AnimationAndProximitySettingsHaveBoundedRanges()
        {
            var scenario = Object.Instantiate(KnowledgeLibrary.GetRoom("Room01").companionScenario);
            try
            {
                scenario.walkAnimationPace = 1.51f;
                Assert.That(scenario.ValidateConfiguration(), Is.Not.Empty);
                scenario.walkAnimationPace = 0.55f; scenario.followStartBuffer = 0.1f;
                Assert.That(scenario.ValidateConfiguration(), Is.Not.Empty);
                scenario.followStartBuffer = 0.45f; scenario.activityLabelDistance = 0.4f;
                Assert.That(scenario.ValidateConfiguration(), Is.Not.Empty);
            }
            finally { Object.DestroyImmediate(scenario); }
        }
    }
}
