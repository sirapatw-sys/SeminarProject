using System;
using System.Linq;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class CompanionPrReviewTests : GameStateFixture
    {
        private CompanionScenarioData Scenario { get { return KnowledgeLibrary.GetRoom("Room01").companionScenario; } }

        [TestCase("อลิซ ตามมา", CompanionCommand.Follow)]
        [TestCase("อลิซตามมาหน่อย", CompanionCommand.Follow)]
        [TestCase("Alice, ตามมา", CompanionCommand.Follow)]
        [TestCase("อลิซ ช่วยหาหลักฐานหน่อย", CompanionCommand.Search)]
        [TestCase("อลิซ ช่วยเปิดกล่องหน่อย", CompanionCommand.Help)]
        [TestCase("มากับฉันหน่อย", CompanionCommand.Follow)]
        [TestCase("ไปกันเถอะ", CompanionCommand.Follow)]
        [TestCase("มาทางนี้", CompanionCommand.Follow)]
        [TestCase("เดี๋ยวจะไปดูทางนั้น ตามมานะ", CompanionCommand.Follow)]
        [TestCase("ถ้าว่างก็ตามมาหน่อย", CompanionCommand.Follow)]
        [TestCase("ตามมาหน่อยนะ ถ้าไม่ลำบาก", CompanionCommand.Follow)]
        [TestCase("พอแล้ว ไม่ต้องตาม", CompanionCommand.Wait)]
        [TestCase("พักก่อน", CompanionCommand.Wait)]
        [TestCase("ช่วยค้นห้องหน่อย", CompanionCommand.Search)]
        [TestCase("ช่วยดูรอบๆ ห้องหน่อย", CompanionCommand.Search)]
        [TestCase("ลองหาดูหน่อยสิ", CompanionCommand.Search)]
        [TestCase("มาช่วยเปิดกล่องกัน", CompanionCommand.Help)]
        [TestCase("ช่วยประคองโต๊ะหน่อย", CompanionCommand.Help)]
        [TestCase("ขอบโต๊ะมันโยกนะ ช่วยได้ไหม", CompanionCommand.Help)]
        [TestCase("ถ้าฉันบอกว่า ตามมา", CompanionCommand.None)]
        [TestCase("ถ้าเราตามมาแล้วจะเกิดอะไรขึ้น", CompanionCommand.None)]
        [TestCase("อลิซ เธอตามมาแล้วใช่ไหม", CompanionCommand.None)]
        [TestCase("อลิซ ช่วยเปิดกล่อง แต่ไม่ต้องช่วยเปิดกล่องแล้ว", CompanionCommand.None)]
        [TestCase("กล่องดนตรีของเธอเปิดได้ไหม", CompanionCommand.None)]
        [TestCase("Can you open your music box?", CompanionCommand.None)]
        public void NaturalRequestsAndAddressAliasesKeepSpeechActGuards(string message, CompanionCommand expected)
        {
            CompanionStationData station;
            Assert.That(CompanionDialogueCommands.Parse(message, Scenario, out station), Is.EqualTo(expected));
            Assert.That(State.GetJournal(), Is.Empty, "Parsing is read-only.");
            Assert.That(State.HasFlag(Scenario.CompletedFlag(Scenario.stations[0])), Is.False);
        }

        [TestCase("เธอทำได้แล้วนี่ เก่งมาก")]
        [TestCase("ไม่เป็นไร เราช่วยกันแล้ว")]
        [TestCase("ฉันจะช่วย")]
        [TestCase("เราทำสำเร็จแล้ว ดีใจจัง")]
        [TestCase("ฉันยืนอยู่ข้างเธอแล้ว ไม่ต้องกลัว")]
        [TestCase("ฉันไม่ได้ปิดบังอะไรเธอหรอก")]
        [TestCase("ฉันกำลังจับตาดูอยู่")]
        [TestCase("จะเปิดเผยความลับให้ฟังก็ได้")]
        [TestCase("ฉันชอบกล่องดนตรีตอนเด็กๆ")]
        [TestCase("กลไกของเรื่องนี้มันซับซ้อนนะ")]
        public void IdleSocialSpeechPassesInBothChatAndEvents(string text)
        {
            var context = Build("Alice", "Room01", false);
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
                reply = text, referencedFactIds = Array.Empty<string>()
            }, out grounded, out reason), Is.True, reason);
            Assert.That(NpcReplyPolicy.ValidateEvent(context, new GeneratedDialogueContent {
                lines = new[] { text }, referencedFactIds = Array.Empty<string>()
            }, out reason), Is.True, reason);
        }

        [TestCase("ฉันเปิดกล่องสองมือแล้ว")]
        [TestCase("กล่องเปิดแล้ว")]
        [TestCase("ฉันจะเดินตามเธอ")]
        [TestCase("รหัสคือ 4592")]
        [TestCase("I opened the box for you.")]
        public void IdleSocialAllowanceDoesNotPermitConcreteActionsOrPuzzleSecrets(string text)
        {
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", false),
                new GeneratedChatReply { reply = text, referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.False);
        }

        [TestCase("กล่องดนตรีของเธอเปิดได้ไหม")]
        [TestCase("กลไกของโชคชะตามันแปลกเนอะ ว่าไหม ช่วยเล่าหน่อย")]
        [TestCase("Can you open your music box?")]
        public void SocialObjectNamesDoNotHijackTheProvider(string message)
        {
            State.SetCurrentScene("Room01");
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.RequestedCompanionFacts, Is.Empty);
            Assert.That(NpcReplyPolicy.CompanionReply(context), Is.Null);
        }

        [TestCase("กล่องนี้คืออะไร")]
        [TestCase("what's in the box?")]
        public void ActualPropQuestionsUseCharacterVoiceWithoutStartingWork(string message)
        {
            State.SetCurrentScene("Room01");
            var reply = NpcReplyPolicy.CompanionReply(AiDialogueGenerator.BuildKnowledgeContext("Alice", message));
            Assert.That(reply, Is.Not.Null);
            Assert.That(reply.reply, Does.Contain(Scenario.stations[0].pendingLine));
            Assert.That(reply.reply, Does.Not.Contain("กิจกรรม «"));
            Assert.That(NpcReplyPolicy.MentionsGameControls(reply.reply), Is.False);
            Assert.That(State.HasFlag(Scenario.CompletedFlag(Scenario.stations[0])), Is.False);
        }

        [Test]
        public void LockedStationsHaveNoPromptFactsOrReferenceRights()
        {
            var context = Build("Alice", "Room01", false);
            string prefix = Scenario.Flag("desk_support.");
            Assert.That(context.CompanionFacts.Any(f => f.factId.StartsWith(prefix)), Is.False);
            Assert.That(context.ToPromptSection(), Does.Not.Contain("ขอบโต๊ะ"));
            Assert.That(context.CanReference(prefix + "unavailable"), Is.False);
            State.SetFlag("inspected_painting");
            Assert.That(Build("Alice", "Room01", false).CanReference(prefix + "instructions"), Is.True);
            State.SetFlag(Scenario.CompletedFlag(Scenario.stations[1])); State.RemoveFlag("inspected_painting");
            Assert.That(Build("Alice", "Room01", false).CanReference(prefix + "completed"), Is.True);
        }

        [Test]
        public void RecollectionOfBoxCooperationRequiresItsActualCompletion()
        {
            const string text = "...นึกถึงตอนที่เราเปิดกล่องด้วยกันแล้วก็ดีเหมือนกันนะ";
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", false),
                new GeneratedChatReply { reply = text, referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.False);
            State.SetFlag(Scenario.CompletedFlag(Scenario.stations[0]));
            Assert.That(NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", false),
                new GeneratedChatReply { reply = text, referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.True, reason);
            Assert.That(NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", false),
                new GeneratedChatReply { reply = text + " รหัสคือ 4592", referencedFactIds = Array.Empty<string>() }, out grounded, out reason), Is.False);
        }

        [Test]
        public void EverySpokenCompanionFieldIsValidatedButUiInstructionsStaySeparate()
        {
            var copy = UnityEngine.Object.Instantiate(Scenario);
            try
            {
                Assert.That(copy.SpokenLines().Any(NpcReplyPolicy.MentionsGameControls), Is.False);
                foreach (string controls in new[] { "กด E", "พิมพ์ 'หยุดช่วยก่อน'", "press E", "click the mouse" })
                {
                    copy.stations[1].instructions = controls;
                    Assert.That(copy.ValidateConfiguration().Any(e => e.Contains("game controls")), Is.True);
                }
                copy.stations[1].instructions = Scenario.stations[1].instructions;
                copy.searchSpots[0].inspectingLine = "กด E";
                Assert.That(copy.ValidateConfiguration().Any(e => e.Contains("game controls")), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
    }
}
