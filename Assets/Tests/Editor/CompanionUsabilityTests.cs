using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;

public class CompanionUsabilityTests
{
    [TestCase("หยุดเดิน", CompanionCommand.Wait)]
    [TestCase("หยุดเดินก่อนนะ", CompanionCommand.Wait)]
    [TestCase("ช่วยหยุดเดินหน่อย", CompanionCommand.Wait)]
    [TestCase("หยุดเดินได้ไหม", CompanionCommand.Wait)]
    [TestCase("หยุด", CompanionCommand.Wait)]
    [TestCase("รอก่อน", CompanionCommand.Wait)]
    [TestCase("ยืนรอตรงนี้", CompanionCommand.Wait)]
    [TestCase("อย่าเดินตามเรา", CompanionCommand.None)]
    [TestCase("หยุดหาหลักฐานก่อน", CompanionCommand.Wait)]
    [TestCase("หยุดค้นหา", CompanionCommand.Wait)]
    [TestCase("ช่วยไปหาหลักฐานหน่อย", CompanionCommand.Search)]
    [TestCase("ช่วยลองไปหาเบาะแส", CompanionCommand.Search)]
    [TestCase("ช่วยจับโต๊ะหน่อย", CompanionCommand.Help)]
    [TestCase("ช่วยมาจับโต๊ะหน่อย", CompanionCommand.Help)]
    [TestCase("Hold the desk please", CompanionCommand.Help)]
    [TestCase("ไม่ต้องเดินแล้ว", CompanionCommand.Wait)]
    [TestCase("Please stop walking", CompanionCommand.Wait)]
    [TestCase("Can you stop moving?", CompanionCommand.Wait)]
    [TestCase("wait", CompanionCommand.Wait)]
    [TestCase("ช่วยหาหลักฐานหน่อย", CompanionCommand.Search)]
    [TestCase("ช่วยเราหาหลักฐานในห้องนี้", CompanionCommand.Search)]
    [TestCase("ช่วยค้นหาเบาะแส", CompanionCommand.Search)]
    [TestCase("หาหลักฐานให้หน่อย", CompanionCommand.Search)]
    [TestCase("Can you help me find clues?", CompanionCommand.Search)]
    [TestCase("ช่วยหาหลักฐาน แต่ไม่ต้องหาหลักฐานแล้ว", CompanionCommand.None)]
    [TestCase("เธอหยุดเดินแล้วใช่ไหม", CompanionCommand.None)]
    [TestCase("ช่วยอธิบายวิธีหาหลักฐาน", CompanionCommand.None)]
    [TestCase("ถ้าฉันบอกให้หยุดเดิน", CompanionCommand.None)]
    [TestCase("เขาพูดว่า ช่วยหาหลักฐาน", CompanionCommand.None)]
    [TestCase("ไม่ต้องหยุดเดิน", CompanionCommand.None)]
    public void RecognizesPlayerRequestsWithoutExecutingQuestionsOrReports(string message, CompanionCommand expected)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(expected));
    }
}
