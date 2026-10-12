using System.Collections.Generic;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MysteryGame.Tests
{
    public class CompanionTests : GameStateFixture
    {
        [Test]
        public void Room01ScenarioIsValidAndOtherRoomsStayOptIn()
        {
            var scenario = KnowledgeLibrary.GetRoom("Room01").companionScenario;
            Assert.That(scenario, Is.Not.Null);
            Assert.That(scenario.ValidateConfiguration(), Is.Empty);
            Assert.That(KnowledgeLibrary.GetRoom("Room02").companionScenario, Is.Null);
            Assert.That(KnowledgeLibrary.GetRoom("Room03").companionScenario, Is.Null);
            Assert.That(scenario.stations[0].conditions, Is.Empty, "Cooperation is not locked behind relationship grinding.");
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0f)]
        [TestCase(-1f)]
        public void InvalidNavigationSettingsAreRejected(float value)
        {
            var scenario = Object.Instantiate(KnowledgeLibrary.GetRoom("Room01").companionScenario);
            try { scenario.cellSize = value; Assert.That(scenario.ValidateConfiguration(), Is.Not.Empty); }
            finally { Object.DestroyImmediate(scenario); }
        }

        [Test]
        public void StationsCannotOverlapOrUseDuplicatePersistentIds()
        {
            var scenario = Object.Instantiate(KnowledgeLibrary.GetRoom("Room01").companionScenario);
            try
            {
                scenario.stations[0].playerPoint = scenario.stations[0].companionPoint;
                scenario.stations.Add(scenario.stations[0]);
                Assert.That(scenario.ValidateConfiguration().Count, Is.GreaterThanOrEqualTo(2));
            }
            finally { Object.DestroyImmediate(scenario); }
        }

        [Test]
        public void NavigatorWalksAroundObstaclesWithoutCuttingThroughThem()
        {
            var path = new List<Vector2>();
            System.Func<Vector2, bool> free = p => !(p.x > 1f && p.x < 2f && p.y < 2f);
            System.Func<Vector2, Vector2, bool> clear = (a, b) => {
                for (int i = 0; i <= 20; i++) if (!free(Vector2.Lerp(a, b, i / 20f))) return false;
                return true;
            };
            Vector2 start = new Vector2(0.5f, 0.5f), goal = new Vector2(2.5f, 0.5f);
            Assert.That(CompanionNavigator.TryPath(start, goal, new Rect(0, 0, 4, 4), 0.25f, free, clear, path), Is.True);
            foreach (var point in path) { Assert.That(clear(start, point), Is.True); start = point; }
            Assert.That(start, Is.EqualTo(goal));
        }

        [Test]
        public void UnreachableOrOversizedNavigationDoesNotLeaveAStaleRoute()
        {
            var path = new List<Vector2> { Vector2.one };
            Assert.That(CompanionNavigator.TryPath(Vector2.one, Vector2.one * 3, new Rect(0, 0, 5, 5), 0.5f,
                p => true, (a, b) => false, path), Is.False);
            Assert.That(path, Is.Empty);
            Assert.That(CompanionNavigator.TryPath(Vector2.one, Vector2.one * 3, new Rect(0, 0, 1000, 1000), 0.01f,
                p => true, (a, b) => true, path), Is.False);
        }

        [Test]
        public void SuccessfulSnapshotRestoreNotifiesTransientActorsAfterApplyingState()
        {
            var snapshot = State.CreateSnapshot(); int calls = 0;
            State.StateRestored += () => { calls++; Assert.That(State.HasFlag("transient"), Is.False); };
            State.SetFlag("transient"); string error;
            Assert.That(State.TryRestoreSnapshot(snapshot, out error), Is.True, error);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(State.TryRestoreSnapshot(null, out error), Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void SharedGreetingDoesNotOverrideHurtAliceOrOtherRoomGreetings()
        {
            var profile = KnowledgeLibrary.GetNpc("Alice");
            State.SetCurrentScene("Room01"); State.SetFlag("companion.room01_alice.two_hands_box.completed");
            State.SetRelationship("Alice", 50);
            Assert.That(NpcOfflineReplies.FirstMatch(profile.returnGreetings, "", PlayerIntent.None, State).ruleId,
                Is.EqualTo("companion_shared_success"));
            State.SetRelationship("Alice", 10);
            Assert.That(NpcOfflineReplies.FirstMatch(profile.returnGreetings, "", PlayerIntent.None, State).ruleId,
                Is.Not.EqualTo("companion_shared_success"));
            State.SetRelationship("Alice", 50); State.SetCurrentScene("Room03");
            Assert.That(NpcOfflineReplies.FirstMatch(profile.returnGreetings, "", PlayerIntent.None, State).ruleId,
                Is.Not.EqualTo("companion_shared_success"));
        }

        [Test]
        public void OriginalRoom01CanBeSolvedWithoutCompanionFlags()
        {
            var host = new GameObject("CompanionOriginalPuzzleTest");
            try
            {
                var interactions = host.AddComponent<InteractionSystem>(); string response;
                foreach (string name in new[] { "Painting", "Desk" })
                    Assert.That(interactions.TryExecute(AssetDatabase.LoadAssetAtPath<InteractionData>(
                        "Assets/Data/Interactions/" + name + "_Data.asset"), out response), Is.True);
                Assert.That(interactions.TryExecute(AssetDatabase.LoadAssetAtPath<InteractionData>(
                    "Assets/Data/Interactions/Drawer_Data.asset"), out response, "4592"), Is.True);
                Assert.That(State.HasItem("key"), Is.True);
                Assert.That(InteractionSystem.CanExecute(AssetDatabase.LoadAssetAtPath<InteractionData>(
                    "Assets/Data/Interactions/Door_Data.asset"), State, out response), Is.True);
                var scenario = KnowledgeLibrary.GetRoom("Room01").companionScenario;
                Assert.That(State.HasFlag(scenario.CompletedFlag(scenario.stations[0])), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
