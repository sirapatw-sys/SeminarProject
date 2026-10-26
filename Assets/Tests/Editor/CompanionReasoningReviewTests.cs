using CompanionFramework;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class CompanionReasoningReviewTests : GameStateFixture
    {
        private CompanionScenarioData Scenario => KnowledgeLibrary.GetRoom("Room01").companionScenario;
        private void BothKnown()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            State.SetFlag(Scenario.SearchFlag("note_direction"));
            State.SetFlag(NpcObservation.Flag("Alice", "Room01", "note_direction"));
            EvidenceSharing.TryShare(State, "Alice", "desk_note", out _, false);
        }
        [Test]
        public void LowTrustCannotProbeCorrectDirectionFromDifferentReplies()
        {
            BothKnown(); State.SetRelationship("Alice", 10);
            CompanionReasoning.TryReply(Scenario, State, "Room01", "เราคิดว่าอ่านจากขวาไปซ้าย", out var right);
            CompanionReasoning.TryReply(Scenario, State, "Room01", "เราคิดว่าอ่านจากซ้ายไปขวา", out var left);
            Assert.That(right.reply, Is.EqualTo(left.reply));
            Assert.That(right.reply, Does.Not.Contain("เดาตรงกัน"));
        }
        [TestCase("เธอเจออะไรบ้างคะ")]
        [TestCase("เธอเจออะไรบ้างครับ?")]
        [TestCase("อ่านจากขวาไปซ้ายใช่ไหมคะ")]
        [TestCase("เราคิดว่าต้องอ่านจากขวา เธอว่าไงนะครับ")]
        public void EverydayQuestionsAndCourtesyReachReasoning(string text)
        {
            BothKnown();
            Assert.That(CompanionReasoning.TryReply(Scenario, State, "Room01", text, out var reply), Is.True);
            Assert.That(reply.reply, Is.Not.EqualTo(Scenario.reasoningProfile.clarifyLine));
        }
        [TestCase("เราคิดว่าเธอคงคิดถึงบ้าน")]
        [TestCase("ฉันคิดว่าเราคงเป็นเพื่อนกันได้")]
        [TestCase("i think you miss home")]
        public void SocialThoughtsRemainOrdinaryConversation(string text)
        {
            BothKnown();
            Assert.That(CompanionReasoning.TryReply(Scenario, State, "Room01", text, out _), Is.False);
        }
        [Test]
        public void CompletedPuzzleDoesNotPretendTheEvidenceIsMissing()
        {
            BothKnown(); State.SetFlag("drawer_opened");
            CompanionReasoning.TryReply(Scenario, State, "Room01", "เราคิดว่าอ่านจากขวาไปซ้าย", out var reply);
            Assert.That(reply.reply, Does.Not.Contain("หลักฐานที่ทั้งคู่"));
            Assert.That(reply.reply, Does.Not.Contain("ยังยืนยันไม่ได้"));
        }
        [Test]
        public void EmptyCriticalReplyIsRejectedByConfigurationValidation()
        {
            var scenario = Object.Instantiate(Scenario);
            scenario.reasoningProfile = Object.Instantiate(scenario.reasoningProfile);
            try
            {
                scenario.reasoningProfile.clarifyLine = "";
                Assert.That(scenario.ValidateConfiguration(), Is.Not.Empty);
            }
            finally { Object.DestroyImmediate(scenario.reasoningProfile); Object.DestroyImmediate(scenario); }
        }
        [Test]
        public void ReflectionCannotTurnPlayersClaimIntoAWitnessedSuccess()
        {
            BothKnown(); State.SetFlag("drawer_opened");
            int score = State.GetRelationship("Alice");
            Assert.That(CompanionReflection.TryReply(Scenario, State, "เราทำได้แล้วค่ะ", out var reply), Is.True);
            Assert.That(reply.reply, Is.EqualTo(Scenario.unverifiedOutcomeLine));
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        }
        [Test]
        public void WitnessedSuccessSurvivesSaveAndDoesNotRewardRepeatingTheReaction()
        {
            BothKnown(); State.SetFlag("drawer_opened");
            State.SetFlag(Scenario.Flag("observed.drawer_seen"));
            var snapshot = State.CreateSnapshot(); State.ResetState(); State.RestoreSnapshot(snapshot);
            int score = State.GetRelationship("Alice");
            for (int i = 0; i < 3; i++)
            {
                Assert.That(CompanionReflection.TryReply(Scenario, State, "เราทำได้แล้วค่ะ", out var reply), Is.True);
                Assert.That(reply.reply, Does.Contain("เธอทำได้แล้ว"));
                Assert.That(reply.relationshipDelta, Is.Zero);
            }
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        }
        [Test]
        public void OldFailureDoesNotOverrideLaterSuccess()
        {
            BothKnown(); State.SetFlag(Scenario.Flag("observed.drawer_failed")); State.SetFlag("drawer_opened");
            CompanionReflection.TryReply(Scenario, State, "ยังเปิดไม่ออกเลย", out var reply);
            Assert.That(reply.reply, Is.EqualTo(Scenario.unverifiedOutcomeLine));
        }
    }
}
