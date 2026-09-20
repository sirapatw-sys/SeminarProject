using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    /// <summary>
    /// The P0 guarantees from the handoff doc: an NPC may never state a fact
    /// the player has not uncovered, may never skip a step of the ladder, and
    /// a gatekeeper may never hint at all.
    /// </summary>
    public class NpcKnowledgeTests
    {
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            KnowledgeLibrary.ClearCache();
            host = new GameObject("TestGameState");
            host.AddComponent<GameState>();
            Assert.That(GameState.Instance, Is.Not.Null,
                        "GameState singleton should be live for these tests.");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            KnowledgeLibrary.ClearCache();
        }

        private static NpcKnowledgeContext Build(
            string npcId, string roomId, bool askedForHint = true)
        {
            GameState.Instance.SetCurrentScene(roomId);
            return NpcKnowledgeContextBuilder.Build(
                npcId, roomId, GameState.Instance, askedForHint);
        }

        // ------------------------------------------------------------ assets

        [Test]
        public void AuthoredRoomsAndNpcsLoadFromResources()
        {
            Assert.That(KnowledgeLibrary.GetRoom("Room01"), Is.Not.Null);
            Assert.That(KnowledgeLibrary.GetRoom("Room02"), Is.Not.Null);
            Assert.That(KnowledgeLibrary.GetNpc("Alice"), Is.Not.Null);
            Assert.That(KnowledgeLibrary.GetNpc("Sena"), Is.Not.Null);
        }

        [Test]
        public void EveryStepHintReferencesAnAuthoredWording()
        {
            foreach (string roomId in new[] { "Room01", "Room02" })
            {
                RoomKnowledgeData room = KnowledgeLibrary.GetRoom(roomId);
                foreach (PuzzleStep step in room.steps)
                {
                    Assert.That(step.HintFor(HintLevel.Vague), Is.Not.Empty,
                                roomId + "/" + step.stepId + " has no vague hint");
                    Assert.That(step.HintFor(HintLevel.Explicit), Is.Not.Empty,
                                roomId + "/" + step.stepId + " has no explicit hint");
                }
            }
        }

        // ------------------------------------------------------------ Room01

        [Test]
        public void BeforeInspectingThePaintingAliceCannotMentionTheNote()
        {
            NpcKnowledgeContext ctx = Build("Alice", "Room01");

            Assert.That(ctx.CanReference("painting_arrow"), Is.False);
            Assert.That(ctx.CanReference("desk_note"), Is.False);
            Assert.That(ctx.CanReference("drawer_code"), Is.False);
            Assert.That(ctx.CurrentStep.stepId, Is.EqualTo("inspect_painting"));
        }

        [Test]
        public void AfterInspectingThePaintingAliceMayMentionTheArrowOnly()
        {
            GameState.Instance.SetFlag("inspected_painting");
            NpcKnowledgeContext ctx = Build("Alice", "Room01");

            Assert.That(ctx.CanReference("painting_arrow"), Is.True);
            Assert.That(ctx.CanReference("drawer_code"), Is.False,
                        "the code is still two steps away");
            Assert.That(ctx.CurrentStep.stepId, Is.EqualTo("search_desk"));
        }

        [Test]
        public void TheDrawerCodeStaysSecretUntilTheNoteIsFound()
        {
            GameState.Instance.SetFlag("inspected_painting");
            Assert.That(Build("Alice", "Room01").CanReference("drawer_code"),
                        Is.False);

            GameState.Instance.SetFlag("found_note");
            NpcKnowledgeContext ctx = Build("Alice", "Room01");

            Assert.That(ctx.CanReference("drawer_code"), Is.True);
            Assert.That(ctx.CurrentStep.stepId, Is.EqualTo("open_drawer"));
            Assert.That(ctx.DeterministicHint, Is.Not.Empty);
        }

        [Test]
        public void Room01LadderWalksInOrder()
        {
            GameState state = GameState.Instance;
            Assert.That(Build("Alice", "Room01").CurrentStep.stepId,
                        Is.EqualTo("inspect_painting"));

            state.SetFlag("inspected_painting");
            Assert.That(Build("Alice", "Room01").CurrentStep.stepId,
                        Is.EqualTo("search_desk"));

            state.SetFlag("found_note");
            Assert.That(Build("Alice", "Room01").CurrentStep.stepId,
                        Is.EqualTo("open_drawer"));

            state.AddItem("key");
            Assert.That(Build("Alice", "Room01").CurrentStep.stepId,
                        Is.EqualTo("unlock_door"));

            state.SetFlag("door_unlocked");
            Assert.That(Build("Alice", "Room01").CurrentStep, Is.Null);
        }

        // ------------------------------------------------------------ Room02

        [Test]
        public void Room02LadderWalksInOrder()
        {
            GameState state = GameState.Instance;
            Assert.That(Build("Alice", "Room02").CurrentStep.stepId,
                        Is.EqualTo("meet_sena"));

            state.SetFlag("sena_wants_tome");
            Assert.That(Build("Alice", "Room02").CurrentStep.stepId,
                        Is.EqualTo("read_ledger"));

            state.SetFlag("read_ledger");
            Assert.That(Build("Alice", "Room02").CurrentStep.stepId,
                        Is.EqualTo("take_tome"));

            state.SetFlag("found_tome");
            state.AddItem("tome");
            Assert.That(Build("Alice", "Room02").CurrentStep.stepId,
                        Is.EqualTo("give_offering"));

            state.SetFlag("sena_offering_given");
            Assert.That(Build("Alice", "Room02").CurrentStep.stepId,
                        Is.EqualTo("answer_riddle"));

            state.SetFlag("sena_passed");
            Assert.That(Build("Alice", "Room02").CurrentStep.stepId,
                        Is.EqualTo("open_gate"));
        }

        [Test]
        public void AliceNeverLeaksTheRiddleAnswerBeforeSenaAsksIt()
        {
            GameState.Instance.SetFlag("sena_wants_tome");
            GameState.Instance.SetFlag("read_ledger");
            NpcKnowledgeContext ctx = Build("Alice", "Room02");

            Assert.That(ctx.CanReference("riddle_answer"), Is.False);
            Assert.That(ctx.DeterministicHint, Does.Not.Contain("พรุ่งนี้"));
            Assert.That(ctx.DeterministicHint, Does.Not.Contain("Tomorrow"));
        }

        [Test]
        public void TheShelfNumberIsUnknownUntilTheLedgerIsRead()
        {
            GameState.Instance.SetFlag("sena_wants_tome");
            Assert.That(Build("Alice", "Room02").CanReference("tome_shelf_number"),
                        Is.False);

            GameState.Instance.SetFlag("read_ledger");
            Assert.That(Build("Alice", "Room02").CanReference("tome_shelf_number"),
                        Is.True);
        }

        // ------------------------------------------------------------ Sena

        [Test]
        public void SenaNeverHintsEvenAtMaximumRelationship()
        {
            GameState.Instance.ChangeRelationship("Sena", 100);
            GameState.Instance.SetFlag("sena_offering_given");

            NpcKnowledgeContext ctx = Build("Sena", "Room02");

            Assert.That(ctx.AllowedHintLevel, Is.EqualTo(HintLevel.None));
            Assert.That(ctx.DeterministicHint, Is.Empty);
            Assert.That(NpcKnowledgeContextBuilder.BuildOfflineReply(ctx),
                        Is.EqualTo(KnowledgeLibrary.GetNpc("Sena").refuseHintLine));
        }

        [Test]
        public void SenaMayNotRevealWhereTheTomeIsShelved()
        {
            GameState.Instance.SetFlag("read_ledger");
            NpcKnowledgeContext ctx = Build("Sena", "Room02");

            Assert.That(ctx.CanReference("tome_shelf_number"), Is.False,
                        "the shelf number is on Sena's forbidden list");
            Assert.That(ctx.CanReference("sena_demand"), Is.True);
        }

        // ------------------------------------------------------------ guards

        [Test]
        public void NoHintIsOfferedWhenThePlayerDidNotAskForOne()
        {
            NpcKnowledgeContext ctx = Build("Alice", "Room01", askedForHint: false);

            Assert.That(ctx.AllowedHintLevel, Is.EqualTo(HintLevel.None));
            Assert.That(NpcKnowledgeContextBuilder.BuildOfflineReply(ctx),
                        Is.Empty);
        }

        [Test]
        public void RepliesCitingUnknownFactsAreRejected()
        {
            NpcKnowledgeContext ctx = Build("Alice", "Room01");
            string offending;

            Assert.That(ctx.ValidateReferences(
                new List<string> { "drawer_code" }, out offending), Is.False);
            Assert.That(offending, Is.EqualTo("drawer_code"));

            Assert.That(ctx.ValidateReferences(
                new List<string> { "door_locked" }, out offending), Is.True);
            Assert.That(ctx.ValidateReferences(null, out offending), Is.True);
        }

        [Test]
        public void InventedFactIdsAreRejected()
        {
            NpcKnowledgeContext ctx = Build("Alice", "Room01");
            string offending;

            Assert.That(ctx.ValidateReferences(
                new List<string> { "secret_trapdoor" }, out offending), Is.False);
            Assert.That(offending, Is.EqualTo("secret_trapdoor"));
        }

        [Test]
        public void UnauthoredRoomsFallBackToAnEmptyContext()
        {
            NpcKnowledgeContext ctx = Build("Alice", "Room03");

            Assert.That(ctx.HasData, Is.False);
            Assert.That(NpcKnowledgeContextBuilder.BuildOfflineReply(ctx),
                        Is.Empty);
        }
    }
}
