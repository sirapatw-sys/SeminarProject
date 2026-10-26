using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;

namespace MysteryGame.Tests
{
    public class CompanionCourtesyTests : GameStateFixture
    {
        [TestCase("ตามมาหน่อยนะ ถ้าไม่ลำบากค่ะ", CompanionCommand.Follow)]
        [TestCase("อลิซ ตามมาหน่อยนะ ถ้าไม่ลำบากครับ!", CompanionCommand.Follow)]
        [TestCase("ตามมาหน่อยนะ ถ้าสะดวกนะคะ…", CompanionCommand.Follow)]
        [TestCase("ถ้าว่างค่ะ ก็ตามมาหน่อย", CompanionCommand.Follow)]
        [TestCase("ถ้าว่างนะ ตามมาหน่อย", CompanionCommand.Follow)]
        [TestCase("ถ้าไม่ยุ่งครับก็ช่วยค้นห้องหน่อย!", CompanionCommand.Search)]
        [TestCase("ช่วยประคองโต๊ะหน่อย ถ้าไม่ลำบากนะคะ!", CompanionCommand.Help)]
        [TestCase("ถ้าว่างานนี้จบ ตามมา", CompanionCommand.None)]
        [TestCase("ถ้าประตูเปิดแล้ว ตามมา", CompanionCommand.None)]
        [TestCase("ตามมา ถ้าเธอเจอหลักฐานแล้ว", CompanionCommand.None)]
        [TestCase("ถ้าว่างก็ตามมาแล้วใช่ไหม", CompanionCommand.None)]
        [TestCase("เขาบอกว่า ตามมาหน่อย ถ้าสะดวกค่ะ", CompanionCommand.None)]
        [TestCase("\"ถ้าว่างค่ะ ก็ตามมา\"", CompanionCommand.None)]
        [TestCase("ตามมา! ไม่ต้องแล้ว…", CompanionCommand.None)]
        public void CourtesyNeverTurnsRealConditionsOrReportedSpeechIntoActions(string message, CompanionCommand expected)
        {
            CompanionStationData station;
            Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(expected));
            Assert.That(State.GetJournal(), Is.Empty);
        }
    }
}
