using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;

public class CompanionCommandTargetTests
{
    [TestCase("ยกเลิก, ตามมา", CompanionCommand.Follow)]
    [TestCase("ยกเลิก, อลิซ ตามมา", CompanionCommand.Follow)]
    [TestCase("ตามมา ยกเลิก, รอตรงนี้", CompanionCommand.Wait)]
    [TestCase("follow me scratch that, wait here", CompanionCommand.Wait)]
    [TestCase("ตามมา, ยกเลิก, ช่วยเปิดกล่อง", CompanionCommand.Help)]
    [TestCase("ยกเลิก, ช่วยยกเก้าอี้หน่อย", CompanionCommand.None)]
    [TestCase("ยกเลิก, เธอสบายดีไหม", CompanionCommand.None)]
    [TestCase("อย่า ยกเลิก", CompanionCommand.None)]
    [TestCase("ยกเลิก, ใช่ไหม", CompanionCommand.None)]
    [TestCase("เขาบอกว่า ยกเลิก, ตามมา", CompanionCommand.None)]
    public void WithdrawalAndRemainingPlanAreParsedWithoutExecutingCancellation(string message, CompanionCommand expected)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(expected));
    }

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
    [TestCase("ตามมาแล้วเปิดกล่อง")]
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
    // Playtest: a stop with an explanation or a guard wording kept following.
    [TestCase("หยุดเดินตามก่อนนะ ผมไปทางนั้นก่อน", CompanionCommand.Wait)]
    [TestCase("อยู่เฝ้าตรงนี้ก่อนนะ", CompanionCommand.Wait)]
    [TestCase("อยู่ตรงนี้ก่อนนะ เดี๋ยวกลับมา", CompanionCommand.Wait)]
    [TestCase("เฝ้าตรงนี้ไว้นะ", CompanionCommand.Wait)]
    [TestCase("รอแป๊บนึงนะ", CompanionCommand.Wait)]
    [TestCase("ไม่ต้องตามแล้ว ผมไปเอง", CompanionCommand.Wait)]
    [TestCase("รอตรงนี้นะ เดี๋ยวผมไปที่ประตูก่อน", CompanionCommand.Wait)]
    [TestCase("เดินเป็นเพื่อนกันหน่อยนะ", CompanionCommand.Follow)]
    [TestCase("มาเป็นเพื่อนหน่อย", CompanionCommand.Follow)]
    [TestCase("อยู่เป็นเพื่อนหน่อยนะ", CompanionCommand.None)]
    // Playtest round 2: a dropped tone mark and a reversed guard phrase.
    [TestCase("เดินเป็นเพือนหน่อยสิ", CompanionCommand.Follow)]
    [TestCase("ตามมาหน่อยนะ ที่นี่น่ากลัว", CompanionCommand.Follow)]
    [TestCase("เฝ้าอยู่ตรงนี้ก่อนนะ", CompanionCommand.Wait)]
    [TestCase("ช่วยเฝ้าตรงนี้หน่อย", CompanionCommand.Wait)]
    [TestCase("อยู่นี่ก่อนนะ", CompanionCommand.Wait)]
    [TestCase("ผมเฝ้าอยู่นะ", CompanionCommand.None)]
    [TestCase("ฉันรออยู่นะ", CompanionCommand.None)]
    public void ExistingSupportedTargetsRemainUsable(string message, CompanionCommand expected)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(expected));
    }
}
