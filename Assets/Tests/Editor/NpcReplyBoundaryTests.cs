using System;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class NpcReplyBoundaryTests : GameStateFixture
    {
        [TestCase("สี่\nห้า\nเก้า\nสอง")]
        [TestCase("four\nfive\nnine\ntwo")]
        [TestCase("four five\nnine two")]
        [TestCase("สี่\r\n\r\nห้า\r\nเก้า\r\nสอง")]
        [TestCase("สี่\nfive\nเก้า\ntwo")]
        [TestCase("FOUR\nFIVE\nNINE\nTWO")]
        [TestCase("ส\nี่\nห้\nา\nเก้\nา\nสอ\nง")]
        [TestCase("f\no\nu\nr\nf\ni\nv\ne\nn\ni\nn\ne\nt\nw\no")]
        [TestCase("f.o.u.r\nf-i-v-e\nn i n e\nt_w_o")]
        [TestCase("สี่\u200b\nห้า\nเก้า\nสอง")]
        public void ProtectedAnswerSpelledAcrossLinesOrWordSeparatorsIsRejected(string reply)
        {
            var context = Build("Alice", "Room01", false);
            Assert.That(context.CanReference("drawer_code"), Is.False);
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = reply, referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.False);
            Assert.That(grounded, Is.Null);
            Assert.That(reason, Is.EqualTo("drawer_code"));
        }

        [Test]
        public void ProtectedAnswerSplitAcrossEventLinesIsRejected()
        {
            var content = new GeneratedDialogueContent { lines = new[] { "four five", "nine two" },
                choices = Array.Empty<GeneratedDialogueChoice>(), referencedFactIds = Array.Empty<string>() };
            GeneratedDialogueContent grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundEvent(Build("Alice", "Room01", false), content,
                out grounded, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("drawer_code"));
        }

        [Test]
        public void ProtectedAnswerSplitAcrossEventLineAndChoiceIsRejected()
        {
            var content = new GeneratedDialogueContent { lines = new[] { "four five" },
                choices = new[] { new GeneratedDialogueChoice { optionText = "nine", responseText = "two" } },
                referencedFactIds = Array.Empty<string>() };
            GeneratedDialogueContent grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundEvent(Build("Alice", "Room01", false), content,
                out grounded, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("drawer_code"));
        }

        [TestCase("ฉันคิดถึงบ้าน\nขอบคุณที่คุยเป็นเพื่อนนะ")]
        [TestCase("I feel alone.\nSomeone staying close makes me feel better.")]
        public void HarmlessSocialDialogueAcrossLinesStillPasses(string reply)
        {
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", false), new GeneratedChatReply
            { reply = reply, referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.True, reason);
            Assert.That(grounded.reply, Is.EqualTo(reply));
        }

        [Test]
        public void KnownCanonFactStillPassesWithSocialLine()
        {
            var context = Build("Alice", "Room01", false);
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = "{fact:door_locked}\nฉันจะอยู่เป็นเพื่อน", referencedFactIds = new[] { "door_locked" } },
                out grounded, out reason), Is.True, reason);
            Assert.That(grounded.reply, Does.Contain(KnowledgeLibrary.GetRoom("Room01").FindFact("door_locked").statement));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PassiveNpcMetricsDoNotClaimPlayerMadeRoomProgress(bool emotion)
        {
            State.SetCurrentScene("Room01"); State.SetRelationship("Alice", 70);
            State.SetFlag("alice_evidence_arc.fulfilled"); State.RecordConversation("Alice");
            var npc = KnowledgeLibrary.GetNpc("Alice");
            string before = NpcOfflineReplies.ReturnGreeting(npc, State);
            if (emotion) State.ChangeNpcEmotion("Alice", "homesickness", 0.01f);
            else State.ChangeNpcNeed("Alice", "thirst", 0.01f);
            Assert.That(KnowledgeLibrary.GetRoom("Room01").CompletedStepCount(State), Is.EqualTo(0));
            Assert.That(State.HasWorldChangedSinceConversation("Alice"), Is.False);
            Assert.That(NpcOfflineReplies.ReturnGreeting(npc, State), Is.EqualTo(before));
        }

        [TestCase(false, 50f, 0f)]
        [TestCase(true, 50f, 0f)]
        [TestCase(false, 100f, 1f)]
        [TestCase(true, 100f, 1f)]
        [TestCase(false, 0f, -1f)]
        [TestCase(true, 0f, -1f)]
        public void NoOpAndClampedNpcMetricsDoNotInvalidateConversation(bool emotion, float initial, float delta)
        {
            if (emotion) State.SetNpcEmotion("Alice", "homesickness", initial);
            else State.SetNpcNeed("Alice", "thirst", initial);
            State.RecordConversation("Alice");
            if (emotion) State.ChangeNpcEmotion("Alice", "homesickness", delta);
            else State.ChangeNpcNeed("Alice", "thirst", delta);
            Assert.That(emotion ? State.GetNpcEmotion("Alice", "homesickness") : State.GetNpcNeed("Alice", "thirst"),
                Is.EqualTo(initial));
            Assert.That(State.HasWorldChangedSinceConversation("Alice"), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NeedAndEmotionThresholdEventsStillReactWithoutWorldRevision(bool emotion)
        {
            var ev = ScriptableObject.CreateInstance<MiniEventData>();
            var dialogue = ScriptableObject.CreateInstance<DialogueData>();
            try
            {
                dialogue.lines.Add("คุยเป็นเพื่อนหน่อยได้ไหม");
                ev.dialogue = dialogue; ev.npcId = "Alice"; ev.threshold = 70f;
                ev.metricId = emotion ? "homesickness" : "thirst";
                ev.triggerType = emotion ? MiniEventTriggerType.EmotionThreshold : MiniEventTriggerType.NeedThreshold;
                State.RecordConversation("Alice");
                Assert.That(ev.CanTrigger(State), Is.False);
                if (emotion) State.ChangeNpcEmotion("Alice", ev.metricId, 80f);
                else State.ChangeNpcNeed("Alice", ev.metricId, 80f);
                Assert.That(ev.CanTrigger(State), Is.True);
                Assert.That(State.HasWorldChangedSinceConversation("Alice"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(ev); UnityEngine.Object.DestroyImmediate(dialogue); }
        }

        [TestCase("flag")]
        [TestCase("add_item")]
        [TestCase("remove_item")]
        public void ActualRoomStateChangesStillInvalidateGreetingAfterPassiveTicks(string change)
        {
            State.SetCurrentScene("Room01"); State.SetRelationship("Alice", 70);
            if (change == "remove_item") State.AddItem("key");
            State.RecordConversation("Alice");
            State.ChangeNpcNeed("Alice", "thirst", 0.1f);
            State.ChangeNpcEmotion("Alice", "homesickness", 0.1f);
            Assert.That(State.HasWorldChangedSinceConversation("Alice"), Is.False);
            if (change == "flag") State.SetFlag("inspected_painting");
            else if (change == "add_item") State.AddItem("key");
            else State.RemoveItem("key");
            Assert.That(State.HasWorldChangedSinceConversation("Alice"), Is.True);
            Assert.That(NpcOfflineReplies.ReturnGreeting(KnowledgeLibrary.GetNpc("Alice"), State), Does.Contain("จัดการบางอย่าง"));
        }

        [Test]
        public void PassiveMetricValuesStillRoundTripThroughExistingSaveFormat()
        {
            State.SetNpcNeed("Alice", "thirst", 73f); State.SetNpcEmotion("Alice", "homesickness", 68f);
            string json = JsonUtility.ToJson(State.CreateSnapshot());
            State.ResetState(); State.RestoreSnapshot(JsonUtility.FromJson<StateSnapshot>(json));
            Assert.That(State.GetNpcNeed("Alice", "thirst"), Is.EqualTo(73f));
            Assert.That(State.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(68f));
        }
    }
}
