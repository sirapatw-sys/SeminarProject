using System.Linq;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class CompanionDiscoveryTests : GameStateFixture
    {
        private CompanionScenarioData Scenario => KnowledgeLibrary.GetRoom("Room01").companionScenario;
        private void Seed(string id)
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            State.SetFlag(Scenario.SearchFlag(id)); State.SetFlag(NpcObservation.Flag("Alice", "Room01", id));
        }
        [Test]
        public void PlayerOnlyDiscoveryCannotCreateAnNpcInvitation()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note"); State.SetFlag(Scenario.SearchFlag("note_direction"));
            Assert.That(CompanionDiscovery.Pending(Scenario, State, "Room01"), Is.Empty);
        }
        [Test]
        public void ObservedUnrevealedFactsAreNotReported()
        {
            State.SetCurrentScene("Room01"); State.SetFlag(NpcObservation.Flag("Alice", "Room01", "note_direction"));
            Assert.That(CompanionDiscovery.Pending(Scenario, State, "Room01"), Is.Empty);
        }
        [Test]
        public void PendingQueryIsReadOnlyAndOnlyVerifiedReportsAcknowledgeFacts()
        {
            Seed("note_direction"); int score = State.GetRelationship("Alice");
            Assert.That(CompanionDiscovery.Pending(Scenario, State, "Room01").Select(f => f.factId), Is.EqualTo(new[] { "note_direction" }));
            Assert.That(State.HasFlag(CompanionDiscovery.ReportedFlag(Scenario, "note_direction")), Is.False);
            Assert.That(CompanionDiscovery.Acknowledge(Scenario, State, "Room01", new[] { "drawer_code", "unknown", "note_direction", "note_direction" }), Is.EqualTo(1));
            Assert.That(CompanionDiscovery.Acknowledge(Scenario, State, "Room01", new[] { "note_direction" }), Is.Zero);
            Assert.That(State.HasFlag(CompanionDiscovery.ReportedFlag(Scenario, "drawer_code")), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score)); Assert.That(State.HasFlag("drawer_opened"), Is.False);
            Assert.That(State.HasItem("key"), Is.False); Assert.That(State.GetJournal(), Is.Empty);
        }
        [Test]
        public void ReadReceiptsAndUnreadDiscoveriesSurviveSnapshots()
        {
            Seed("note_direction"); var unread = State.CreateSnapshot();
            CompanionDiscovery.Acknowledge(Scenario, State, "Room01", new[] { "note_direction" }); var read = State.CreateSnapshot();
            State.ResetState(); State.RestoreSnapshot(unread);
            Assert.That(CompanionDiscovery.Pending(Scenario, State, "Room01"), Has.Count.EqualTo(1));
            State.RestoreSnapshot(read); Assert.That(CompanionDiscovery.Pending(Scenario, State, "Room01"), Is.Empty);
        }
        [TestCase("answer")]
        [TestCase("guidance")]
        [TestCase("forbidden")]
        [TestCase("nonEvidence")]
        public void DiscoveryNeverBypassesKnowledgePermissions(string blocked)
        {
            Seed("note_direction"); var fact = KnowledgeLibrary.GetRoom("Room01").FindFact("note_direction");
            var profile = KnowledgeLibrary.GetNpc("Alice");
            bool answer = fact.isPuzzleAnswer, guidance = fact.isPuzzleGuidance, evidence = fact.canShareAsEvidence;
            var forbidden = profile.forbiddenFactIds;
            try
            {
                fact.isPuzzleAnswer = blocked == "answer"; fact.isPuzzleGuidance = blocked == "guidance";
                fact.canShareAsEvidence = blocked != "nonEvidence";
                if (blocked == "forbidden") profile.forbiddenFactIds = forbidden.Concat(new[] { "note_direction" }).ToList();
                Assert.That(CompanionDiscovery.Pending(Scenario, State, "Room01"), Is.Empty);
                Assert.That(CompanionDiscovery.Acknowledge(Scenario, State, "Room01", new[] { "note_direction" }), Is.Zero);
            }
            finally { fact.isPuzzleAnswer = answer; fact.isPuzzleGuidance = guidance; fact.canShareAsEvidence = evidence; profile.forbiddenFactIds = forbidden; }
        }
        [Test]
        public void UnreportedFindingsComeBeforeOldFactsWithoutMutatingTheReasoningQuery()
        {
            Seed("note_direction"); Seed("companion_box_marks"); State.SetFlag("inspected_painting"); State.SetFlag("drawer_opened");
            foreach (string id in new[] { "painting_arrow", "desk_note", "drawer_key" })
                Assert.That(EvidenceSharing.TryShare(State, "Alice", id, out _, false), Is.True);
            CompanionDiscovery.Acknowledge(Scenario, State, "Room01", new[] { "note_direction" }); int score = State.GetRelationship("Alice");
            Assert.That(CompanionReasoning.TryReply(Scenario, State, "Room01", "เธอเจออะไรบ้าง", out var reply), Is.True);
            Assert.That(reply.referencedFactIds.First(), Is.EqualTo("companion_box_marks"));
            Assert.That(reply.referencedFactIds, Has.Length.EqualTo(CompanionDiscovery.ReportLimit));
            Assert.That(State.HasFlag(CompanionDiscovery.ReportedFlag(Scenario, "companion_box_marks")), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score)); Assert.That(reply.reply, Does.Not.Contain("4592"));
        }
        [Test]
        public void OtherRoomsCannotConsumeThisRoomsDiscovery()
        {
            Seed("note_direction"); State.SetCurrentScene("Room02");
            Assert.That(CompanionDiscovery.Pending(Scenario, State, "Room01"), Is.Empty);
            Assert.That(CompanionDiscovery.Acknowledge(Scenario, State, "Room01", new[] { "note_direction" }), Is.Zero);
        }
        [Test]
        public void ReportHeaderIsIncludedInAuthoredSpeechValidation()
        {
            var copy = Object.Instantiate(Scenario);
            try { copy.discoveryReportLine = "กด E"; Assert.That(copy.ValidateConfiguration(), Has.Some.Contains("game controls")); }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
