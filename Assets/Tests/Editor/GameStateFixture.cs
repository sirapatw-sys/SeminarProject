using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;

namespace MysteryGame.Tests
{
    /// <summary>
    /// A fresh GameState per test. Awake does not run in edit mode, so the
    /// instance is installed explicitly rather than assumed.
    /// </summary>
    public abstract class GameStateFixture
    {
        private GameObject host;

        protected GameState State
        {
            get { return GameState.Instance; }
        }

        [SetUp]
        public void CreateState()
        {
            KnowledgeLibrary.ClearCache();
            host = new GameObject("TestGameState");
            GameState.UseForTests(host.AddComponent<GameState>());
            Assert.That(GameState.Instance, Is.Not.Null,
                        "GameState singleton should be live for these tests.");
        }

        [TearDown]
        public void DestroyState()
        {
            GameState.UseForTests(null);
            Object.DestroyImmediate(host);
            KnowledgeLibrary.ClearCache();
        }

        protected NpcKnowledgeContext Build(
            string npcId, string roomId, bool askedForHint = true)
        {
            State.SetCurrentScene(roomId);
            return NpcKnowledgeContextBuilder.Build(npcId, roomId, State, askedForHint);
        }
    }
}
