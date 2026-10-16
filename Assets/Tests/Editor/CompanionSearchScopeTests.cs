using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

public class CompanionSearchScopeTests
{
    [TestCase("ช่วยหาหลักฐานในลิ้นชักเท่านั้น", CompanionCommand.Clarify)]
    [TestCase("ช่วยหาหลักฐานตรงโต๊ะหน่อย", CompanionCommand.Clarify)]
    [TestCase("ช่วยหาหลักฐานในกล่องสองมือหน่อย", CompanionCommand.Clarify)]
    [TestCase("ช่วยหาหลักฐานแล้วช่วยยกเก้าอี้หน่อย", CompanionCommand.Clarify)]
    [TestCase("search only the drawer please", CompanionCommand.Clarify)]
    [TestCase("search the desk please", CompanionCommand.Clarify)]
    [TestCase("search the room and lift the chair", CompanionCommand.Clarify)]
    [TestCase("ช่วยหาหลักฐานหน่อย", CompanionCommand.Search)]
    [TestCase("ช่วยหาหลักฐานในห้องนี้หน่อย", CompanionCommand.Search)]
    [TestCase("ช่วยหาหลักฐานทั่วทั้งห้องหน่อย", CompanionCommand.Search)]
    [TestCase("ช่วยหาหลักฐานในห้องนี้เท่านั้น", CompanionCommand.Search)]
    [TestCase("ช่วยหาหลักฐานได้หรือเปล่า", CompanionCommand.Search)]
    [TestCase("search only this room please", CompanionCommand.Search)]
    [TestCase("search for evidence in this room please", CompanionCommand.Search)]
    [TestCase("อย่าช่วยหาหลักฐาน", CompanionCommand.None)]
    [TestCase("เธอหาหลักฐานแล้วใช่ไหม", CompanionCommand.None)]
    public void SearchUsesOnlySupportedRoomScope(string message, CompanionCommand expected)
    {
        CompanionStationData station;
        Assert.That(CompanionDialogueCommands.Parse(message, KnowledgeLibrary.GetRoom("Room01").companionScenario, out station), Is.EqualTo(expected));
        Assert.That(station, Is.Null, "A search request must never silently turn into a hold task.");
    }
    [Test]
    public void ConfigurableSearchScopeSpeechParticipatesInContentValidation()
    {
        var copy = Object.Instantiate(KnowledgeLibrary.GetRoom("Room01").companionScenario);
        try
        {
            Assert.That(copy.searchScopeLine, Is.Not.Empty);
            copy.searchScopeLine = "กด E";
            Assert.That(copy.ValidateConfiguration(), Has.Some.Contains("game controls"));
        }
        finally { Object.DestroyImmediate(copy); }
    }
}
