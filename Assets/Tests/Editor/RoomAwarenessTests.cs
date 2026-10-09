using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;

namespace MysteryGame.Tests
{
    /// <summary>
    /// NPCs know who is standing in their room, and riddle answers count
    /// however politely or hesitantly the player says them.
    /// </summary>
    public class RoomAwarenessTests : GameStateFixture
    {
        private GameDefinition originalGame;

        [SetUp]
        public void PrepareRooms()
        {
            originalGame = GameDefinition.Override;
            GameDefinition.Override = null;
            RoomPresence.Clear();
        }

        [TearDown]
        public void ForgetRooms()
        {
            RoomPresence.Clear();
            GameDefinition.Override = originalGame;
        }

        [Test]
        public void AliceSeesRinaAndStelleInRoom03()
        {
            foreach (string id in new[] { "Alice", "Rina", "Stelle" }) RoomPresence.Enter(id, "Room03");
            State.SetCurrentScene("Room03");
            string section = Build("Alice", "Room03", false).ToCharacterSection();
            Assert.That(section, Does.Contain("ใครอยู่ในห้องนี้"));
            Assert.That(section, Does.Contain(KnowledgeLibrary.GetNpc("Rina").displayName));
            Assert.That(section, Does.Contain(KnowledgeLibrary.GetNpc("Stelle").displayName));
            Assert.That(section, Does.Contain(KnowledgeLibrary.GetNpc("Rina").Appearance));
            Assert.That(section, Does.Not.Contain("- " + KnowledgeLibrary.GetNpc("Alice").displayName + ":"),
                "an NPC is not listed as someone else in its own room");
        }

        [Test]
        public void AliceIsAloneWithThePlayerInRoom01AndDoesNotBringUpLaterCharacters()
        {
            RoomPresence.Enter("Alice", "Room01");
            State.SetCurrentScene("Room01");
            string section = Build("Alice", "Room01", false).ToCharacterSection();
            Assert.That(section, Does.Contain("ไม่มีใครอื่นในห้องนี้"));
            Assert.That(section, Does.Not.Contain("Rina"));
            Assert.That(section, Does.Not.Contain("Stelle"));
        }

        [Test]
        public void TheGatekeeperIsNoLongerThereOnceSheLeaves()
        {
            RoomPresence.Enter("Alice", "Room02");
            RoomPresence.Enter("Sena", "Room02");
            State.SetCurrentScene("Room02");
            Assert.That(Build("Alice", "Room02", false).ToCharacterSection(),
                Does.Contain(KnowledgeLibrary.GetNpc("Sena").displayName));

            RoomPresence.Leave("Sena", "Room02");
            Assert.That(Build("Alice", "Room02", false).ToCharacterSection(), Does.Contain("ไม่มีใครอื่นในห้องนี้"));
        }

        [Test]
        public void WithoutALoadedSceneNothingIsClaimed()
        {
            Assert.That(Build("Alice", "Room03", false).ToCharacterSection(), Does.Not.Contain("ใครอยู่ในห้องนี้"));
        }

        [TestCase("วันถัดไป... เหรอครับ..?")]
        [TestCase("คำตอบคือพรุ่งนี้ครับ")]
        [TestCase("พรุ่งนี้ใช่ไหม")]
        [TestCase("น่าจะเป็นวันพรุ่งนี้นะ")]
        [TestCase("ข้าคิดว่าพรุ่งนี้")]
        [TestCase("It's tomorrow!")]
        [TestCase("tomorrow?")]
        public void RiddleAnswerCountsWhenSaidNaturally(string message)
        {
            var puzzle = UnityEditor.AssetDatabase.LoadAssetAtPath<InputPuzzleData>("Assets/Data/Puzzles/Sena_Riddle.asset");
            Assert.That(puzzle.Accepts(message), Is.True, message);
        }

        [TestCase("ไม่ใช่พรุ่งนี้")]
        [TestCase("ไม่ใช่พรุ่งนี้หรอ")]
        [TestCase("เมื่อวานหรือพรุ่งนี้")]
        [TestCase("yesterday or tomorrow")]
        [TestCase("tell me tomorrow")]
        [TestCase("...")]
        [TestCase("ครับ")]
        public void WrappingWordsNeverTurnAWrongAnswerIntoARightOne(string message)
        {
            var puzzle = UnityEditor.AssetDatabase.LoadAssetAtPath<InputPuzzleData>("Assets/Data/Puzzles/Sena_Riddle.asset");
            Assert.That(puzzle.Accepts(message), Is.False, message);
        }
    }
}
