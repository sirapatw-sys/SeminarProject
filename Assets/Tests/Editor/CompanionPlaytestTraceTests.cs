using System;
using System.IO;
using System.Reflection;
using System.Collections;
using MysteryGame.Companions;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class CompanionPlaytestTraceTests : GameStateFixture
    {
        string folder;
        [SetUp] public void TraceSetup()
        { CompanionPlaytestTrace.End(); folder = Path.Combine(Application.temporaryCachePath, "CompanionTraceTests", Guid.NewGuid().ToString("N")); }
        [TearDown] public void TraceCleanup()
        {
            CompanionPlaytestTrace.End();
            if (Directory.Exists(folder)) { foreach (string file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
        }
        [Test]
        public void TraceIsOptInBoundedAndCannotChangeWorldState()
        {
            int score = State.GetRelationship("Alice"); var before = State.CreateSnapshot();
            CompanionPlaytestTrace.Record("request", "Room01", "Alice");
            Assert.That(Directory.Exists(folder), Is.False);
            Assert.That(CompanionPlaytestTrace.Begin(folder), Is.True);
            CompanionPlaytestTrace.Record("reply", "Room01", "Alice", target: "private text and key=must-not-appear",
                request: "3:2", route: "companion", duration: 0.25f);
            CompanionPlaytestTrace.End();
            string text = File.ReadAllText(CompanionPlaytestTrace.LastPath);
            Assert.That(text, Does.Not.Contain("private text").And.Not.Contain("must-not-appear"));
            Assert.That(text, Does.Contain("3:2").And.Contain("companion"));
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
            Assert.That(State.CreateSnapshot().Flags, Is.EquivalentTo(before.Flags));
        }
        [Test]
        public void OfflineModeBlocksVoiceAndModelDiscoveryWithoutANetworkRequest()
        {
            var setting = typeof(CompanionPlaytestTrace).GetProperty("OfflineMode");
            var generator = UnityEngine.Object.FindObjectOfType<AiDialogueGenerator>();
            GameObject temporary = null;
            try
            {
                setting.SetValue(null, true);
                Assert.That(SpeechToText.IsAvailable(out string reason), Is.False);
                Assert.That(reason, Does.Contain("ออฟไลน์"));
                if (generator == null) { temporary = new GameObject("Offline trace test"); generator = temporary.AddComponent<AiDialogueGenerator>(); }
                Assert.That(generator.CanGenerate, Is.False);
                bool called = false;
                IEnumerator fetch = generator.FetchAvailableModels((models, error) => { called = true; Assert.That(models, Is.Null); Assert.That(error, Does.Contain("ออฟไลน์")); });
                Assert.That(fetch.MoveNext(), Is.False, "Offline lookup must return without yielding a web request.");
                Assert.That(called, Is.True);
            }
            finally { setting.SetValue(null, false); if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary); }
        }
        [Test]
        public void ReachingTheEntryLimitClosesWithoutRecursionOrThrowing()
        {
            Assert.That(CompanionPlaytestTrace.Begin(folder), Is.True);
            typeof(CompanionPlaytestTrace).GetField("count", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 20000);
            Assert.DoesNotThrow(() => CompanionPlaytestTrace.Record("request", "Room01"));
            Assert.That(CompanionPlaytestTrace.Enabled, Is.False);
        }
    }
}
