using System;
using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class RelationshipHintSafetyTests : GameStateFixture
    {
        private bool originalPersistence;
        private GameDefinition originalGame;

        [SetUp]
        public void PrepareHints()
        {
            originalPersistence = SaveSystem.PersistenceEnabled;
            originalGame = GameDefinition.Override;
            SaveSystem.PersistenceEnabled = false;
            GameDefinition.Override = null;
            KnowledgeLibrary.ClearCache();
            State.SetCurrentScene("Room01");
            State.SetRelationship("Alice", 10);
        }

        [TearDown]
        public void RestoreSettings()
        {
            GameDefinition.Override = originalGame;
            SaveSystem.PersistenceEnabled = originalPersistence;
            KnowledgeLibrary.ClearCache();
        }

        [TestCase("ประตูนี้เปิดยังไง")]
        [TestCase("ประตูเปิดได้ด้วยอะไร")]
        [TestCase("เปิดลิ้นชักอย่างไร")]
        [TestCase("บอกวิธีผ่านห้องนี้")]
        [TestCase("ควรเริ่มสำรวจตรงไหน")]
        [TestCase("กุญแจอยู่ที่ไหน")]
        [TestCase("ควรใช้กุญแจนี้กับอะไร")]
        [TestCase("How do I open the door?")]
        [TestCase("What opens the door?")]
        [TestCase("Where is the key?")]
        [TestCase("Could you open this drawer?")]
        [TestCase("Where should we start exploring?")]
        public void IndirectGameplayQuestionsUseTheSameLowRelationshipHint(string message)
        {
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.PlayerAskedForHint, Is.True, message);
            Assert.That(context.AllowedHintLevel, Is.EqualTo(HintLevel.Vague));
            Assert.That(NpcReplyPolicy.HintReply(context).reply, Is.EqualTo(context.CurrentStep.vagueHint));
            Assert.That(context.CurrentStep.stepId, Is.EqualTo("inspect_painting"));
        }

        [TestCase("เธอคิดถึงบ้านไหม")]
        [TestCase("เธอเปิดใจยังไง")]
        [TestCase("เธอรู้สึกอย่างไร")]
        [TestCase("เธอชอบทำอะไร")]
        [TestCase("เราอยากช่วยเธอ")]
        [TestCase("เราอยากช่วยเธอหากุญแจ")]
        [TestCase("ประตูนี้ทำให้เธอกลัวไหม")]
        [TestCase("How are you feeling?")]
        [TestCase("Where is your home?")]
        [TestCase("What is your favourite monkey?")]
        public void SocialQuestionsAndUnaskedOffersDoNotBecomeHints(string message)
        {
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.PlayerAskedForHint, Is.False, message);
            Assert.That(NpcReplyPolicy.HintReply(context), Is.Null);
        }

        [TestCase(0, HintLevel.Vague)]
        [TestCase(44, HintLevel.Vague)]
        [TestCase(45, HintLevel.Normal)]
        [TestCase(69, HintLevel.Normal)]
        [TestCase(70, HintLevel.Explicit)]
        [TestCase(100, HintLevel.Explicit)]
        public void IndirectRequestsPreserveConfiguredRelationshipBoundaries(int score, HintLevel level)
        {
            State.SetRelationship("Alice", score);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ประตูนี้เปิดยังไง");
            Assert.That(context.AllowedHintLevel, Is.EqualTo(level));
            Assert.That(NpcReplyPolicy.HintReply(context).reply, Is.EqualTo(context.CurrentStep.HintFor(level)));
        }

        [Test]
        public void LowRelationshipDrawerHintNeverRevealsTheCodeOrEscalatesOnRepeatedRequests()
        {
            State.SetFlag("inspected_painting"); State.SetFlag("found_note");
            foreach (string message in new[] { "ช่วยใบ้หน่อย", "เปิดลิ้นชักอย่างไร", "เปิดลิ้นชักอย่างไร" })
            {
                var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
                var reply = NpcReplyPolicy.HintReply(context);
                Assert.That(context.CurrentStep.stepId, Is.EqualTo("open_drawer"));
                Assert.That(reply.reply, Is.EqualTo(context.CurrentStep.vagueHint));
                Assert.That(reply.reply, Does.Not.Contain("4592"));
                Assert.That(reply.hintId, Does.EndWith(".Vague"));
                State.AddConversationTurn("Alice", ConversationTurn.Player, message);
                State.AddConversationTurn("Alice", "Alice", reply.reply);
            }
        }

        [Test]
        public void NeutralDoorFactCannotSmuggleTheFutureKeyAndDrawerSolution()
        {
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "เล่าเรื่องประตูให้ฟัง");
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, FactReply("door_locked"), out grounded, out reason),
                Is.True, reason);
            Assert.That(grounded.reply, Is.EqualTo("ประตูทางออกถูกล็อคอยู่"));
            Assert.That(grounded.reply, Does.Not.Contain("กุญแจ"));
            Assert.That(grounded.reply, Does.Not.Contain("ลิ้นชัก"));
            Assert.That(context.ToPromptSection(), Does.Not.Contain("กุญแจทองเหลืองจากลิ้นชัก"));
        }

        [TestCase(10, false)]
        [TestCase(10, true)]
        [TestCase(100, false)]
        [TestCase(100, true)]
        public void SolutionGuidanceFactsCannotBypassAuthoredHintsInChatOrEvents(int score, bool asked)
        {
            State.SetRelationship("Alice", score);
            var context = Build("Alice", "Room02", asked);
            foreach (string id in new[] { "catalogue_rule", "gate_unlock" })
            {
                var fact = context.Room.FindFact(id);
                Assert.That(fact.isPuzzleGuidance, Is.True);
                Assert.That(context.CanReference(id), Is.False);
                Assert.That(context.ToPromptSection(), Does.Not.Contain(fact.statement));
                GeneratedChatReply chat; GeneratedDialogueContent ev; string reason;
                Assert.That(NpcReplyPolicy.TryGroundReply(context, FactReply(id), out chat, out reason), Is.False);
                Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                    { reply = fact.statement, referencedFactIds = new[] { id } }, out chat, out reason), Is.False);
                Assert.That(NpcReplyPolicy.TryGroundEvent(context, new GeneratedDialogueContent
                    { lines = new[] { "{fact:" + id + "}" }, choices = Array.Empty<GeneratedDialogueChoice>(),
                        referencedFactIds = new[] { id } }, out ev, out reason), Is.False);
            }
        }

        [Test]
        public void GuidanceStaysBlockedEvenIfAccidentallyAddedToKnownFacts()
        {
            var context = Build("Alice", "Room02", false);
            var fact = context.Room.FindFact("catalogue_rule");
            context.KnownFacts.Add(fact);
            GeneratedChatReply grounded; string reason;
            Assert.That(context.CanReference(fact.factId), Is.False);
            Assert.That(NpcReplyPolicy.TryGroundReply(context, FactReply(fact.factId), out grounded, out reason), Is.False);
        }

        [Test]
        public void HintOnlyGuidanceCannotBecomeShareableEvidenceByEnablingThePickerFlag()
        {
            var clone = UnityEngine.Object.Instantiate(KnowledgeLibrary.GetRoom("Room02"));
            try
            {
                clone.FindFact("catalogue_rule").canShareAsEvidence = true;
                KnowledgeLibrary.Register(clone); State.SetCurrentScene("Room02");
                EvidenceShareResult shared;
                Assert.That(EvidenceSharing.Available(State, "Alice").Exists(f => f.factId == "catalogue_rule"), Is.False);
                Assert.That(EvidenceSharing.TryShare(State, "Alice", "catalogue_rule", out shared), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); KnowledgeLibrary.ClearCache(); }
        }

        [Test]
        public void DiscoveredEvidenceCanStillBeSharedAndDiscussedAtLowRelationship()
        {
            State.SetFlag("inspected_painting");
            EvidenceShareResult shared;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "painting_arrow", out shared), Is.True);
            var context = Build("Alice", "Room01", false);
            GeneratedChatReply grounded; string reason;
            Assert.That(context.CanReference("painting_arrow"), Is.True);
            Assert.That(NpcReplyPolicy.TryGroundReply(context, FactReply("painting_arrow"), out grounded, out reason), Is.True, reason);
            Assert.That(grounded.reply, Is.EqualTo(shared.Fact.statement));
        }

        [Test]
        public void ContentValidationFlagsGuidanceIncorrectlyMarkedAsEvidence()
        {
            var game = UnityEngine.Object.Instantiate(GameDefinition.Current);
            var room = UnityEngine.Object.Instantiate(KnowledgeLibrary.GetRoom("Room02"));
            var npc = UnityEngine.Object.Instantiate(KnowledgeLibrary.GetNpc("Alice"));
            try
            {
                var fact = room.FindFact("catalogue_rule");
                fact.canShareAsEvidence = true; fact.evidenceTitle = "Invalid hint-only evidence";
                game.rooms[game.rooms.FindIndex(r => r.roomId == room.roomId)] = room;
                game.npcs[game.npcs.FindIndex(n => n.npcId == npc.npcId)] = npc;
                npc.evidenceReactions.Add(new EvidenceReaction { factId = fact.factId });
                var errors = ContentValidator.Validate(game, Array.Empty<InteractionData>(),
                    Array.Empty<InputPuzzleData>(), Array.Empty<MiniEventData>());
                Assert.That(errors, Does.Contain("Room02/catalogue_rule: puzzle guidance cannot be shareable evidence."));
                Assert.That(errors, Does.Contain("Alice: invalid evidence reaction catalogue_rule"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(npc); UnityEngine.Object.DestroyImmediate(room);
                UnityEngine.Object.DestroyImmediate(game);
            }
        }

        [Test]
        public void RoomAuthoredGameplaySubjectsRouteHintsWithoutHardcodingAnotherRoom()
        {
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            try
            {
                room.roomId = "HintSafetyRoom";
                room.gameplayTerms = new List<string> { "คันโยก" };
                room.steps.Add(new PuzzleStep { stepId = "inspect_lever", vagueHint = "ลองสังเกตสิ่งรอบตัวก่อน" });
                KnowledgeLibrary.Register(room); State.SetCurrentScene(room.roomId);
                var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "คันโยกนี้ใช้อย่างไร");
                Assert.That(context.PlayerAskedForHint, Is.True);
                Assert.That(NpcReplyPolicy.HintReply(context).reply, Is.EqualTo(room.steps[0].vagueHint));
                Assert.That(AiDialogueGenerator.BuildKnowledgeContext("Alice", "ฉันรู้สึกกลัวคันโยกนี้").PlayerAskedForHint, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(room); KnowledgeLibrary.ClearCache(); }
        }

        [Test]
        public void IndirectHintsStillRespectAnNpcWhoNeverGivesHints()
        {
            State.SetCurrentScene("Room02"); State.SetRelationship("Sena", 100);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Sena", "ประตูนี้เปิดยังไง");
            Assert.That(context.PlayerAskedForHint, Is.True);
            Assert.That(context.AllowedHintLevel, Is.EqualTo(HintLevel.None));
            Assert.That(NpcReplyPolicy.HintReply(context).reply, Is.EqualTo(context.Npc.refuseHintLine));
        }

        private static GeneratedChatReply FactReply(string id)
        {
            return new GeneratedChatReply { reply = "{fact:" + id + "}", referencedFactIds = new[] { id } };
        }
    }
}
