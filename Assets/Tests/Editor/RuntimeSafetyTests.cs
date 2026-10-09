using System;
using System.Reflection;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class RuntimeSafetyTests : GameStateFixture
    {
        [TestCase("relationship")]
        [TestCase("memory")]
        [TestCase("need")]
        [TestCase("emotion")]
        [TestCase("bond")]
        [TestCase("dialogue_flag")]
        [TestCase("evidence_flag")]
        public void SocialChangesNeverClaimRoomPuzzleProgress(string change)
        {
            State.SetCurrentScene("Room01"); State.SetRelationship("Alice", 70);
            State.RecordConversation("Alice");
            switch (change)
            {
                case "relationship": State.ChangeRelationship("Alice", 1); break;
                case "memory": State.AddNpcMemory("Alice", "เราได้คุยกัน"); break;
                case "need": State.ChangeNpcNeed("Alice", "thirst", 10f); break;
                case "emotion": State.ChangeNpcEmotion("Alice", "homesickness", 10f); break;
                case "bond": State.ChangeNpcRelationship("Alice", "Sena", -10); break;
                case "dialogue_flag": State.SetFlag("dialogue.another_npc.met"); break;
                case "evidence_flag": State.SetFlag("evidence.Alice.Room01.shared_any"); break;
            }
            Assert.That(KnowledgeLibrary.GetRoom("Room01").CompletedStepCount(State), Is.Zero);
            Assert.That(State.HasRoomProgressChangedSinceConversation("Alice"), Is.False);
            Assert.That(NpcOfflineReplies.ReturnGreeting(KnowledgeLibrary.GetNpc("Alice"), State),
                Does.Not.Contain("จัดการบางอย่างในห้องไปแล้ว"));
        }

        [TestCase("flag")]
        [TestCase("item")]
        [TestCase("scene")]
        public void RealProgressStillChangesTheRoomSignature(string change)
        {
            State.SetCurrentScene("Room01"); State.RecordConversation("Alice");
            if (change == "flag") State.SetFlag("inspected_painting");
            else if (change == "item") State.AddItem("key");
            else State.SetCurrentScene("Room02");
            var rule = new ConditionRule { type = ConditionType.RoomProgressSinceLastTalk, targetId = "Alice" };
            Assert.That(rule.Evaluate(State), Is.True);
            State.RecordConversation("Alice");
            Assert.That(rule.Evaluate(State), Is.False);
            Assert.That((int)ConditionType.WorldChangedSinceLastTalk, Is.EqualTo(8));
            Assert.That((int)ConditionType.CurrentScene, Is.EqualTo(9));
            Assert.That((int)ConditionType.RoomProgressSinceLastTalk, Is.EqualTo(10));
        }

        [TestCase("relationship_id")]
        [TestCase("missing_scene")]
        [TestCase("blank_scene")]
        [TestCase("null_entry")]
        [TestCase("memory_id")]
        [TestCase("memory_text")]
        [TestCase("need_id")]
        [TestCase("emotion_nan")]
        [TestCase("need_infinity")]
        [TestCase("relationship_range")]
        [TestCase("bond_id")]
        [TestCase("count_negative")]
        [TestCase("log_turn")]
        [TestCase("journal")]
        [TestCase("inventory")]
        public void InvalidSnapshotsNeverPartiallyReplaceLiveState(string invalid)
        {
            State.SetCurrentScene("Room01"); State.SetFlag("keep_flag"); State.AddItem("keep_item");
            State.SetRelationship("Alice", 75); State.RecordConversation("Alice");
            string before = JsonUtility.ToJson(State.CreateSnapshot());
            var snapshot = new StateSnapshot { CurrentSceneId = "Room02" };
            switch (invalid)
            {
                case "missing_scene": snapshot.CurrentSceneId = null; break;
                case "blank_scene": snapshot.CurrentSceneId = " "; break;
                case "relationship_id": snapshot.Relationships.Add(new RelationshipSnapshot { NpcId = null, Value = 55 }); break;
                case "null_entry": snapshot.Relationships.Add(null); break;
                case "memory_id": snapshot.NpcMemories.Add(new NpcMemorySnapshot()); break;
                case "memory_text": snapshot.NpcMemories.Add(new NpcMemorySnapshot { NpcId = "Alice", Memories = new System.Collections.Generic.List<string> { null } }); break;
                case "need_id": snapshot.NpcNeeds.Add(new NpcNeedSnapshot { NpcId = "Alice" }); break;
                case "emotion_nan": snapshot.NpcEmotions.Add(new NpcEmotionSnapshot { NpcId = "Alice", EmotionId = "fear", Value = float.NaN }); break;
                case "need_infinity": snapshot.NpcNeeds.Add(new NpcNeedSnapshot { NpcId = "Alice", NeedId = "thirst", Value = float.PositiveInfinity }); break;
                case "relationship_range": snapshot.Relationships.Add(new RelationshipSnapshot { NpcId = "Alice", Value = 999 }); break;
                case "bond_id": snapshot.NpcRelationships.Add(new NpcRelationshipSnapshot { FirstNpcId = "Alice", SecondNpcId = null }); break;
                case "count_negative": snapshot.ConversationCounts.Add(new RelationshipSnapshot { NpcId = "Alice", Value = -1 }); break;
                case "log_turn": snapshot.ConversationLogs.Add(new ConversationLogSnapshot { NpcId = "Alice", Turns = new System.Collections.Generic.List<ConversationTurn> { null } }); break;
                case "journal": snapshot.Journal.Add(new JournalEntry { Id = null, Text = "broken" }); break;
                case "inventory": snapshot.Inventory.Add(null); break;
            }
            string error;
            Assert.That(State.TryRestoreSnapshot(snapshot, out error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(JsonUtility.ToJson(State.CreateSnapshot()), Is.EqualTo(before));
            Assert.That(State.HasRoomProgressChangedSinceConversation("Alice"), Is.False);
            // The legacy void API also rejects bad data without throwing/mutating.
            Assert.DoesNotThrow(() => State.RestoreSnapshot(snapshot));
            Assert.That(JsonUtility.ToJson(State.CreateSnapshot()), Is.EqualTo(before));
        }

        [Test]
        public void JsonWithMissingNpcIdIsRejectedAtomically()
        {
            State.SetCurrentScene("Room01"); State.SetFlag("keep_flag");
            var snapshot = new StateSnapshot { CurrentSceneId = "Room02" };
            snapshot.Relationships.Add(new RelationshipSnapshot { Value = 55 });
            var parsed = JsonUtility.FromJson<StateSnapshot>(JsonUtility.ToJson(snapshot));
            string before = JsonUtility.ToJson(State.CreateSnapshot());
            string error;
            Assert.That(State.TryRestoreSnapshot(parsed, out error), Is.False);
            Assert.That(JsonUtility.ToJson(State.CreateSnapshot()), Is.EqualTo(before));
        }

        [Test]
        public void LegacyMissingCollectionsAndNullOptionalListsNormalize()
        {
            var old = JsonUtility.FromJson<StateSnapshot>("{\"CurrentSceneId\":\"Room01\"}");
            old.NpcNeeds = null; old.NpcEmotions = null; old.ConversationLogs = null; old.Journal = null;
            string error;
            Assert.That(State.TryRestoreSnapshot(old, out error), Is.True);
            Assert.That(State.GetCurrentScene(), Is.EqualTo("Room01"));
            Assert.That(State.GetJournal(), Is.Empty);
            Assert.That(State.GetConversationLog("Alice"), Is.Empty);
        }

        [Test]
        public void RestoredNestedRecordsAreDetachedFromTheInput()
        {
            State.AddJournalEntry("note", "Title", "Original");
            State.AddNpcMemory("Alice", "Memory");
            State.AddConversationTurn("Alice", "Alice", "Hello");
            var snapshot = State.CreateSnapshot();
            string error;
            Assert.That(State.TryRestoreSnapshot(snapshot, out error), Is.True);
            snapshot.Journal[0].Text = "Tampered";
            snapshot.NpcMemories[0].Memories[0] = "Tampered";
            snapshot.ConversationLogs[0].Turns[0].Text = "Tampered";
            Assert.That(State.GetJournal()[0].Text, Is.EqualTo("Original"));
            Assert.That(State.GetNpcMemory("Alice")[0], Is.EqualTo("Memory"));
            Assert.That(State.GetConversationLog("Alice")[0].Text, Is.EqualTo("Hello"));
        }

        [TestCase(AiProviderType.OpenAiResponses)]
        [TestCase(AiProviderType.Gemini)]
        [TestCase(AiProviderType.OpenAiCompatible)]
        public void SwitchingProvidersClearsOldSessionCredential(AiProviderType next)
        {
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            var previousGenerator = AiDialogueGenerator.Instance;
            var active = typeof(AiSettingsPanel).GetField("active", BindingFlags.Static | BindingFlags.NonPublic);
            var previousPanel = active.GetValue(null);
            string previousBackup = Environment.GetEnvironmentVariable("KKU_API_KEY_BACKUP");
            var generatorHost = new GameObject("DummyGenerator");
            var panelHost = new GameObject("DummyPanel");
            try
            {
                // If a regression reads the old generator, it sees dummy credentials only.
                Environment.SetEnvironmentVariable("KKU_API_KEY_BACKUP", "test-backup");
                var generator = generatorHost.AddComponent<AiDialogueGenerator>();
                typeof(AiDialogueGenerator).GetField("provider", fields).SetValue(generator, AiProviderType.KkuIntelsphere);
                typeof(AiDialogueGenerator).GetField("sessionApiKey", fields).SetValue(generator, "test-kku-key");
                typeof(AiDialogueGenerator).GetProperty("Instance").SetValue(null, generator);
                var panel = panelHost.AddComponent<AiSettingsPanel>();
                typeof(AiSettingsPanel).GetField("apiKey", fields).SetValue(panel, "unsaved-old-key");
                typeof(AiSettingsPanel).GetField("providerIndex", fields).SetValue(panel, (int)next);
                typeof(AiSettingsPanel).GetMethod("ApplyProviderDefaults", fields).Invoke(panel, null);
                Assert.That(typeof(AiSettingsPanel).GetField("apiKey", fields).GetValue(panel), Is.EqualTo(string.Empty));
                Assert.That(generator.Provider, Is.EqualTo(AiProviderType.KkuIntelsphere), "Selecting a preset does not apply it yet.");
            }
            finally
            {
                typeof(AiDialogueGenerator).GetProperty("Instance").SetValue(null, previousGenerator);
                UnityEngine.Object.DestroyImmediate(panelHost); UnityEngine.Object.DestroyImmediate(generatorHost);
                active.SetValue(null, previousPanel);
                Environment.SetEnvironmentVariable("KKU_API_KEY_BACKUP", previousBackup);
            }
        }
    }
}
