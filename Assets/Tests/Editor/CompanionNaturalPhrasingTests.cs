using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;

// Short phrasings players actually type, including the verb Alice herself
// uses in the box instructions ("ยกพร้อมกัน").
public class CompanionNaturalPhrasingTests
{
    [TestCase("มานี่", CompanionCommand.Follow, null)]
    [TestCase("มานี่หน่อย", CompanionCommand.Follow, null)]
    [TestCase("มาตรงนี้สิ", CompanionCommand.Follow, null)]
    [TestCase("ไปกัน", CompanionCommand.Follow, null)]
    [TestCase("ไปกันเลย", CompanionCommand.Follow, null)]
    [TestCase("ไปกันต่อเถอะ", CompanionCommand.Follow, null)]
    [TestCase("อยากให้เธอมาด้วย", CompanionCommand.Follow, null)]
    [TestCase("ช่วยหาหน่อย", CompanionCommand.Search, null)]
    [TestCase("ช่วยค้นหน่อย", CompanionCommand.Search, null)]
    [TestCase("ช่วยหาหน่อยได้ไหม", CompanionCommand.Search, null)]
    [TestCase("ช่วยตรวจห้องหน่อย", CompanionCommand.Search, null)]
    [TestCase("ช่วยตรวจดูรอบๆ หน่อย", CompanionCommand.Search, null)]
    [TestCase("ช่วยหาอะไรแปลกๆ หน่อย", CompanionCommand.Search, null)]
    [TestCase("ลองหาอะไรผิดปกติดูหน่อย", CompanionCommand.Search, null)]
    [TestCase("ช่วยยกกล่องหน่อย", CompanionCommand.Help, "two_hands_box")]
    [TestCase("ยกกล่องด้วยกันไหม", CompanionCommand.Help, "two_hands_box")]
    [TestCase("ช่วยจับอีกด้านของกล่องหน่อย", CompanionCommand.Help, "two_hands_box")]
    [TestCase("ช่วยยกอีกฝั่งของกล่องหน่อย", CompanionCommand.Help, "two_hands_box")]
    [TestCase("ขอบโต๊ะมันโยก ช่วยจับไว้ได้ไหม", CompanionCommand.Help, "desk_support")]
    [TestCase("ช่วยยกโต๊ะหน่อย", CompanionCommand.Help, "desk_support")]
    public void ShortNaturalRequestsStartTheMatchingActivity(string message, CompanionCommand expected, string stationId)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(expected), message);
        Assert.That(station != null ? station.id : null, Is.EqualTo(stationId), message);
    }

    [TestCase("ไปกันใหญ่แล้ว")]
    [TestCase("เขาไปกันหมดแล้ว")]
    [TestCase("เธอจะไปกันไหม")]
    [TestCase("ไม่ต้องไปกันหรอก")]
    [TestCase("ฉันมาด้วย")]
    [TestCase("ฉันจะไปด้วย")]
    [TestCase("เธอมาด้วยเหรอ")]
    [TestCase("มานี่ทำไม")]
    [TestCase("หาอะไรแปลกๆ เจอไหม")]
    [TestCase("เธอหาอะไรอยู่")]
    [TestCase("ยกเลิกเปิดกล่อง")]
    [TestCase("ฉันยกกล่องไม่ไหว")]
    [TestCase("กล่องนี้ยกได้ไหม")]
    [TestCase("ช่วยจับมือฉันหน่อย")]
    [TestCase("ช่วยเปิดใจคุยกับฉันหน่อย")]
    public void SimilarStatementsAndSocialRequestsAreNotCommands(string message)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(CompanionCommand.None), message);
    }
}
