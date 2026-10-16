using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;

public class CompanionCommandTargetTests
{
    [TestCase("ช่วยจับขอบโต๊ะกับตู้หนังสือหน่อย")]
    [TestCase("ช่วยจับขอบโต๊ะกับโคมไฟหน่อย")]
    [TestCase("ช่วยยกกล่องกับเก้าอี้หน่อย")]
    [TestCase("hold the desk and the bookshelf please")]
    [TestCase("hold the desk and lift the chair please")]
    [TestCase("ช่วยจับขอบโต๊ะและช่วยยกตู้หนังสือหน่อย")]
    [TestCase("ช่วยจับขอบโต๊ะกับประตูหน่อย")]
    [TestCase("ตามเรามาที่ประตูหน่อย")]
    [TestCase("follow me to the door please")]
    [TestCase("รอตรงนี้ที่ประตู")]
    [TestCase("wait here by the window please")]
    public void RecognizedActionCannotSilentlyDropAnUnsupportedTarget(string message)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(CompanionCommand.Clarify));
        Assert.That(station, Is.Null);
    }
    [TestCase("ช่วยจับอีกด้านของกล่องหน่อย", CompanionCommand.Help)]
    [TestCase("hold the box please", CompanionCommand.Help)]
    [TestCase("ขอบโต๊ะมันโยกนะ ช่วยได้ไหม", CompanionCommand.Help)]
    [TestCase("ช่วยเปิดกล่องข้างประตูหน่อย", CompanionCommand.Help)]
    [TestCase("ช่วยเปิดกล่องในห้องนี้หน่อย", CompanionCommand.Help)]
    [TestCase("ช่วยเปิดกล่องได้หรือเปล่า", CompanionCommand.Help)]
    [TestCase("Can you help me with the box in this room?", CompanionCommand.Help)]
    [TestCase("เดี๋ยวจะไปดูทางนั้น ตามมานะ", CompanionCommand.Follow)]
    [TestCase("ตามมาและตามเรามาหน่อย", CompanionCommand.Follow)]
    [TestCase("รอตรงนี้และรอก่อน", CompanionCommand.Wait)]
    [TestCase("ตามเรามาหน่อย", CompanionCommand.Follow)]
    [TestCase("Please follow me", CompanionCommand.Follow)]
    [TestCase("ช่วยเราเดินสำรวจด้วยกันหน่อย", CompanionCommand.Follow)]
    [TestCase("ไม่ต้องตามมาแล้ว", CompanionCommand.Wait)]
    [TestCase("รอตรงนี้ก่อน", CompanionCommand.Wait)]
    [TestCase("อย่าเพิ่งไป", CompanionCommand.Wait)]
    public void ExistingSupportedTargetsRemainUsable(string message, CompanionCommand expected)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(expected));
    }
}
