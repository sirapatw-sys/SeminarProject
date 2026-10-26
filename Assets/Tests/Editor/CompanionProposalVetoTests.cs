using MysteryGame.Companions;
using NUnit.Framework;

// PR #5 review: a model proposal must never override what the player's words
// rule out, and must not stand in for a different object.
public class CompanionProposalVetoTests
{
    [TestCase("อย่าหยุดเดิน", CompanionCommand.Wait)]
    [TestCase("ไม่ต้องหยุดเดิน", CompanionCommand.Wait)]
    [TestCase("follow me, scratch that", CompanionCommand.Follow)]
    [TestCase("ตามมา ไม่ต้องแล้ว", CompanionCommand.Follow)]
    [TestCase("ตามมา ยกเลิก", CompanionCommand.Follow)]
    [TestCase("ช่วยบอกวิธีจับกล่องหน่อย", CompanionCommand.Help)]
    [TestCase("ถ้าฉันบอกให้ตาม เธอจะตามไหม", CompanionCommand.Follow)]
    [TestCase("เขาบอกว่า \"รอตรงนี้\"", CompanionCommand.Wait)]
    [TestCase("เธอหยุดแล้วใช่ไหม", CompanionCommand.Wait)]
    [TestCase("อย่าช่วยเลย", CompanionCommand.Help)]
    public void RulesVetoProposalsTheMessageRulesOut(string message, CompanionCommand proposed)
    {
        Assert.That(CompanionDialogueCommands.RulesVeto(message, proposed), Is.True, message);
    }

    [TestCase("ไม่ต้องตามแล้ว", CompanionCommand.Wait)]
    [TestCase("รอแป๊บนึงนะ", CompanionCommand.Wait)]
    [TestCase("หยุดตรงนั้นก่อน", CompanionCommand.Wait)]
    [TestCase("เดินเป็นเพื่อนกันหน่อยนะ", CompanionCommand.Follow)]
    [TestCase("มาช่วยกันตรงนี้หน่อย", CompanionCommand.Help)]
    [TestCase("ไปประจำอีกฝั่งให้หน่อย", CompanionCommand.Help)]
    [TestCase("ลองดูรอบๆ ห้องให้ที", CompanionCommand.Search)]
    public void PlainRequestsAreNotVetoed(string message, CompanionCommand proposed)
    {
        Assert.That(CompanionDialogueCommands.RulesVeto(message, proposed), Is.False, message);
    }

    [TestCase("ช่วยยกเก้าอี้หน่อย", CompanionCommand.Help, true)]
    [TestCase("ช่วยจับมือฉันหน่อย", CompanionCommand.Help, true)]
    [TestCase("ช่วยหาแว่นให้หน่อย", CompanionCommand.Search, true)]
    [TestCase("มาช่วยกันตรงนี้หน่อย", CompanionCommand.Help, false)]
    [TestCase("ช่วยจับไว้ที่นี่หน่อยนะ", CompanionCommand.Help, false)]
    [TestCase("ไปประจำอีกฝั่งให้หน่อย", CompanionCommand.Help, false)]
    [TestCase("ช่วยหาอะไรแปลกๆ รอบห้องหน่อย", CompanionCommand.Search, false)]
    [TestCase("ลองดูรอบๆ ห้องให้ที", CompanionCommand.Search, false)]
    [TestCase("ช่วยยกเก้าอี้หน่อย", CompanionCommand.Follow, false)]
    public void TargetlessRequestsDifferFromOnesNamingAnotherObject(string message, CompanionCommand proposed, bool other)
    {
        Assert.That(CompanionDialogueCommands.NamesOtherObject(message, proposed), Is.EqualTo(other), message);
    }
}
