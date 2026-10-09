using System.Linq;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class EvidenceSharingTests : GameStateFixture
    {
        private static MiniEventData Event(string name)
        {
            var data = AssetDatabase.LoadAssetAtPath<MiniEventData>("Assets/Data/Events/" + name + "_Event.asset");
            Assert.That(data, Is.Not.Null);
            return data;
        }

        private static void Choose(DialogueData data, int index, GameState state)
        {
            foreach (var action in data.choices[index].actions) action.Execute(state);
        }

        [Test]
        public void PickerListsOnlyDiscoveredOptInNonAnswerEvidence()
        {
            State.SetCurrentScene("Room01");
            Assert.That(EvidenceSharing.Available(State, "Alice"), Is.Empty);
            State.SetFlag("inspected_painting"); State.SetFlag("found_note");
            var ids = EvidenceSharing.Available(State, "Alice").Select(f => f.factId).ToArray();
            Assert.That(ids, Is.EquivalentTo(new[] { "painting_arrow", "desk_note" }));
            Assert.That(ids, Does.Not.Contain("drawer_code"));
            Assert.That(ids, Does.Not.Contain("drawer_key"));
        }

        [TestCase("desk_note")]
        [TestCase("drawer_code")]
        [TestCase("invented")]
        [TestCase(null)]
        public void UndiscoveredAnswersAndFakeIdsNeverChangeState(string factId)
        {
            State.SetCurrentScene("Room01");
            int relationship = State.GetRelationship("Alice");
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", factId, out result), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship));
            Assert.That(State.GetJournal(), Is.Empty);
            Assert.That(State.GetNpcMemory("Alice"), Is.Empty);
            Assert.That(State.GetConversationLog("Alice"), Is.Empty);
        }

        [Test]
        public void DiscoveryAloneDoesNotTellNpcButSharingGrantsExactFact()
        {
            State.SetFlag("found_note");
            Assert.That(Build("Alice", "Room01", false).CanReference("desk_note"), Is.False);
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.Reply, Does.Contain(KnowledgeLibrary.GetRoom("Room01").FindFact("desk_note").statement));
            Assert.That(Build("Alice", "Room01", false).CanReference("desk_note"), Is.True);
            Assert.That(Build("Alice", "Room01", false).CanReference("drawer_code"), Is.False);
            Assert.That(result.Reply, Does.Not.Contain("4592"));
            Assert.That(State.HasFlag("drawer_opened"), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
        }

        [Test]
        public void OtherNpcsDoNotLearnEvidenceSharedWithAlice()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(Build("Rina", "Room01", false).CanReference("desk_note"), Is.False);
            Assert.That(EvidenceSharing.TryShare(State, "Rina", "desk_note", out result), Is.True);
            Assert.That(Build("Rina", "Room01", false).CanReference("desk_note"), Is.True);
            Assert.That(result.Reply, Does.Contain("ตอนนี้ฉันรู้ข้อมูลนี้แล้ว"));
        }

        [Test]
        public void RepeatedSharingHasNoSecondRewardMemoryOrJournalEntry()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            EvidenceShareResult first, repeated;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out first), Is.True);
            int score = State.GetRelationship("Alice");
            int memories = State.GetNpcMemory("Alice").Count;
            int journal = State.GetJournal().Count;
            Assert.That(first.RelationshipDelta, Is.EqualTo(3));
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out repeated), Is.True);
            Assert.That(repeated.AlreadyShared, Is.True);
            Assert.That(repeated.RelationshipDelta, Is.Zero);
            Assert.That(repeated.JournalEntryAdded, Is.False);
            Assert.That(repeated.Reply, Is.Not.EqualTo(first.Reply));
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
            Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(memories));
            Assert.That(State.GetJournal().Count, Is.EqualTo(journal));
        }

        [Test]
        public void RelationshipSelectsAuthoredInterpretationWithoutLeakingAnswer()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            State.ChangeRelationship("Alice", -100);
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.Reply, Does.Contain("ตั้งสติก่อน"));
            Assert.That(result.RelationshipDelta, Is.Zero);
            State.ResetState(); State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            State.ChangeRelationship("Alice", 20);
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.Reply, Does.Contain("แม่กุญแจรหัส"));
            Assert.That(result.Reply, Does.Not.Contain("4592"));
        }

        [Test]
        public void SharedEvidenceAndStoryOutcomesSurviveSnapshotWithoutRewardReset()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            EvidenceShareResult result;
            EvidenceSharing.TryShare(State, "Alice", "desk_note", out result);
            State.SetFlag("alice_evidence_arc.fulfilled");
            int relationship = State.GetRelationship("Alice");
            var snapshot = State.CreateSnapshot();
            State.ResetState(); State.RestoreSnapshot(snapshot);
            Assert.That(Build("Alice", "Room01", false).CanReference("desk_note"), Is.True);
            Assert.That(State.HasFlag("alice_evidence_arc.fulfilled"), Is.True);
            Assert.That(State.GetNpcMemory("Alice"), Is.Not.Empty);
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.AlreadyShared, Is.True);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship));
        }

        [Test]
        public void EvidenceCannotBeSubmittedFromAnotherRoomOrUnknownNpc()
        {
            State.SetFlag("found_note"); State.SetCurrentScene("Room02");
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.False);
            State.SetCurrentScene("Room01");
            Assert.That(EvidenceSharing.TryShare(State, "Unknown", "desk_note", out result), Is.False);
            Assert.That(EvidenceSharing.Available(State, "Unknown"), Is.Empty);
        }

        [Test]
        public void ForbiddenAndAccidentallyShareableAnswersAreStillRejected()
        {
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            var npc = ScriptableObject.CreateInstance<NpcProfileData>();
            try
            {
                room.roomId = "EvidenceTest"; npc.npcId = "EvidenceNpc";
                room.facts.Add(new RoomFact { factId = "blocked", statement = "private", canShareAsEvidence = true });
                room.facts.Add(new RoomFact { factId = "answer", statement = "1234", canShareAsEvidence = true, isPuzzleAnswer = true });
                npc.forbiddenFactIds.Add("blocked");
                KnowledgeLibrary.Register(room); KnowledgeLibrary.Register(npc);
                State.SetCurrentScene(room.roomId);
                Assert.That(EvidenceSharing.Available(State, npc.npcId), Is.Empty);
                EvidenceShareResult result;
                Assert.That(EvidenceSharing.TryShare(State, npc.npcId, "blocked", out result), Is.False);
                Assert.That(EvidenceSharing.TryShare(State, npc.npcId, "answer", out result), Is.False);
            }
            finally { Object.DestroyImmediate(room); Object.DestroyImmediate(npc); }
        }

        [Test]
        public void PromiseFulfilledAndMissedOutcomesAreMutuallyExclusive()
        {
            State.SetCurrentScene("Room01");
            var opening = Event("Alice_EvidencePromise");
            var kept = Event("Alice_PromiseKept");
            var missed = Event("Alice_PromiseMissed");
            Choose(opening.dialogue, 0, State);
            State.SetFlag("found_note"); State.SetFlag("drawer_opened");
            Assert.That(missed.CanTrigger(State), Is.True);
            Assert.That(kept.CanTrigger(State), Is.False);
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(missed.CanTrigger(State), Is.False);
            Assert.That(kept.CanTrigger(State), Is.True);
            Choose(kept.dialogue, 0, State);
            Assert.That(State.HasFlag("alice_evidence_arc.fulfilled"), Is.True);
            Assert.That(kept.CanTrigger(State), Is.False);
            Assert.That(missed.CanTrigger(State), Is.False);
        }

        [Test]
        public void AnyDiscoveredEvidenceHonoursThePromiseNotOnlyTheDeskNote()
        {
            State.SetCurrentScene("Room01");
            Choose(Event("Alice_EvidencePromise").dialogue, 0, State);
            State.SetFlag("inspected_painting");
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "painting_arrow", out result), Is.True);
            Assert.That(State.HasFlag("found_note"), Is.False);
            Assert.That(Event("Alice_PromiseKept").CanTrigger(State), Is.True);
            State.SetFlag("drawer_opened");
            Assert.That(Event("Alice_PromiseMissed").CanTrigger(State), Is.False);
        }

        [Test]
        public void HonestRefusalCreatesNoBrokenPromisePenalty()
        {
            State.SetCurrentScene("Room01");
            int relationship = State.GetRelationship("Alice");
            Choose(Event("Alice_EvidencePromise").dialogue, 1, State);
            State.SetFlag("found_note"); State.SetFlag("drawer_opened");
            Assert.That(State.HasFlag("alice_evidence_arc.declined"), Is.True);
            Assert.That(State.HasFlag("alice_evidence_arc.promised"), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship));
            Assert.That(Event("Alice_PromiseMissed").CanTrigger(State), Is.False);
            Assert.That(Event("Alice_PromiseKept").CanTrigger(State), Is.False);
        }

        [Test]
        public void MissedPromiseCanBeAcknowledgedAndEvidenceSharedLater()
        {
            State.SetCurrentScene("Room01");
            Choose(Event("Alice_EvidencePromise").dialogue, 0, State);
            State.SetFlag("found_note"); State.SetFlag("drawer_opened");
            Choose(Event("Alice_PromiseMissed").dialogue, 0, State);
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.Reply, Does.Contain("ช้ากว่าที่รับปาก"));
            Assert.That(Event("Alice_PromiseKept").CanTrigger(State), Is.False);
            Assert.That(Build("Alice", "Room01", false).CanReference("desk_note"), Is.True);
        }

        [Test]
        public void EncodedFlagPartsCannotCollideAcrossIds()
        {
            Assert.That(EvidenceSharing.SharedFlag("a.b", "c", "d"),
                Is.Not.EqualTo(EvidenceSharing.SharedFlag("a", "b.c", "d")));
        }

        [Test]
        public void Room01CanBeCompletedWithoutSharingOrAcceptingThePromise()
        {
            State.SetCurrentScene("Room01");
            var host = new GameObject("EvidenceIndependentInteractions");
            try
            {
                var system = host.AddComponent<InteractionSystem>();
                string response;
                foreach (string name in new[] { "Painting", "Desk", "Drawer", "Door" })
                {
                    var data = AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/Data/Interactions/" + name + "_Data.asset");
                    Assert.That(system.TryExecute(data, out response, name == "Drawer" ? "4592" : null), Is.True, name);
                }
                Assert.That(State.HasFlag("door_unlocked"), Is.True);
                Assert.That(State.HasFlag("alice_evidence_arc.promised"), Is.False);
                Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void ContentValidatorRejectsEvidenceAnswersAndChoiceEventsWithoutChoices()
        {
            var game = ScriptableObject.CreateInstance<GameDefinition>();
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            var npc = ScriptableObject.CreateInstance<NpcProfileData>();
            var data = ScriptableObject.CreateInstance<MiniEventData>();
            var dialogue = ScriptableObject.CreateInstance<DialogueData>();
            try
            {
                room.roomId = game.firstScene; game.rooms.Add(room);
                npc.npcId = "N"; game.npcs.Add(npc);
                room.facts.Add(new RoomFact { factId = "answer", isPuzzleAnswer = true, canShareAsEvidence = true });
                npc.evidenceReactions.Add(new EvidenceReaction { factId = "unknown" });
                data.eventId = "invalid"; data.npcId = "N"; data.dialogue = dialogue; data.completeOnChoice = true;
                dialogue.lines.Add("line");
                var errors = ContentValidator.Validate(game, new InteractionData[0], new InputPuzzleData[0], new[] { data });
                Assert.That(errors.Any(e => e.Contains("cannot be shareable")), Is.True);
                Assert.That(errors.Any(e => e.Contains("invalid evidence reaction")), Is.True);
                Assert.That(errors.Any(e => e.Contains("completeOnChoice requires choices")), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(game); Object.DestroyImmediate(room); Object.DestroyImmediate(npc);
                Object.DestroyImmediate(data); Object.DestroyImmediate(dialogue);
            }
        }
    }
}
