using System.Collections.Generic;
using System.Reflection;
using MysteryGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class NpcInitializationTests : GameStateFixture
    {
        private GameObject npcHost;
        private NpcNeedController controller;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private void Call(string method) { typeof(NpcNeedController).GetMethod(method, Private).Invoke(controller, null); }

        [SetUp]
        public void ConfigureNpc()
        {
            npcHost = new GameObject("InitializationTestNpc");
            controller = npcHost.AddComponent<NpcNeedController>();
            typeof(NpcNeedController).GetField("npcId", Private).SetValue(controller, "Alice");
            typeof(NpcNeedController).GetField("needs", Private).SetValue(controller,
                new List<NpcNeedRate> { new NpcNeedRate { needId = "thirst", initialValue = 60f } });
            typeof(NpcNeedController).GetField("emotions", Private).SetValue(controller,
                new List<NpcEmotionRate> { new NpcEmotionRate { emotionId = "homesickness", initialValue = 62f } });
            Call("Start"); // EditMode does not run Unity's Start lifecycle.
        }
        [TearDown]
        public void CleanupNpc()
        {
            Call("OnDisable");
            Object.DestroyImmediate(npcHost);
        }

        [Test]
        public void NewGameResetReappliesDefaultsSynchronously()
        {
            State.SetNpcNeed("Alice", "thirst", 12f); State.SetNpcEmotion("Alice", "homesickness", 8f);
            State.ResetState();
            Assert.That(State.GetNpcNeed("Alice", "thirst"), Is.EqualTo(60f));
            Assert.That(State.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(62f));
        }

        [TestCase(0f)]
        [TestCase(5f)]
        [TestCase(90f)]
        public void ExistingSavedMetricsAlwaysWinOverSceneDefaults(float value)
        {
            var save = new StateSnapshot { CurrentSceneId = "Room01" };
            save.NpcNeeds.Add(new NpcNeedSnapshot { NpcId = "Alice", NeedId = "thirst", Value = value });
            save.NpcEmotions.Add(new NpcEmotionSnapshot { NpcId = "Alice", EmotionId = "homesickness", Value = value });
            string error; Assert.That(State.TryRestoreSnapshot(save, out error), Is.True);
            Call("InitializeMissingValues"); Call("Start");
            Assert.That(State.GetNpcNeed("Alice", "thirst"), Is.EqualTo(value));
            Assert.That(State.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(value));
        }

        [Test]
        public void LegacyMissingMetricsInitializeOnlyTheMissingEntries()
        {
            var save = new StateSnapshot { CurrentSceneId = "Room01" };
            save.NpcNeeds.Add(new NpcNeedSnapshot { NpcId = "Alice", NeedId = "thirst", Value = 93f });
            string error; Assert.That(State.TryRestoreSnapshot(save, out error), Is.True);
            Call("InitializeMissingValues");
            Assert.That(State.GetNpcNeed("Alice", "thirst"), Is.EqualTo(93f));
            Assert.That(State.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(62f));
        }

        [Test]
        public void DisabledControllerUnsubscribesAndReenableInitializesMissingState()
        {
            controller.enabled = false; Call("OnDisable"); State.ResetState();
            Assert.That(State.HasNpcNeed("Alice", "thirst"), Is.False);
            controller.enabled = true; Call("OnEnable");
            Assert.That(State.GetNpcNeed("Alice", "thirst"), Is.EqualTo(60f));
            Assert.That(State.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(62f));
        }

        [Test]
        public void ResetSignalIsNeverPublishedForSaveRestore()
        {
            int resets = 0; State.StateReset += () => resets++;
            string error; Assert.That(State.TryRestoreSnapshot(State.CreateSnapshot(), out error), Is.True);
            Assert.That(resets, Is.Zero);
            State.ResetState(); Assert.That(resets, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedLifecycleBindingDoesNotReplaceExistingGameplayValues()
        {
            State.SetNpcNeed("Alice", "thirst", 21f); State.SetNpcEmotion("Alice", "homesickness", 77f);
            Call("Start"); Call("OnEnable"); Call("Start");
            Assert.That(State.GetNpcNeed("Alice", "thirst"), Is.EqualTo(21f));
            Assert.That(State.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(77f));
        }
    }
}
