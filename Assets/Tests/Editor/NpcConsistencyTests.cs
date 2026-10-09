using System;
using System.Linq;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class NpcConsistencyTests : GameStateFixture
    {
        private static bool Accepts(NpcKnowledgeContext context, string text, params string[] ids)
        {
            GeneratedChatReply grounded;
            string reason;
            return NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = text, referencedFactIds = ids }, out grounded, out reason);
        }

        [TestCase("Alice", "Room01")]
        [TestCase("Rina", "Room03")]
        [TestCase("Stelle", "Room03")]
        [TestCase("Sena", "Room02")]
        public void EveryLockedSecretRejectsUncitedAuthoredTextAndProtectedAliases(string npc, string room)
        {
            State.SetRelationship(npc, 0);
            var context = Build(npc, room, false);
            Assert.That(context.LockedSecrets, Is.Not.Empty);
            foreach (var secret in context.LockedSecrets)
            {
                Assert.That(Accepts(context, secret.statement), Is.False, secret.factId);
                Assert.That(Accepts(context, "{fact:" + secret.factId + "}", secret.factId), Is.False);
                Assert.That(secret.protectedTerms, Is.Not.Empty);
                foreach (var term in secret.protectedTerms)
                    Assert.That(Accepts(context, "..." + term + "..."), Is.False, secret.factId + "/" + term);
            }
        }

        [TestCase("ฉันมีน้องชายรออยู่ที่บ้าน")]
        [TestCase("น้อ.ง ชายรอฉันอยู่")]
        [TestCase("น้อ\u200bงชายรอฉันอยู่")]
        [TestCase("น้อง\nชายรอฉันอยู่")]
        [TestCase("My younger brother is waiting at home.")]
        [TestCase("I do not have a little brother.")]
        public void LockedPersonalDetailsRejectParaphrasesSeparatorsAndNegatedClaims(string text)
        {
            Assert.That(Accepts(Build("Alice", "Room01", false), text), Is.False);
        }

        [Test]
        public void PersonalGuardAlsoWorksWithoutAuthoredRoomData()
        {
            var context = Build("Alice", "Room99", false);
            Assert.That(context.HasData, Is.False);
            Assert.That(Accepts(context, "ฉันมีน้องชาย"), Is.False);
            Assert.That(Accepts(context, "ฉันคิดถึงบ้าน คุยเป็นเพื่อนฉันหน่อยได้ไหม"), Is.True);
        }

        [Test]
        public void UnlockedSecretStillRequiresCanonButNormalFeelingsRemainFree()
        {
            State.SetRelationship("Alice", 70);
            var context = Build("Alice", "Room01", false);
            var secret = context.ShareablePersonalFacts.Single(f => f.factId == "alice_brother");
            Assert.That(Accepts(context, "ฉันมีน้องชาย"), Is.False);
            Assert.That(Accepts(context, secret.statement), Is.False);
            Assert.That(Accepts(context, secret.statement, secret.factId), Is.True);
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = "{fact:alice_brother}\nขอบคุณที่รับฟังฉันนะ", referencedFactIds = new[] { "alice_brother" } },
                out grounded, out reason), Is.True, reason);
            Assert.That(grounded.reply, Does.Contain(secret.statement));
            Assert.That(Accepts(context, "ฉันเศร้าและคิดถึงบ้าน ขอบคุณที่คุยเป็นเพื่อนนะ"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EventOptionsAndResponsesCannotLeakLockedSecrets(bool inOption)
        {
            var content = new GeneratedDialogueContent { lines = new[] { "ฉันอยากคุยด้วย" },
                choices = new[] { new GeneratedDialogueChoice {
                    optionText = inOption ? "น้องชายของฉัน" : "ฉันฟังอยู่",
                    responseText = inOption ? "ขอบคุณนะ" : "My younger brother is waiting at home." } },
                referencedFactIds = Array.Empty<string>() };
            GeneratedDialogueContent grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundEvent(Build("Alice", "Room01", false), content,
                out grounded, out reason), Is.False);
        }

        [Test]
        public void LockedSecretContentAndProtectionAliasesNeverEnterCharacterPrompt()
        {
            var context = Build("Alice", "Room01", false);
            var secret = context.LockedSecrets.Single(f => f.factId == "alice_brother");
            string prompt = context.ToCharacterSection();
            Assert.That(prompt, Does.Not.Contain(secret.factId));
            Assert.That(prompt, Does.Not.Contain(secret.statement));
            foreach (var term in secret.protectedTerms) Assert.That(prompt, Does.Not.Contain(term));
        }

        [Test]
        public void ValidatorReportsGatedPersonalFactsWithoutProtectionAndInvalidScenes()
        {
            var game = ScriptableObject.CreateInstance<GameDefinition>();
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            var npc = ScriptableObject.CreateInstance<NpcProfileData>();
            try
            {
                room.roomId = game.firstScene; game.rooms.Add(room);
                npc.npcId = "N"; game.npcs.Add(npc);
                npc.secrets.Add(new PersonalFact { factId = "secret", statement = "private story",
                    when = { new ConditionRule { type = ConditionType.RelationshipAtLeast, targetId = "N", amount = 70 } } });
                npc.returnGreetings.Add(new FallbackReplyRule { when = {
                    new ConditionRule { type = ConditionType.CurrentScene, targetId = "missing" } } });
                var errors = ContentValidator.Validate(game, new InteractionData[0], new InputPuzzleData[0], new MiniEventData[0]);
                Assert.That(errors.Any(e => e.Contains("gated personal fact has no protectedTerms")), Is.True);
                Assert.That(errors.Any(e => e.Contains("Unknown condition scene")), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(game); UnityEngine.Object.DestroyImmediate(room); UnityEngine.Object.DestroyImmediate(npc); }
        }

        [TestCase("alice_evidence_arc.fulfilled", 50)]
        [TestCase("alice_evidence_arc.fulfilled", 70)]
        [TestCase("alice_evidence_arc.neglected", 50)]
        [TestCase("alice_evidence_arc.neglected", 70)]
        public void WorldChangeTakesPriorityOverBothPromiseOutcomes(string flag, int relationship)
        {
            State.SetCurrentScene("Room01"); State.SetFlag(flag); State.SetRelationship("Alice", relationship);
            var npc = KnowledgeLibrary.GetNpc("Alice");
            State.RecordConversation("Alice");
            string before = NpcOfflineReplies.ReturnGreeting(npc, State);
            State.SetFlag("drawer_opened");
            Assert.That(State.HasWorldChangedSinceConversation("Alice"), Is.True);
            string after = NpcOfflineReplies.ReturnGreeting(npc, State);
            Assert.That(after, Is.Not.EqualTo(before));
            Assert.That(after, Does.Contain(relationship >= 70 ? "จัดการบางอย่าง" : "สถานการณ์เปลี่ยนไป"));
            Assert.That(State.HasFlag(flag), Is.True, "The memory remains permanent.");
        }

        [TestCase("alice_evidence_arc.fulfilled")]
        [TestCase("alice_evidence_arc.neglected")]
        public void HurtToneIsNotMaskedByPromiseMemoryEvenWhenWorldChanges(string flag)
        {
            State.SetCurrentScene("Room01"); State.SetFlag(flag); State.SetRelationship("Alice", 0);
            State.RecordConversation("Alice");
            var npc = KnowledgeLibrary.GetNpc("Alice");
            Assert.That(NpcOfflineReplies.ReturnGreeting(npc, State), Does.Contain("ยังไม่ลืม"));
            State.SetFlag("drawer_opened");
            string changed = NpcOfflineReplies.ReturnGreeting(npc, State);
            Assert.That(changed, Does.Contain("สถานการณ์เปลี่ยน"));
            Assert.That(changed, Does.Contain("ยังไม่ลืม"));
        }

        [TestCase("alice_evidence_arc.fulfilled", "Room02")]
        [TestCase("alice_evidence_arc.neglected", "Room02")]
        [TestCase("alice_evidence_arc.fulfilled", "Room03")]
        [TestCase("alice_evidence_arc.neglected", "Room03")]
        public void OldRoomPromiseDoesNotMonopolizeNewRoomGreeting(string flag, string scene)
        {
            State.SetCurrentScene(scene); State.SetFlag(flag); State.RecordConversation("Alice");
            var rule = NpcOfflineReplies.FirstMatch(KnowledgeLibrary.GetNpc("Alice").returnGreetings,
                string.Empty, PlayerIntent.None, State);
            Assert.That(rule.ruleId, Is.EqualTo("default"));
            Assert.That(State.HasFlag(flag), Is.True);
        }

        [TestCase("alice_evidence_arc.fulfilled")]
        [TestCase("alice_evidence_arc.neglected")]
        public void UnchangedPromiseGreetingRotatesWithoutConsumingOutcomeFlag(string flag)
        {
            State.SetCurrentScene("Room01"); State.SetFlag(flag); State.RecordConversation("Alice");
            var npc = KnowledgeLibrary.GetNpc("Alice");
            string first = NpcOfflineReplies.ReturnGreeting(npc, State);
            Assert.That(NpcOfflineReplies.ReturnGreeting(npc, State), Is.EqualTo(first), "Preview must not mutate state.");
            State.RecordConversation("Alice");
            Assert.That(NpcOfflineReplies.ReturnGreeting(npc, State), Is.Not.EqualTo(first));
            Assert.That(State.HasFlag(flag), Is.True);
        }

        [Test]
        public void UnchangedDefaultGreetingDoesNotInventWorldProgress()
        {
            State.RecordConversation("Alice");
            Assert.That(NpcOfflineReplies.ReturnGreeting(KnowledgeLibrary.GetNpc("Alice"), State), Does.Not.Contain("เปลี่ยน"));
        }

        [TestCase("Room01", true)]
        [TestCase("Room02", false)]
        public void SceneConditionIsDataDrivenAndExistingSerializedValuesAreStable(string scene, bool expected)
        {
            Assert.That((int)ConditionType.WorldChangedSinceLastTalk, Is.EqualTo(8));
            State.SetCurrentScene(scene);
            var rule = new ConditionRule { type = ConditionType.CurrentScene, targetId = "Room01" };
            Assert.That(rule.Evaluate(State), Is.EqualTo(expected));
            Assert.That(rule.Evaluate(null), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EvidenceNeverSuggestsReopeningCompletedDrawerAndRewardRemainsOnce(bool doorOpen)
        {
            State.SetCurrentScene("Room01"); State.SetFlag("inspected_painting"); State.SetFlag("found_note");
            State.SetFlag("drawer_opened"); State.AddItem("key"); State.SetRelationship("Alice", 70);
            if (doorOpen) State.SetFlag("door_unlocked");
            Assert.That(Build("Alice", "Room01").CurrentStep?.stepId, Is.EqualTo(doorOpen ? null : "unlock_door"));
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.Reply, Does.Not.Contain("เราลองเทียบตัวเลขในโน้ตกับแม่กุญแจรหัสของลิ้นชักกัน"));
            Assert.That(result.Reply, Does.Contain(doorOpen ? "ผ่านห้องนี้มาแล้ว" : "ลิ้นชักเปิดแล้ว"));
            int score = State.GetRelationship("Alice");
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.AlreadyShared, Is.True);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        }

        [Test]
        public void HighRelationshipEvidenceStillHelpsBeforeTheDrawerIsOpened()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note"); State.SetRelationship("Alice", 70);
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.Reply, Does.Contain("เราลองเทียบตัวเลขในโน้ต"));
            Assert.That(State.HasFlag("drawer_opened"), Is.False);
        }

        [Test]
        public void JsonRoundTripKeepsEvidenceKnowledgeAndDoesNotResetRewards()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("found_note");
            EvidenceShareResult result;
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            State.SetFlag("alice_evidence_arc.fulfilled");
            int score = State.GetRelationship("Alice");
            string json = JsonUtility.ToJson(State.CreateSnapshot());
            State.ResetState(); State.RestoreSnapshot(JsonUtility.FromJson<StateSnapshot>(json));
            Assert.That(State.HasFlag(EvidenceSharing.AnySharedFlag("Alice", "Room01")), Is.True);
            Assert.That(State.HasFlag("alice_evidence_arc.fulfilled"), Is.True);
            Assert.That(Build("Alice", "Room01", false).CanReference("desk_note"), Is.True);
            Assert.That(EvidenceSharing.TryShare(State, "Alice", "desk_note", out result), Is.True);
            Assert.That(result.AlreadyShared, Is.True);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        }
    }
}
