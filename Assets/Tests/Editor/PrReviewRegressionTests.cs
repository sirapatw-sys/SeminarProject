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

        private static void AssertChatAndEvent(NpcKnowledgeContext context, string text, bool accepted)
        {
            GeneratedChatReply reply; GeneratedDialogueContent ev; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
            { reply = text, referencedFactIds = Array.Empty<string>() }, out reply, out reason), Is.EqualTo(accepted), reason);
            Assert.That(NpcReplyPolicy.TryGroundEvent(context, new GeneratedDialogueContent
            { lines = new[] { text }, choices = Array.Empty<GeneratedDialogueChoice>(), referencedFactIds = Array.Empty<string>() },
                out ev, out reason), Is.EqualTo(accepted), reason);
            if (accepted) { Assert.That(reply.reply, Is.EqualTo(text)); Assert.That(ev.lines[0], Is.EqualTo(text)); }
            else { Assert.That(reply, Is.Null); Assert.That(ev, Is.Null); }
        }

        [TestCase("มีเสียงเพลงจากกล่องดนตรีอีกแล้วค่ะ สเตลกลัวจัง")]
        [TestCase("เจอกระจกบานนั้นทีไรขนลุกทุกที")]
        [TestCase("มองกระจกแล้วเห็นแต่หน้าตัวเองซีดๆ")]
        [TestCase("มองกระจกบานนั้นแล้วเห็นแต่ใบหน้าตัวเอง")]
        [TestCase("There is a mirror here. It makes me nervous.")]
        [TestCase("There is a music box here and I feel uneasy.")]
        [TestCase("I look at the mirror and see only my own face.")]
        public void VisibleRoomAtmosphereIsAllowedInChatAndEvents(string text)
        { AssertChatAndEvent(Build("Stelle", "Room03", false), text, true); }

        [TestCase("Stelle", "Room03", "กล่องดนตรีอยู่บนโต๊ะเครื่องแป้ง")]
        [TestCase("Rina", "Room03", "ลองดูที่เตาผิงสิ")]
        [TestCase("Rina", "Room03", "There is a hidden key in the music box.")]
        [TestCase("Rina", "Room03", "The mirror is near the fireplace.")]
        [TestCase("Rina", "Room03", "There is a music box underneath the table.")]
        [TestCase("Rina", "Room03", "The drawer contains the music box.")]
        [TestCase("Rina", "Room03", "มีเสียงเพลงจากกล่องดนตรีบนโต๊ะเครื่องแป้ง")]
        [TestCase("Rina", "Room03", "มีเสียงเพลงจากกล่องดนตรีอีกแล้วค่ะ ใช้กล่องดนตรีสิ")]
        [TestCase("Rina", "Room03", "มองกระจกแล้วเห็นแต่หน้าตัวเองซีดๆ แล้วหมุนกล่องดนตรี")]
        [TestCase("Rina", "Room03", "I look at the mirror and see my own face and wind the music box.")]
        [TestCase("Rina", "Room03", "มองกระจกแล้วเห็นแต่หน้าตัวเอง กล่องดนตรีอยู่บนโต๊ะเครื่องแป้ง")]
        [TestCase("Rina", "Room03", "มีเสียงเพลงจากกล่องดนตรีอีกแล้วค่ะ ลองดูที่เตาผิงสิ")]
        [TestCase("Rina", "Room03", "เจอกระจกบานนั้นทีไรขนลุก รหัสคือ 4592")]
        [TestCase("Rina", "Room03", "ลองมองกระจกแล้วเห็นแต่หน้าตัวเอง")]
        [TestCase("Rina", "Room03", "Look at the mirror and see my face.")]
        [TestCase("Sena", "Room02", "ผู้ที่มิมีของถวาย จงเสาะหาจารึกที่ยังเขียนมิจบ")]
        [TestCase("Alice", "Room01", "มีนางฟ้ายืนขวางประตู")]
        public void AmbientMentionsDoNotAuthorizeLocationsInstructionsOrPuzzleClaims(string npc, string room, string text)
        { AssertChatAndEvent(Build(npc, room, false), text, false); }

        [Test]
        public void VisibleTermsAreOptInAndDoNotBypassWithheldFactsOrCoreSubjects()
        {
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            try
            {
                room.gameplayTerms.Add("statue");
                var context = new NpcKnowledgeContext { Room = room };
                AssertChatAndEvent(context, "There is a statue here.", false);
                room.visibleAmbientTerms.Add("statue");
                AssertChatAndEvent(context, "There is a statue here.", true);
                AssertChatAndEvent(context, "The statue is behind the door.", false);
                room.gameplayTerms.Add("key"); room.visibleAmbientTerms.Add("key");
                AssertChatAndEvent(context, "There is a key here.", false);
                context.WithheldFacts.Add(new RoomFact { factId = "hidden_statue", isPuzzleAnswer = true,
                    protectedTerms = new List<string> { "statue" } });
                AssertChatAndEvent(context, "There is a statue here.", false);
            }
            finally { UnityEngine.Object.DestroyImmediate(room); }
        }

        [TestCase(10)]
        [TestCase(70)]
        public void VisibleSceneryIsStillForbiddenInTheAiHintFrame(int relationship)
        {
            State.SetRelationship("Rina", relationship);
            var context = Build("Rina", "Room03", true);
            foreach (string text in new[] { "{hint} เจอกระจกแล้วฉันกลัว", "{hint} There is a music box here." })
            {
                GeneratedChatReply reply; string reason;
                Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                { reply = text, referencedFactIds = Array.Empty<string>() }, out reply, out reason), Is.False);
            }
        }

        [TestCase("Alice", "Room01", "ลองคิดดูนะ: {hint}")]
        [TestCase("Alice", "Room01", "ค่อนข้างยากหน่อยนะ {hint}")]
        [TestCase("Sena", "Room02", "จงใช้ปัญญาของเจ้าเถิด {hint}")]
        [TestCase("Sena", "Room02", "ลองใช้เหตุผลและไตร่ตรองดู {hint}")]
        [TestCase("Alice", "Room01", "ค่อน\u200bข้างยากหน่อย {hint}")]
        [TestCase("Sena", "Room02", "จงใช้ ปัญญาของเจ้าเถิด {hint}")]
        public void NeutralThinkingFramesKeepPersonalityAndTheLockedHintAtEveryTier(string npc, string room, string text)
        {
            foreach (int relationship in new[] { 10, 45, 70 })
            {
                State.SetRelationship(npc, relationship);
                var context = Build(npc, room, true);
                var authored = NpcReplyPolicy.HintReply(context); Assert.That(authored, Is.Not.Null);
                GeneratedChatReply reply; string reason;
                Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                { reply = text, referencedFactIds = Array.Empty<string>(), relationshipDelta = 10 }, out reply, out reason), Is.True, reason);
                Assert.That(reply.reply, Is.EqualTo(text.Replace("{hint}", authored.reply)));
                Assert.That(reply.hintId, Is.EqualTo(authored.hintId));
                Assert.That(reply.relationshipDelta, Is.Zero);
            }
        }

        [TestCase("{hint} ลองดูมัน")]
        [TestCase("{hint} จงใช้มัน")]
        [TestCase("{hint} ลองคิดดูนะ กุญแจอยู่ใต้โต๊ะ")]
        [TestCase("{hint} ค่อนข้างยาก ดูข้างหลังนะ")]
        [TestCase("{hint} จงใช้ปัญญาแล้วลองเปิดมัน")]
        [TestCase("{hint} ลองคิดดูนะ รหัสคือ 4592")]
        [TestCase("{hint} ค่อนข้างยาก อยู่ข้างโซฟานะ")]
        [TestCase("{hint} ลองคิดดูนะ\nไปตรวจที่นั่น")]
        public void NeutralFrameExemptionsCannotHideRealDirectionsOrPuzzleDetails(string text)
        {
            GeneratedChatReply reply; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", true), new GeneratedChatReply
            { reply = text, referencedFactIds = Array.Empty<string>() }, out reply, out reason), Is.False);
            Assert.That(reply, Is.Null);
        }

        [TestCase("unknown")]
        [TestCase("key")]
        [TestCase("")]
        [TestCase("duplicate")]
        public void ValidatorRejectsInvalidVisibleAmbientConfiguration(string term)
        {
            var game = ScriptableObject.CreateInstance<GameDefinition>();
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            try
            {
                room.roomId = game.firstScene; game.rooms.Add(room); room.gameplayTerms.Add("mirror");
                room.gameplayTerms.Add("key");
                if (term == "duplicate") { room.visibleAmbientTerms.Add("mirror"); room.visibleAmbientTerms.Add("mirror"); }
                else room.visibleAmbientTerms.Add(term);
                Assert.That(ContentValidator.Validate(game, Array.Empty<InteractionData>(), Array.Empty<InputPuzzleData>(),
                    Array.Empty<MiniEventData>()), Is.Not.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(game); UnityEngine.Object.DestroyImmediate(room); }
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

        [TestCase(false)]
        [TestCase(true)]
        public void EventPromptsKeepAuthoredChoiceTextAndLimitAiToOpeningLines(bool freeTopic)
        {
            var host = new GameObject("ChoicePromptTest"); host.SetActive(false);
            var generator = host.AddComponent<AiDialogueGenerator>();
            var ev = ScriptableObject.CreateInstance<MiniEventData>();
            var dialogue = ScriptableObject.CreateInstance<DialogueData>();
            try
            {
                ev.npcId = "Alice"; ev.dialogue = dialogue; ev.freeTopic = freeTopic;
                dialogue.speakerName = "Alice"; dialogue.lines.Add("คุยเป็นเพื่อนกันนะ");
                dialogue.choices.Add(new DialogueChoiceData
                { optionText = "ฉันฟังอยู่", responseText = "ขอบคุณที่รับฟัง" });
                string prompt = (string)typeof(AiDialogueGenerator)
                    .GetMethod("BuildPrompt", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(generator, new object[] { ev });
                Assert.That(prompt, Does.Contain("คัดลอก optionText และ responseText ต้นฉบับ"));
                Assert.That(prompt, Does.Contain("แต่งเฉพาะ lines"));
                Assert.That(prompt, Does.Contain("ฉันฟังอยู่"));
                Assert.That(prompt, Does.Contain("ขอบคุณที่รับฟัง"));
                Assert.That(prompt, Does.Not.Contain("ตัวอย่างด้านล่างเป็นแค่แนว"));
            }
            finally
            { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(ev); UnityEngine.Object.DestroyImmediate(dialogue); }
        }

        private static void InvokeSelector(GameDefinitionSelector selector, string method)
        {
            typeof(GameDefinitionSelector).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(selector, null);
        }

        private static GameDefinitionSelector CreateSelector(GameDefinition definition)
        {
            var host = new GameObject("ScopedDefinitionTest"); host.SetActive(false);
            var selector = host.AddComponent<GameDefinitionSelector>(); selector.definition = definition;
            InvokeSelector(selector, "Awake");
            return selector;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SceneSelectionRestoresThePreviousUnscopedDefinitionAndKnowledge(bool explicitOverride)
        {
            var original = Resources.Load<GameDefinition>("GameDefinition");
            var sample = ScriptableObject.CreateInstance<GameDefinition>();
            var selector = (GameDefinitionSelector)null;
            try
            {
                GameDefinition.Override = explicitOverride ? original : null;
                var alice = KnowledgeLibrary.GetNpc("Alice"); Assert.That(alice, Is.Not.Null);
                selector = CreateSelector(sample);
                Assert.That(GameDefinition.Current, Is.SameAs(sample));
                Assert.That(KnowledgeLibrary.GetNpc("Alice"), Is.Null);
                InvokeSelector(selector, "OnDestroy");
                Assert.That(GameDefinition.Override, Is.EqualTo(explicitOverride ? original : null));
                Assert.That(GameDefinition.Current, Is.SameAs(original));
                Assert.That(KnowledgeLibrary.GetNpc("Alice"), Is.SameAs(alice));
                Assert.That(GameSession.CreateDefault().CurrentSceneId, Is.EqualTo("Room01"));
            }
            finally
            {
                GameDefinition.Override = null;
                if (selector != null) UnityEngine.Object.DestroyImmediate(selector.gameObject);
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldSelectorCannotClearReplacementOrRestoreAnUnloadedSample(bool sameDefinition)
        {
            var first = ScriptableObject.CreateInstance<GameDefinition>();
            var second = sameDefinition ? first : ScriptableObject.CreateInstance<GameDefinition>();
            var oldSelector = (GameDefinitionSelector)null;
            var newSelector = (GameDefinitionSelector)null;
            try
            {
                oldSelector = CreateSelector(first); newSelector = CreateSelector(second);
                InvokeSelector(oldSelector, "OnDestroy");
                Assert.That(GameDefinition.Current, Is.SameAs(second));
                InvokeSelector(newSelector, "OnDestroy");
                Assert.That(GameDefinition.Override, Is.Null, "Do not restore the old scene's override.");
                InvokeSelector(oldSelector, "OnDestroy");
                Assert.That(GameDefinition.Override, Is.Null, "Repeated old cleanup must be harmless.");
            }
            finally
            {
                GameDefinition.Override = null;
                if (oldSelector != null) UnityEngine.Object.DestroyImmediate(oldSelector.gameObject);
                if (newSelector != null) UnityEngine.Object.DestroyImmediate(newSelector.gameObject);
                if (!sameDefinition) UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExplicitOverrideSupersedesSceneOwnershipEvenForTheSameAsset(bool sameDefinition)
        {
            var sample = ScriptableObject.CreateInstance<GameDefinition>();
            var explicitGame = sameDefinition ? sample : Resources.Load<GameDefinition>("GameDefinition");
            var selector = (GameDefinitionSelector)null;
            try
            {
                selector = CreateSelector(sample); GameDefinition.Override = explicitGame;
                InvokeSelector(selector, "OnDestroy");
                Assert.That(GameDefinition.Override, Is.SameAs(explicitGame));
            }
            finally
            {
                GameDefinition.Override = null;
                if (selector != null) UnityEngine.Object.DestroyImmediate(selector.gameObject);
                UnityEngine.Object.DestroyImmediate(sample);
            }
        }

        [Test]
        public void EmptySelectorDoesNotTakeOwnershipFromTheActiveScene()
        {
            var sample = ScriptableObject.CreateInstance<GameDefinition>();
            var active = (GameDefinitionSelector)null; var empty = (GameDefinitionSelector)null;
            try
            {
                active = CreateSelector(sample); empty = CreateSelector(null);
                InvokeSelector(empty, "OnDestroy");
                Assert.That(GameDefinition.Current, Is.SameAs(sample));
                InvokeSelector(active, "OnDestroy");
                Assert.That(GameDefinition.Override, Is.Null);
            }
            finally
            {
                GameDefinition.Override = null;
                if (active != null) UnityEngine.Object.DestroyImmediate(active.gameObject);
                if (empty != null) UnityEngine.Object.DestroyImmediate(empty.gameObject);
                UnityEngine.Object.DestroyImmediate(sample);
            }
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
