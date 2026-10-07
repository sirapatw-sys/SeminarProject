using System;
using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class NpcDialogueBoundaryRegressionTests : GameStateFixture
    {
        private bool originalPersistence;
        private GameDefinition originalGame;
        [SetUp]
        public void PrepareBoundaries()
        {
            originalPersistence = SaveSystem.PersistenceEnabled;
            originalGame = GameDefinition.Override;
            SaveSystem.PersistenceEnabled = false;
            GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
            State.SetCurrentScene("Room01"); State.SetRelationship("Alice", 10);
        }
        [TearDown]
        public void RestoreSettings()
        {
            GameDefinition.Override = originalGame; KnowledgeLibrary.ClearCache();
            SaveSystem.PersistenceEnabled = originalPersistence;
        }

        [TestCase("อยู่ในห้องนี้เธอรู้สึกอย่างไร")]
        [TestCase("เธออยากเล่าว่ารู้สึกอย่างไรกับห้องนี้")]
        [TestCase("ประตูนี้ทำให้เธอรู้สึกอย่างไร")]
        [TestCase("What do you feel about this room?")]
        [TestCase("How are you coping in this room?")]
        [TestCase("How long have you been in this room?")]
        [TestCase("How to stay calm in this room?")]
        [TestCase("เธอเปิดประตูแล้วรู้สึกอย่างไร")]
        [TestCase("เธอรู้สึกยังไงตอนต้องเปิดประตู")]
        [TestCase("How do you feel about opening this door?")]
        [TestCase("Can you share how you feel about unlocking the door?")]
        [TestCase("What do you remember about this room?")]
        [TestCase("เธอรู้สึกแบบนี้ควรทำยังไง")]
        [TestCase("I am afraid. How do you cope in this room?")]
        [TestCase("Why does this room make you sad?")]
        [TestCase("เธอคิดถึงบ้านไหม")]
        [TestCase("How do you feel about using this key?")]
        [TestCase("ช่วยเราหน่อย เรารู้สึกเหงา")]
        [TestCase("ช่วยฉันสงบใจหน่อย")]
        [TestCase("Can you help me calm down in this room?")]
        [TestCase("Could you read my feelings in this room?")]
        [TestCase("Where is your home outside this room?")]
        [TestCase("บ้านเธออยู่ที่ไหนก่อนเข้าห้องนี้")]
        [TestCase("I don't need a hint, just tell me how you feel.")]
        [TestCase("สวัสดี ไม่ต้องใบ้นะ แค่อยากคุยเป็นเพื่อน")]
        [TestCase("ช่วยด้วย! ฉันกลัว")]
        [TestCase("ช่วยเราหน่อย แล้วคุยเป็นเพื่อนเราได้ไหม")]
        [TestCase("ช่วยหน่อย เราคิดถึงบ้าน")]
        [TestCase("Can you help me with my homesickness?")]
        [TestCase("Help me breathe, please.")]
        [TestCase("Help me understand my feelings about the door.")]
        [TestCase("Could you read my feelings about the painting?")]
        [TestCase("Where is your family in this room?")]
        [TestCase("No hints, please.")]
        [TestCase("Don't give me a hint.")]
        [TestCase("I don’t need a hint, just talk with me.")]
        [TestCase("ไม่อยากได้คำใบ้")]
        [TestCase("อย่าเพิ่งใบ้")]
        [TestCase("ขอคำใบ้หน่อย แต่ตอนนี้ไม่ต้องใบ้แล้ว")]
        [TestCase("Give me a hint, but actually no hints please.")]
        [TestCase("Can you help me calm down? No hints please, how do you feel?")]
        [TestCase("No hints, how do I open the door?")]
        [TestCase("ไม่ต้องใบ้ แค่บอกว่าประตูนี้เปิดยังไง")]
        [TestCase("Can you not give me a hint?")]
        [TestCase("I don't want you to give me a hint.")]
        [TestCase("Please don't give me any hints.")]
        [TestCase("ไม่ต้องมาช่วยใบ้")]
        [TestCase("ไม่อยากให้เธอบอกคำใบ้")]
        [TestCase("อย่าเพิ่งให้คำใบ้")]
        [TestCase("Don't give me the code, I only want to talk.")]
        [TestCase("ไม่ต้องบอกว่าประตูเปิดยังไง แค่อยากคุยเป็นเพื่อน")]
        public void PersonalQuestionsWithRoomContextAreNotPuzzleHelp(string message)
        {
            Assert.That(PlayerIntentClassifier.Classify(message) & PlayerIntent.AskingHint,
                Is.EqualTo(PlayerIntent.None), "Base classifier: " + message);
            Assert.That(PlayerIntentClassifier.IsAskingForHint(message), Is.False, message);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.PlayerAskedForHint, Is.False, message);
            Assert.That(NpcReplyPolicy.HintReply(context), Is.Null);
        }

        [TestCase("Room01", "อยู่ในห้องนี้เธอรู้สึกอย่างไร ช่วยใบ้หน่อย")]
        [TestCase("Room01", "ฉันกลัวแต่ประตูเปิดยังไง")]
        [TestCase("Room01", "How are you feeling, and how do I open the door?")]
        [TestCase("Room01", "ทำยังไงถึงจะเปิดประตูได้")]
        [TestCase("Room01", "How to open this door?")]
        [TestCase("Room01", "Where is the painting?")]
        [TestCase("Room01", "ภาพวาดนี้ตรวจอย่างไร")]
        [TestCase("Room02", "สมุดทะเบียนอ่านอย่างไร")]
        [TestCase("Room01", "ทำยังไงดี")]
        [TestCase("Room01", "what should I do next?")]
        [TestCase("Room01", "where should we go?")]
        [TestCase("Room01", "ช่วยฉันสงบใจหน่อย แล้วประตูนี้เปิดยังไง")]
        [TestCase("Room01", "Can you help me open the door?")]
        [TestCase("Room01", "ไม่ต้องปลอบนะ ช่วยใบ้หน่อย")]
        [TestCase("Room01", "ฉันรู้สึกกลัว ประตูนี้เปิดยังไง")]
        [TestCase("Room01", "ช่วยฉันสงบใจหน่อย ประตูเปิดยังไง")]
        [TestCase("Room01", "I am afraid. Where should we start exploring?")]
        [TestCase("Room01", "Can you help me calm down and tell me how to open the door?")]
        [TestCase("Room01", "ไม่ต้องใบ้ก่อนนะ แต่ตอนนี้ขอคำใบ้หน่อย")]
        [TestCase("Room01", "Don't give me hints yet, but now give me a hint.")]
        [TestCase("Room01", "ช่วยใบ้หน่อย แต่ไม่ต้องเฉลย")]
        [TestCase("Room01", "Can you give me a hint without spoiling the answer?")]
        [TestCase("Room01", "Hint please")]
        [TestCase("Room01", "ช่วยหน่อย")]
        [TestCase("Room01", "Help me")]
        [TestCase("Room01", "ทางออกอยู่ไหน")]
        [TestCase("Room01", "Tell me the door code.")]
        [TestCase("Room01", "Where can I find the key?")]
        [TestCase("Room01", "กุญแจนี้ต้องใช้ที่ไหน")]
        [TestCase("Room02", "สมุดทะเบียนอยู่ที่ไหน")]
        [TestCase("Room01", "Ｇｉｖｅ ｍｅ ａ ｈｉｎｔ")]
        [TestCase("Room01", "I'm not calm. Could you help me open this drawer?")]
        [TestCase("Room01", "I don't know how to open the door.")]
        [TestCase("Room01", "ฉันไม่รู้ว่าประตูเปิดยังไง")]
        [TestCase("Room01", "ไม่ต้องใบ้เยอะ ขอแค่คำใบ้หน่อย")]
        public void GenuineAndMixedHelpQuestionsStillGetOnlyTheirAllowedHint(string room, string message)
        {
            State.SetCurrentScene(room);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.PlayerAskedForHint, Is.True, message);
            Assert.That(context.AllowedHintLevel, Is.EqualTo(HintLevel.Vague));
            Assert.That(NpcReplyPolicy.HintReply(context).reply, Is.EqualTo(context.CurrentStep.vagueHint));
        }

        [TestCase("Room01", "Go inspect the painting on the wall.")]
        [TestCase("Room02", "Go read the ledger on the right table.")]
        [TestCase("Room01", "เริ่มจากรูปเอียงบนกำแพงก่อนนะ")]
        [TestCase("Room01", "ลองมองรูปที่เอียงบนกำแพง")]
        [TestCase("Room01", "Look at the painting first.")]
        [TestCase("Room01", "The picture is crooked.")]
        [TestCase("Room02", "The ledger is on the right table.")]
        [TestCase("Room03", "The mirror is near the fireplace.")]
        [TestCase("Room01", "Go inspect the strange statue.")]
        [TestCase("Room01", "You should examine the unfamiliar surface.")]
        [TestCase("Room01", "Go\ninspect the strange statue.")]
        [TestCase("Room01", "Go\u200b inspect the painting.")]
        [TestCase("Room01", "Ｇｏ ｉｎｓｐｅｃｔ ｔｈｅ ｐａｉｎｔｉｎｇ")]
        [TestCase("Room01", "เริ่มจากรูปบนผนังนะ")]
        [TestCase("Room02", "Start with the catalogue.")]
        [TestCase("Room03", "Wind the music box.")]
        public void UnattributedGameplayProseIsRejectedInBothChatAndEvents(string room, string text)
        {
            State.SetCurrentScene(room);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "สวัสดี");
            Assert.That(context.PlayerAskedForHint, Is.False);
            GeneratedChatReply chat; GeneratedDialogueContent ev; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                { reply = text, referencedFactIds = Array.Empty<string>() }, out chat, out reason), Is.False, text);
            Assert.That(chat, Is.Null);
            Assert.That(NpcReplyPolicy.TryGroundEvent(context, Event(text), out ev, out reason), Is.False, text);
            Assert.That(ev, Is.Null);
        }

        [TestCase("ฉันรู้สึกกังวลนิดหน่อย")]
        [TestCase("ฉันคิดถึงบ้าน ขอบคุณที่คุยเป็นเพื่อนนะ")]
        [TestCase("ลองมองในแง่ดีนะ")]
        [TestCase("เริ่มจากหายใจช้าๆ ก่อนก็ได้")]
        [TestCase("Take a deep breath.")]
        [TestCase("Let's start by taking a deep breath.")]
        [TestCase("Try to stay calm.")]
        [TestCase("You should try to stay calm.")]
        [TestCase("What a remarkable monkey.")]
        [TestCase("I feel comfortable.")]
        public void SocialAndCopingRepliesStillPassWithoutFactReferences(string text)
        {
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "สวัสดี");
            GeneratedChatReply chat; GeneratedDialogueContent ev; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                { reply = text, referencedFactIds = Array.Empty<string>() }, out chat, out reason), Is.True, reason);
            Assert.That(chat.reply, Is.EqualTo(text));
            Assert.That(NpcReplyPolicy.TryGroundEvent(context, Event(text), out ev, out reason), Is.True, reason);
            Assert.That(ev.lines[0], Is.EqualTo(text));
        }

        [Test]
        public void InstructionsCannotBeSplitAcrossAnEventLineAndChoice()
        {
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "สวัสดี");
            var input = new GeneratedDialogueContent { lines = new[] { "Go" },
                choices = new[] { new GeneratedDialogueChoice { optionText = "inspect", responseText = "the unfamiliar surface." } },
                referencedFactIds = Array.Empty<string>() };
            GeneratedDialogueContent grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundEvent(context, input, out grounded, out reason), Is.False);
        }

        [Test]
        public void NewRoomObjectAliasesControlBothQuestionRoutingAndReplyValidation()
        {
            var room = ScriptableObject.CreateInstance<RoomKnowledgeData>();
            try
            {
                room.roomId = "BoundaryAliasRoom";
                room.gameplayTerms = new List<string> { "statue", "รูปปั้น" };
                room.steps.Add(new PuzzleStep { stepId = "inspect_statue", vagueHint = "ลองสังเกตรอบตัวก่อน" });
                KnowledgeLibrary.Register(room); State.SetCurrentScene(room.roomId);
                var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "How do I inspect this statue?");
                Assert.That(context.PlayerAskedForHint, Is.True);
                Assert.That(NpcReplyPolicy.HintReply(context).reply, Is.EqualTo(room.steps[0].vagueHint));
                context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "How do you feel about this statue?");
                Assert.That(context.PlayerAskedForHint, Is.False);
                GeneratedChatReply grounded; string reason;
                Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply
                    { reply = "The statue is crooked.", referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(room); KnowledgeLibrary.ClearCache(); }
        }

        private static GeneratedDialogueContent Event(string text)
        {
            return new GeneratedDialogueContent { lines = new[] { text },
                choices = Array.Empty<GeneratedDialogueChoice>(), referencedFactIds = Array.Empty<string>() };
        }
    }
}
