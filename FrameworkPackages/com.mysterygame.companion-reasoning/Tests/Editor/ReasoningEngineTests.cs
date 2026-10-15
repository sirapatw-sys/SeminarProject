using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace CompanionFramework.Tests
{
    public class ReasoningEngineTests
    {
        private sealed class World : IReasoningWorld
        {
            public HashSet<string> facts = new HashSet<string>();
            public HashSet<string> flags = new HashSet<string>();
            public int AllowedDetail { get; set; }
            public bool KnowsFact(string id) => facts.Contains(id);
            public bool HasFlag(string id) => flags.Contains(id);
        }
        private ReasoningRule Rule() => new ReasoningRule {
            id = "cable", requiredFacts = new List<string> { "loose_cable", "power_off" },
            cautiousLine = "cautious", normalLine = "normal", detailedLine = "detailed",
            insufficientLine = "need evidence"
        };
        [TestCase(0, null)]
        [TestCase(1, "cautious")]
        [TestCase(2, "normal")]
        [TestCase(3, "detailed")]
        public void DisclosureFollowsHostLimit(int detail, string expected)
        {
            var world = new World { AllowedDetail = detail };
            world.facts.UnionWith(new[] { "loose_cable", "power_off" });
            var result = ReasoningEngine.Evaluate(Rule(), world);
            Assert.That(result.Line, Is.EqualTo(expected));
            Assert.That(result.Outcome, Is.EqualTo(detail == 0 ? ReasoningOutcome.Unavailable : ReasoningOutcome.Consideration));
        }
        [Test]
        public void MissingKnowledgeNeverReturnsDetailedLineOrReferences()
        {
            var world = new World { AllowedDetail = 3 };
            world.facts.Add("loose_cable");
            var result = ReasoningEngine.Evaluate(Rule(), world);
            Assert.That(result.Outcome, Is.EqualTo(ReasoningOutcome.InsufficientEvidence));
            Assert.That(result.ReferencedFacts, Is.Empty);
            Assert.That(result.Line, Is.EqualTo("need evidence"));
        }
        [Test]
        public void MissingCautiousLineDoesNotFallForwardToNormal()
        {
            var world = new World { AllowedDetail = 1 };
            world.facts.UnionWith(new[] { "loose_cable", "power_off" });
            var rule = Rule(); rule.cautiousLine = "";
            Assert.That(ReasoningEngine.Evaluate(rule, world).Outcome, Is.EqualTo(ReasoningOutcome.Unavailable));
        }
        [Test]
        public void CompletedOrUnavailableRuleIsWithheld()
        {
            var world = new World { AllowedDetail = 3 };
            world.facts.UnionWith(new[] { "loose_cable", "power_off" });
            var rule = Rule(); rule.requiredFlags.Add("available");
            Assert.That(ReasoningEngine.Evaluate(rule, world).Outcome, Is.EqualTo(ReasoningOutcome.Unavailable));
            world.flags.Add("available"); rule.excludedFlags.Add("repaired"); world.flags.Add("repaired");
            Assert.That(ReasoningEngine.Evaluate(rule, world).Outcome, Is.EqualTo(ReasoningOutcome.Unavailable));
        }
        [Test]
        public void HostCanAddAMechanicWithoutEditingCoreAndRejectRepeatedCompletion()
        {
            int count = 0;
            var registry = new CompanionEffectRegistry();
            registry.Register("repair-device", () => { if (count != 0) return false; count++; return true; });
            Assert.That(registry.TryExecute("invented-action"), Is.False);
            Assert.That(registry.TryExecute("repair-device"), Is.True);
            Assert.That(registry.TryExecute("repair-device"), Is.False);
            Assert.That(count, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => registry.Register("repair-device", () => true));
        }
    }
}
