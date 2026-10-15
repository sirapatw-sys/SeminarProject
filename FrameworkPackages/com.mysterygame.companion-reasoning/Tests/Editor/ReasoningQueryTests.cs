using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CompanionFramework.Tests
{
    public class ReasoningQueryTests
    {
        private ReasoningProfile profile;
        [SetUp] public void Setup()
        {
            profile = ScriptableObject.CreateInstance<ReasoningProfile>();
            profile.npcId = "Nora";
            profile.topicTerms = new List<string> { "arrow", "ลูกศร", "อ่านจาก" };
            profile.rules.Add(new ReasoningRule {
                id = "right", phrases = new List<string> { "อ่านจากขวาไปซ้าย", "read from right to left" },
                requiredFacts = new List<string> { "arrow" }, cautiousLine = "correct",
                normalLine = "normal", insufficientLine = "need evidence" });
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(profile); }
        [TestCase("อ่านจากขวาไปซ้ายใช่ไหมคะ", ReasoningQueryKind.Hypothesis)]
        [TestCase("เราคิดว่าอ่านจากขวาไปซ้าย เธอว่าไงนะครับ", ReasoningQueryKind.Hypothesis)]
        [TestCase("i think read from right to left, please", ReasoningQueryKind.Hypothesis)]
        [TestCase("เธอเจออะไรบ้างครับ?", ReasoningQueryKind.Report)]
        [TestCase("เราคิดว่าเธอคงคิดถึงบ้าน", ReasoningQueryKind.None)]
        [TestCase("i think you miss home", ReasoningQueryKind.None)]
        [TestCase("เราคิดว่าเธออ่านใจเราได้", ReasoningQueryKind.None)]
        public void TopicsAndCourtesyPreserveConversationScope(string text, ReasoningQueryKind expected)
        {
            var query = ReasoningQueryMatcher.Read(profile, text);
            Assert.That(query.Kind, Is.EqualTo(expected));
            if (expected == ReasoningQueryKind.Hypothesis) Assert.That(query.Rules.Count, Is.EqualTo(1));
        }
        [TestCase("เราคิดว่าไม่ต้องอ่านจากขวาไปซ้าย")]
        [TestCase("เราคิดว่าอ่านจากขวาไปซ้ายแล้วเปิดให้เลย")]
        public void NegatedOrCompoundHypothesesAreNeverForcedIntoKnownRule(string text)
        {
            var query = ReasoningQueryMatcher.Read(profile, text);
            Assert.That(query.Kind, Is.EqualTo(ReasoningQueryKind.Hypothesis));
            Assert.That(query.Rules, Is.Empty);
        }
        [Test]
        public void InvalidUnicodeDoesNotCrashTheDialogue()
        {
            Assert.That(ReasoningQueryMatcher.Read(profile, "\ud800").Kind, Is.EqualTo(ReasoningQueryKind.None));
        }
        [Test]
        public void ExactAuthoredPhraseCanIncludeAnIntroductoryWord()
        {
            profile.rules[0].phrases = new List<string> { "maybe the arrow is wrong" };
            Assert.That(ReasoningQueryMatcher.Read(profile, "maybe the arrow is wrong").Rules.Count, Is.EqualTo(1));
        }
        [Test]
        public void ProfileRejectsAmbiguousNormalizedAliasesAndUnsafeFacts()
        {
            Assert.That(profile.ValidateConfiguration(), Is.Empty);
            profile.rules.Add(new ReasoningRule { id = "other", requiredFacts = new List<string> { "hidden_answer" },
                phrases = new List<string> { "READ FROM RIGHT TO LEFT" }, insufficientLine = "need evidence" });
            Assert.That(profile.ValidateConfiguration(f => f == "arrow"), Has.Some.Contains("ambiguous"));
            Assert.That(profile.ValidateConfiguration(f => f == "arrow"), Has.Some.Contains("unsafe"));
        }
        private class World : IReasoningWorld
        {
            public int AllowedDetail { get; set; }
            public bool knows = true;
            public bool KnowsFact(string id) => knows;
            public bool HasFlag(string id) => false;
        }
        [Test]
        public void ProfileUsesSharedCautiousReplyRatherThanRuleTruthSignal()
        {
            var result = profile.Evaluate(profile.rules[0], new World { AllowedDetail = 1 });
            Assert.That(result.Line, Is.EqualTo(profile.cautiousLine));
            Assert.That(result.Line, Is.Not.EqualTo("correct"));
        }
        [Test]
        public void ZeroDisclosureBlocksInsufficientEvidenceTextToo()
        {
            var world = new World { AllowedDetail = 0, knows = false };
            Assert.That(profile.Evaluate(profile.rules[0], world).Outcome, Is.EqualTo(ReasoningOutcome.Unavailable));
        }
    }
}
