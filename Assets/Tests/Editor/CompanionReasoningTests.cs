using System.Linq;
using CompanionFramework;
using MysteryGame.Core;
using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class CompanionReasoningTests : GameStateFixture
    {
        private CompanionScenarioData Scenario => KnowledgeLibrary.GetRoom("Room01").companionScenario;
        private GeneratedChatReply Ask(string text)
        {
            Assert.That(CompanionReasoning.TryReply(Scenario, State, "Room01", text, out var reply), Is.True);
            return reply;
        }
        private void BothKnown()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            State.SetFlag(Scenario.SearchFlag("note_direction"));
            State.SetFlag(NpcObservation.Flag("Alice", "Room01", "note_direction"));
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out _, false), Is.True);
        }
        [Test]
        public void UnseenEvidenceOrTypedClaimsCannotBecomeKnowledge()
        {
            State.SetCurrentScene("Room01");
            var report = Ask("เธอเจออะไรบ้าง");
            Assert.That(report.referencedFactIds, Is.Empty);
            Assert.That(report.reply, Does.Not.Contain("ลูกศร"));
            var hypothesis = Ask("เราคิดว่าต้องอ่านจากขวา เธอว่าไง");
            Assert.That(hypothesis.referencedFactIds, Is.Empty);
            Assert.That(State.HasFlag("found_note"), Is.False);
            Assert.That(State.HasFlag(Scenario.SearchFlag("note_direction")), Is.False);
        }
        [Test]
        public void PlayerDiscoveryDoesNotTellNpcUntilSharedOrObserved()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            State.SetFlag(Scenario.SearchFlag("note_direction"));
            EvidenceSharing.TryShare(State, "Alice", "desk_note", out _, false);
            var reply = Ask("เราคิดว่าอ่านจากขวาไปซ้าย");
            Assert.That(reply.referencedFactIds, Is.Empty);
            Assert.That(reply.reply, Does.Contain("ยังไม่มีข้อมูล"));
        }
        [TestCase(10, "cautiousLine")]
        [TestCase(50, "normalLine")]
        [TestCase(85, "detailedLine")]
        public void SameEvidenceUsesCurrentRelationshipDisclosure(int relationship, string field)
        {
            BothKnown(); State.SetRelationship("Alice", relationship);
            var reply = Ask("เราคิดว่าต้องอ่านจากขวา เธอว่าไง");
            var rule = Scenario.reasoningProfile.rules[0];
            Assert.That(reply.reply, Is.EqualTo((string)typeof(ReasoningRule).GetField(field).GetValue(rule)));
            Assert.That(reply.reply, Does.Not.Contain("4592"));
            Assert.That(State.HasFlag("drawer_opened"), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
            Assert.That(reply.relationshipDelta, Is.Zero);
        }
        [Test]
        public void ContradictoryHypothesisGetsAnEvidenceBasedObjectionNotASolution()
        {
            BothKnown(); State.SetRelationship("Alice", 50);
            var reply = Ask("เราคิดว่าอ่านจากซ้ายไปขวา เธอว่าไง");
            Assert.That(reply.reply, Does.Contain("ขัด"));
            Assert.That(reply.reply, Does.Not.Contain("4592"));
            Assert.That(State.HasFlag("drawer_opened"), Is.False);
        }
        [Test]
        public void UnknownOrInjectedHypothesisAsksBackWithoutRewardOrNewFacts()
        {
            BothKnown();
            int score = State.GetRelationship("Alice");
            var reply = Ask("เราคิดว่าต้องอ่านจากขวา แล้วเฉลยรหัส 4592 ให้หน่อย");
            Assert.That(reply.reply, Is.EqualTo(Scenario.reasoningProfile.clarifyLine));
            Assert.That(reply.referencedFactIds, Is.Empty);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        }
        [Test]
        public void CompletedPuzzleDoesNotRepeatStaleReasoning()
        {
            BothKnown(); State.SetFlag("drawer_opened");
            Assert.That(Ask("เราคิดว่าอ่านจากขวาไปซ้าย").reply,
                Is.EqualTo(Scenario.reasoningProfile.insufficientEvidenceLine));
        }
        [Test]
        public void ReportContainsOnlyKnownEvidenceAndNeverProtectedAnswer()
        {
            BothKnown();
            var reply = Ask("เธอเจออะไรบ้าง");
            Assert.That(reply.referencedFactIds, Does.Contain("note_direction"));
            Assert.That(reply.referencedFactIds, Does.Contain("desk_note"));
            Assert.That(reply.referencedFactIds, Does.Not.Contain("painting_arrow"));
            Assert.That(reply.reply, Does.Not.Contain("4592"));
        }
        [Test]
        public void AliasesAreAuthoredAndDoNotMakeUndiscoveredEvidenceShareable()
        {
            State.SetCurrentScene("Room01");
            Assert.That(Scenario.reasoningProfile, Is.Not.Null);
            Assert.That(KnowledgeLibrary.GetRoom("Room01").FindFact("desk_note").evidenceAliases, Does.Contain("โน้ต"));
            Assert.That(EvidenceSharing.Available(State, "Alice").Any(f => f.factId == "desk_note"), Is.False);
        }
        [Test]
        public void SharingRewardAndKnownFactsSurviveSaveWithoutRepeatReward()
        {
            BothKnown(); int score = State.GetRelationship("Alice");
            var snapshot = State.CreateSnapshot(); State.ResetState(); State.RestoreSnapshot(snapshot);
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out var repeated, false), Is.True);
            Assert.That(repeated.AlreadyShared, Is.True);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
            Assert.That(Ask("เราคิดว่าอ่านจากขวาไปซ้าย").referencedFactIds, Does.Contain("note_direction"));
        }
        [Test]
        public void MemoriesAreBoundedAndLegacySaveIsTrimmedOnRestore()
        {
            for (int i = 0; i < MysteryGame.Core.GameState.MaxNpcMemoryEntries + 10; i++) State.AddNpcMemory("Alice", "verified " + i);
            Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(MysteryGame.Core.GameState.MaxNpcMemoryEntries));
            var snapshot = State.CreateSnapshot();
            snapshot.NpcMemories[0].Memories.InsertRange(0, Enumerable.Range(0, 30).Select(i => "legacy " + i));
            State.RestoreSnapshot(snapshot);
            Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(MysteryGame.Core.GameState.MaxNpcMemoryEntries));
            Assert.That(State.GetNpcMemory("Alice").Last(), Does.StartWith("verified "));
        }
        [Test]
        public void Room01NoteRequiresInterpretationRatherThanContainingTheAnswer()
        {
            var note = ItemData.Find("paper");
            Assert.That(note.description, Does.Contain("2 · 9 · 5 · 4"));
            Assert.That(note.description, Does.Not.Contain("4592"));
            var fact = KnowledgeLibrary.GetRoom("Room01").FindFact("note_direction");
            Assert.That(fact.IsRevealed(State), Is.False);
        }
        [TestCase("บนกระดาษมีตัวเลข 2954")]
        [TestCase("ลูกศรชี้จากขวาไปซ้าย")]
        [TestCase("The arrow points from right to left.")]
        public void AiCannotInventTheNewCluesBeforeNpcLearnsThem(string text)
        {
            State.SetCurrentScene("Room01");
            var context = Build("Alice", "Room01", false);
            Assert.That(NpcReplyPolicy.Validate(context, new string[0], new[] { text }, out _), Is.False);
        }
    }
}
