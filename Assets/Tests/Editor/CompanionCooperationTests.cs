using System.Collections.Generic;
using System.Linq;
using MysteryGame.Companions;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

public class CompanionCooperationTests
{
    [TestCase("ช่วยเปิดกล่องสองมือ แต่ไม่ต้องช่วยเปิดกล่องแล้ว", CompanionCommand.None)]
    [TestCase("Open the box, but don't open the box", CompanionCommand.None)]
    [TestCase("ตามเรามา แต่ห้ามเดินตามเรา", CompanionCommand.None)]
    [TestCase("ไม่ต้องช่วยเปิดกล่อง แต่ช่วยเปิดกล่องสองมือหน่อย", CompanionCommand.Help)]
    [TestCase("ช่วยจับขอบโต๊ะหน่อย", CompanionCommand.Help)]
    [TestCase("อย่าช่วยจับขอบโต๊ะ", CompanionCommand.None)]
    [TestCase("ช่วยจับขอบโต๊ะ แต่ไม่ต้องจับขอบโต๊ะแล้ว", CompanionCommand.None)]
    [TestCase("ช่วยเปิดกล่องสองมือ แต่ไม่ต้องจับขอบโต๊ะ", CompanionCommand.Help)]
    public void RequestsRespectTargetedWithdrawal(string text, CompanionCommand expected)
    {
        var scenario = KnowledgeLibrary.GetRoom("Room01").companionScenario;
        CompanionStationData target;
        Assert.That(CompanionDialogueCommands.Parse(text, scenario, out target), Is.EqualTo(expected));
    }

    [Test]
    public void RecallFindsRelevantOlderVerifiedNoteWithoutInventingIt()
    {
        var scenario = KnowledgeLibrary.GetRoom("Room01").companionScenario;
        string memory = scenario.stations[0].memory;
        var notes = new List<string> { memory, "recent 1", "recent 2", "recent 3", "recent 4", "recent 5", "ผู้เล่นพูดว่า: เปิดประตูแล้ว" };
        var result = NpcMemoryRecall.Select(notes, "จำกล่องสองมือได้ไหม", scenario);
        Assert.That(result.First(), Is.EqualTo(memory));
        Assert.That(result.Count, Is.EqualTo(4));
        Assert.That(result.Any(x => x.StartsWith("ผู้เล่นพูดว่า:")), Is.False);
        notes.Remove(memory);
        Assert.That(NpcMemoryRecall.Select(notes, "จำกล่องสองมือได้ไหม", scenario), Does.Not.Contain(memory));
    }
    [Test]
    public void FollowMayStopInsideBoundsWhilePlayerIsOutside()
    {
        var bounds = new Rect(-2, -2, 4, 4);
        var path = new List<Vector2>();
        Assert.That(CompanionNavigator.TryFollowPath(new Vector2(0, -1), new Vector2(0, 2.1f), 1,
            bounds, 0.2f, bounds.Contains, (a,b) => bounds.Contains(a) && bounds.Contains(b), path), Is.True);
        Assert.That(path, Is.Not.Empty);
        Assert.That(path.All(bounds.Contains), Is.True);
        Assert.That(Vector2.Distance(path.Last(), new Vector2(0, 2.1f)), Is.EqualTo(1).Within(0.002));
    }
}
