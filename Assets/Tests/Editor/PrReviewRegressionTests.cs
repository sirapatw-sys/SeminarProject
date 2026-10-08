using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class PrReviewRegressionTests : GameStateFixture
    {
        private bool persistence;
        private GameDefinition game;
        [SetUp]
        public void Prepare()
        {
            persistence = SaveSystem.PersistenceEnabled; game = GameDefinition.Override;
            SaveSystem.PersistenceEnabled = false; GameDefinition.Override = null;
            KnowledgeLibrary.ClearCache(); State.SetCurrentScene("Room01");
            State.SetRelationship("Alice", 10);
        }
        [TearDown]
        public void Restore()
        {
            SaveSystem.PersistenceEnabled = persistence; GameDefinition.Override = game;
            KnowledgeLibrary.ClearCache();
        }

        [TestCase("Alice", "Room01", "มาช่วยกันหาทางออกต่อเถอะ")]
        [TestCase("Rina", "Room03", "สเตลน่ะ ถ้ามีคนอยู่ข้างๆ จะใจกล้าขึ้นเยอะ")]
        [TestCase("Sena", "Room02", "จารึกนั้นมิได้เดินมาหาเจ้าเองดอก")]
        [TestCase("Rina", "Room03", "ติดอยู่ที่นี่มา 3 วันแล้ว")]
        [TestCase("Alice", "Room01", "ฉันจะอยู่ข้างๆ เธอเอง")]
        [TestCase("Alice", "Room01", "ลองหายใจช้าๆ 3 ครั้งนะ")]
        [TestCase("Alice", "Room01", "ฉันนับถึง ๑๐ เพื่อให้ใจเย็นลง")]
        [TestCase("Alice", "Room01", "กุญแจนั่นทำให้เธอกังวลเหรอ")]
        [TestCase("Alice", "Room01", "ฉันไม่รู้ว่ากุญแจอยู่ที่ไหน")]
        [TestCase("Alice", "Room01", "เธอไม่ได้ซ่อนความรู้สึกจากฉันใช่ไหม")]
        [TestCase("Rina", "Room03", "กระจกนั่นทำให้ฉันรู้สึกกลัว")]
        [TestCase("Rina", "Room03", "เธอรู้สึกยังไงกับกระจกนั่น")]
        [TestCase("Sena", "Room02", "ข้าไม่ชอบจารึกนั่นเลย")]
        [TestCase("Alice", "Room01", "Let's keep looking for a way out together.")]
        [TestCase("Alice", "Room01", "I will stay beside you.")]
        [TestCase("Alice", "Room01", "Take 3 slow breaths.")]
        [TestCase("Alice", "Room01", "The key makes you feel nervous, doesn't it?")]
        [TestCase("Alice", "Room01", "I don't know where the key is.")]
        [TestCase("Rina", "Room03", "The mirror makes me feel uneasy.")]
        [TestCase("Alice", "Room01", "I'm not hiding my feelings from you.")]
        public void SocialMentionsAndOrdinaryNumbersAreNotGameplayClaims(string npc, string room, string text)
        {
            State.SetCurrentScene(room);
            var context = NpcKnowledgeContextBuilder.Build(npc, room, State, false);
            GeneratedChatReply reply; GeneratedDialogueContent ev; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = text, referencedFactIds = Array.Empty<string>() }, out reply, out reason), Is.True, reason);
            Assert.That(reply.reply, Is.EqualTo(text));
            Assert.That(NpcReplyPolicy.TryGroundEvent(context, new GeneratedDialogueContent
            { lines = new[] { text }, choices = Array.Empty<GeneratedDialogueChoice>(), referencedFactIds = Array.Empty<string>() },
                out ev, out reason), Is.True, reason);
        }

        [Test]
        public void OrdinaryEventPromptWorksWithoutRoomKnowledge()
        {
            State.SetCurrentScene("UnauthoredReviewRoom");
            var host = new GameObject("PromptReviewTest"); host.SetActive(false);
            var generator = host.AddComponent<AiDialogueGenerator>();
            var ev = ScriptableObject.CreateInstance<MiniEventData>();
            var dialogue = ScriptableObject.CreateInstance<DialogueData>();
            try
            {
                ev.npcId = "Alice"; ev.dialogue = dialogue; ev.situationPrompt = "คุยเป็นเพื่อน";
                dialogue.speakerName = "Alice"; dialogue.lines.Add("สวัสดี");
                string prompt = null;
                Assert.DoesNotThrow(() => prompt = (string)typeof(AiDialogueGenerator)
                    .GetMethod("BuildPrompt", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(generator, new object[] { ev }));
                Assert.That(prompt, Does.Contain("ยังไม่มีข้อมูล canon"));
                Assert.That(prompt, Does.Contain("ห้ามให้คำใบ้"));
            }
            finally
            { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(ev); UnityEngine.Object.DestroyImmediate(dialogue); }
        }

        [Test]
        public void MissingRoomContextCanBeFormattedSafely()
        {
            var context = new NpcKnowledgeContext();
            Assert.DoesNotThrow(() => context.ToPromptSection());
            Assert.That(context.ToPromptSection(), Does.Contain("ยังไม่มีข้อมูล canon"));
        }

        [TestCase(10)]
        [TestCase(45)]
        [TestCase(70)]
        public void AiCanStyleTheHintWithoutChangingItsAuthorizedContent(int relationship)
        {
            State.SetRelationship("Alice", relationship);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย");
            GeneratedChatReply reply; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = "ฉันจะบอกนายเท่านี้นะ: {hint} เราค่อยๆ คิดไปด้วยกัน", referencedFactIds = Array.Empty<string>(), relationshipDelta = 10 },
                out reply, out reason), Is.True, reason);
            Assert.That(reply.reply, Does.Contain(context.DeterministicHint));
            Assert.That(reply.reply, Does.Not.Contain("{hint}"));
            Assert.That(reply.reply, Does.StartWith("ฉันจะบอกนายเท่านี้นะ:"));
            Assert.That(reply.relationshipDelta, Is.Zero);
            Assert.That(reply.hintId, Is.EqualTo(NpcReplyPolicy.HintReply(context).hintId));
        }

        [TestCase("ฉันมีคำใบ้ให้")]
        [TestCase("{hint} {hint}")]
        [TestCase("{hint} กุญแจอยู่ใต้โต๊ะ")]
        [TestCase("{hint} Go inspect the painting.")]
        [TestCase("{hint} 4592")]
        [TestCase("{hint} สี่ ห้า เก้า สอง")]
        [TestCase("{hint} four five nine two")]
        [TestCase("{hint} {fact:door_locked}")]
        [TestCase("{hint} The painting is crooked.")]
        [TestCase("{hint} สี่")]
        [TestCase("{hint} f.o.u.r")]
        [TestCase("{hint} กุญ\u200bแจ")]
        [TestCase("{hint} ลองดูทางขวา")]
        [TestCase("{hint} Try moving it underneath.")]
        [TestCase("{hint} อยู่ข้างโซฟา")]
        public void ModelCannotAddHintInformationOrOmitTheLockedCore(string text)
        {
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย");
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = text, referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.False);
            Assert.That(grounded, Is.Null);
        }

        [Test]
        public void HintFrameCannotForgeReferencesOrCrashOnMissingReferences()
        {
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย");
            foreach (var refs in new[] { null, new[] { "door_locked" } })
            {
                GeneratedChatReply grounded; string reason;
                Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                { reply = "{hint}", referencedFactIds = refs }, out grounded, out reason), Is.False);
            }
        }

        [Test]
        public void LowTierHintPromptDoesNotSupplyHigherTierHintsOrConversationHistory()
        {
            State.SetFlag("inspected_painting"); State.SetFlag("found_note");
            State.AddConversationTurn("Alice", "Alice", "old explicit hint: 4592");
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย");
            string prompt = AiDialogueGenerator.BuildReplyPrompt("Alice", "Alice", "", "ช่วยใบ้หน่อย", null, context);
            Assert.That(prompt, Does.Contain(context.DeterministicHint));
            Assert.That(prompt, Does.Contain("{hint}"));
            Assert.That(prompt, Does.Contain(context.Npc.speechStyle));
            Assert.That(prompt, Does.Not.Contain("4592"));
            Assert.That(prompt, Does.Not.Contain("old explicit hint"));
        }

        [Test]
        public void ExplicitHintMayContainItsAuthorizedCodeButVagueCoreNeverDoes()
        {
            State.SetFlag("inspected_painting"); State.SetFlag("found_note");
            foreach (int relationship in new[] { 10, 70 })
            {
                State.SetRelationship("Alice", relationship);
                var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย");
                GeneratedChatReply reply; string reason;
                Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                { reply = "เอาล่ะ {hint}", referencedFactIds = Array.Empty<string>() }, out reply, out reason), Is.True, reason);
                Assert.That(reply.reply.Contains("4592"), Is.EqualTo(relationship == 70));
            }
        }

        [Test]
        public void CompletedRoomFallbackDoesNotImposeAliceSpeechOnEveryNpc()
        {
            var context = NpcKnowledgeContextBuilder.Build("Rina", "Room03", State, true);
            context.CurrentStep = null; context.DeterministicHint = "";
            context.CompletedSteps = context.TotalSteps;
            Assert.That(NpcReplyPolicy.HintReply(context).reply, Does.Not.Contain("ค่ะ"));
        }

        private static object CachedQuestion(IEnumerable<string> terms)
        {
            return typeof(PlayerIntentClassifier).GetMethod("GetGameplayQuestion", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { terms });
        }

        [Test]
        public void EquivalentRoomAliasesReuseRegexRegardlessOfOrderAndUnicode()
        {
            object first = CachedQuestion(new[] { "Statue", "portrait" });
            Assert.That(CachedQuestion(new[] { "portrait", "Ｓｔａｔｕｅ", "statue", "" }), Is.SameAs(first));
        }

        [Test]
        public void EditingRoomAliasesCannotReuseAnOutdatedRegex()
        {
            var terms = new List<string> { "statue" };
            object before = CachedQuestion(terms); terms[0] = "compass";
            Assert.That(CachedQuestion(terms), Is.Not.SameAs(before));
            Assert.That(PlayerIntentClassifier.IsAskingForHint("Where is the compass?", terms), Is.True);
            Assert.That(PlayerIntentClassifier.IsAskingForHint("Where is the statue?", terms), Is.False);
        }

        [Test]
        public void MalformedRoomAliasDoesNotCrashSocialReplyValidation()
        {
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            try
            {
                room.gameplayTerms.Add("\ud800");
                var context = new NpcKnowledgeContext { Room = room };
                GeneratedChatReply reply; string reason;
                Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                { reply = "ฉันอยู่เป็นเพื่อนนะ", referencedFactIds = Array.Empty<string>() }, out reply, out reason), Is.True, reason);
            }
            finally { UnityEngine.Object.DestroyImmediate(room); }
        }

        [Test]
        public void RegexCacheIsBoundedAndDoesNotChangeClassificationAfterEviction()
        {
            for (int i = 0; i < 80; i++) CachedQuestion(new[] { "review_object_" + i });
            var cache = (IDictionary)typeof(PlayerIntentClassifier).GetField("GameplayQuestionCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Assert.That(cache.Count, Is.LessThanOrEqualTo(32));
            Assert.That(PlayerIntentClassifier.IsAskingForHint("How do I inspect the statue?", new[] { "statue" }), Is.True);
        }
    }
}
