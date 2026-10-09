using System;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;

namespace MysteryGame.Tests
{
    /// <summary>
    /// Even the strongest hint only says where to look: no keys or buttons,
    /// no codes, no riddle answers. NPCs never talk about the game's controls.
    /// </summary>
    public class HintScaleTests : GameStateFixture
    {
        [Test]
        public void EveryTierOfEveryRoomStaysInCharacterAndKeepsTheAnswer()
        {
            GameDefinition game = UnityEngine.Resources.Load<GameDefinition>("GameDefinition");
            Assert.That(game, Is.Not.Null);
            foreach (RoomKnowledgeData room in game.rooms)
            {
                foreach (PuzzleStep step in room.steps)
                {
                    foreach (string hint in new[] { step.vagueHint, step.normalHint, step.explicitHint })
                    {
                        string where = room.roomId + "/" + step.stepId + ": " + hint;
                        Assert.That(string.IsNullOrWhiteSpace(hint), Is.False, where);
                        Assert.That(NpcReplyPolicy.MentionsGameControls(hint), Is.False, where);
                        Assert.That(Regex.IsMatch(hint, @"\p{Nd}"), Is.False, "no codes or numbers: " + where);
                        foreach (RoomFact fact in room.facts)
                            foreach (string secret in fact.protectedTerms)
                                if (!string.IsNullOrWhiteSpace(secret))
                                    Assert.That(hint.IndexOf(secret, StringComparison.OrdinalIgnoreCase), Is.LessThan(0),
                                        "gives away " + secret + ": " + where);
                    }
                }
            }
        }

        [TestCase("ไปกด E ที่โต๊ะอ่านหนังสือสิ")]
        [TestCase("กดE ที่ประตูได้เลย")]
        [TestCase("ลองกดปุ่ม F10 ดูนะ")]
        [TestCase("พิมพ์ตอบในช่องแชทได้เลย")]
        [TestCase("คลิกที่กล่องดนตรีสิ")]
        [TestCase("Press E at the desk.")]
        [TestCase("Just click the painting.")]
        public void TalkAboutControlsIsRejected(string text)
        {
            Assert.That(NpcReplyPolicy.MentionsGameControls(text), Is.True);
        }

        [TestCase("กดดันตัวเองเกินไปหรือเปล่า")]
        [TestCase("ฉันกดดันมากเลย")]
        [TestCase("ลองไปดูแถวโต๊ะอ่านหนังสือสิ")]
        [TestCase("กล่องดนตรีนั่นทำให้สเตลกลัว")]
        [TestCase("I will press on.")]
        [TestCase("Let's keep going.")]
        public void OrdinaryWordsAreNotMistakenForControls(string text)
        {
            Assert.That(NpcReplyPolicy.MentionsGameControls(text), Is.False);
        }

        [TestCase(false, "ถ้าเบื่อก็กด E ที่ประตูดูสิ")]
        [TestCase(true, "{hint} แล้วกด E ได้เลย")]
        [TestCase(true, "ไปกด E ตรงนั้นนะ {hint}")]
        public void AiReplyThatNamesAKeyFallsBackToWrittenLines(bool askedForHint, string text)
        {
            State.SetCurrentScene("Room01");
            State.SetRelationship("Alice", 90);
            GeneratedChatReply grounded;
            string reason;
            bool accepted = NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", askedForHint), new GeneratedChatReply
            {
                reply = text,
                referencedFactIds = Array.Empty<string>(),
                relationshipDelta = 0,
            }, out grounded, out reason);
            Assert.That(accepted, Is.False);
        }
    }
}
