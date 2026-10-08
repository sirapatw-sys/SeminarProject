using MysteryGame.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MysteryGame.Tests
{
    /// <summary>
    /// The notebook (J): what the player read is kept, per room, and survives
    /// a save, so a clue that was skipped past can always be read again.
    /// </summary>
    public class JournalTests : GameStateFixture
    {
        [Test]
        public void TheJournalKeepsWhatWasReadPerRoom()
        {
            State.SetCurrentScene("Room01");
            Assert.That(State.AddJournalEntry("item.paper", "บันทึก", "รหัสคือ 4592"), Is.True);
            State.SetCurrentScene("Room02");
            Assert.That(State.AddJournalEntry("interaction.take_tome", "ชั้นล่างสุด", "วันถัดไป"), Is.True);

            Assert.That(State.GetJournal().Count, Is.EqualTo(2));
            Assert.That(State.GetJournal()[0].RoomId, Is.EqualTo("Room01"));
            Assert.That(State.GetJournal()[1].RoomId, Is.EqualTo("Room02"));
        }

        [Test]
        public void ReadingTheSameThingAgainUpdatesItInPlace()
        {
            State.SetCurrentScene("Room02");
            State.AddJournalEntry("interaction.inspect_shelf_a", "ชั้นบน", "เก่า");
            State.AddJournalEntry("interaction.take_tome", "ชั้นล่างสุด", "จารึก");

            Assert.That(State.AddJournalEntry("interaction.inspect_shelf_a", "ชั้นบน", "ใหม่"), Is.False);
            Assert.That(State.GetJournal().Count, Is.EqualTo(2));
            Assert.That(State.GetJournal()[0].Text, Is.EqualTo("ใหม่"));
        }

        [Test]
        public void EmptyTextIsNotRecorded()
        {
            Assert.That(State.AddJournalEntry("interaction.x", "x", "  "), Is.False);
            Assert.That(State.GetJournal(), Is.Empty);
        }

        [Test]
        public void TheJournalSurvivesASaveAndOlderSavesStillLoad()
        {
            State.SetCurrentScene("Room02");
            State.AddJournalEntry("item.tome", "จารึก", "วันถัดไป");
            string json = JsonUtility.ToJson(State.CreateSnapshot());

            GameObject other = new GameObject("RestoredState");
            try
            {
                GameState restored = other.AddComponent<GameState>();
                restored.RestoreSnapshot(JsonUtility.FromJson<StateSnapshot>(json));
                Assert.That(restored.GetJournal().Count, Is.EqualTo(1));
                Assert.That(restored.GetJournal()[0].Text, Is.EqualTo("วันถัดไป"));

                StateSnapshot old = JsonUtility.FromJson<StateSnapshot>("{\"CurrentSceneId\":\"Room01\"}");
                restored.RestoreSnapshot(old);
                Assert.That(restored.GetJournal(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void TheEmptyShelfPointsThePlayerToTheJournal()
        {
            InteractionData shelf = AssetDatabase.LoadAssetAtPath<InteractionData>(
                "Assets/Data/Interactions/BookshelfC_Data.asset");
            Assert.That(shelf, Is.Not.Null);
            Assert.That(shelf.popupItemDescription, Is.Not.Empty, "the tome's text is what gets kept");
            Assert.That(shelf.alreadyCompletedMessage, Does.Contain("J"));
        }
    }
}
