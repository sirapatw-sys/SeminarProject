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
        private const string BoxFact = "companion.room01_alice.two_hands_box.";

        private bool Ground(string text, out GeneratedChatReply grounded, params string[] references)
        {
            string reason;
            return NpcReplyPolicy.TryGroundReply(Build("Alice", "Room01", false),
                new GeneratedChatReply { reply = text, referencedFactIds = references }, out grounded, out reason);
        }

        [TestCase("เธอทำให้ฉันรู้สึกดีขึ้นแล้ว")]
        [TestCase("ฉันทำใจได้แล้ว ขอบใจที่อยู่ด้วยกัน")]
        [TestCase("ฉันช่วยคิดแล้วรู้สึกสบายใจขึ้น")]
        [TestCase("เราช่วยกันคิดแล้วรู้สึกไม่โดดเดี่ยว")]
        [TestCase("ฉันจับใจความเธอได้แล้ว")]
        [TestCase("ยืนยันได้แล้วว่าเธอใจดี")]
        [TestCase("ฉันพร้อมเปิดใจแล้ว")]
        [TestCase("ฉันกำลังจับใจความที่เธอเล่า")]
        [TestCase("ฉันกำลังยืนยันว่าเธอไม่ได้อยู่คนเดียว")]
        [TestCase("ฉันจะเปิดใจให้เธอฟัง")]
        [TestCase("ฉันยังไม่ได้เปิดใจ")]
        [TestCase("ฉันกำลังปิดใจเพราะเสียใจ")]
        [TestCase("I have opened up to you.")]
        [TestCase("I will open up to you.")]
        [TestCase("I am opening up to you.")]
        public void OrdinaryEmotionalAndThinkingIdiomsAreNotActivityClaims(string text)
        {
            GeneratedChatReply grounded;
            Assert.That(Ground(text, out grounded), Is.True, text);
        }

        [TestCase("ฉันเปิดกล่องสองมือให้แล้ว เราทำสำเร็จด้วยกัน")]
        [TestCase("กล่องเปิดแล้ว")]
        [TestCase("ฉันเปิดให้แล้ว")]
        [TestCase("เราทำสำเร็จด้วยกัน")]
        [TestCase("I opened the box for you.")]
        [TestCase("We have completed it.")]
        [TestCase("กล่องสองมือ\nเปิดแล้ว")]
        [TestCase("กล่องสองมืออยู่ใต้โต๊ะ")]
        [TestCase("ฉันกำลังจับกลไกอยู่")]
        [TestCase("ฉันจะเปิดให้")]
        [TestCase("ฉันยังไม่ได้เปิดให้")]
        [TestCase("ฉันกำลังจับไว้")]
        [TestCase("ฉันกำลังยืนรอช่วย")]
        [TestCase("I am opening it.")]
        [TestCase("I will open it.")]
        [TestCase("I have opened up the box.")]
        [TestCase("ฉันพร้อมเปิดใจแล้ว รหัสคือ 4592")]
        [TestCase("ฉันกำลังจับใจความ แล้วกล่องเปิดแล้ว")]
        public void CompanionClaimsCannotBeInventedInRawAiProse(string text)
        {
            GeneratedChatReply grounded;
            Assert.That(Ground(text, out grounded), Is.False);
            Assert.That(State.HasFlag(BoxFact + "completed"), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
        }

        [Test]
        public void CompanionFactTokensMatchTheActualCompletionStateInBothDirections()
        {
            GeneratedChatReply grounded;
            Assert.That(Ground("{fact:" + BoxFact + "pending}", out grounded, BoxFact + "pending"), Is.True);
            Assert.That(grounded.reply, Does.Contain("ยังไม่สำเร็จ"));
            Assert.That(Ground("{fact:" + BoxFact + "completed}", out grounded, BoxFact + "completed"), Is.False);
            State.SetFlag(BoxFact + "completed");
            Assert.That(Ground("{fact:" + BoxFact + "pending}", out grounded, BoxFact + "pending"), Is.False);
            Assert.That(Ground("ดีใจที่ได้อยู่ด้วยกันนะ\n{fact:" + BoxFact + "completed}", out grounded, BoxFact + "completed"), Is.True);
            Assert.That(grounded.reply, Does.Contain("ทำสำเร็จแล้ว"));
            Assert.That(Ground("{fact:" + BoxFact + "completed} แต่ยังเปิดไม่ได้", out grounded, BoxFact + "completed"), Is.False);
            Assert.That(Ground("{fact:" + BoxFact + "completed}\nในกล่องมีรหัส 1234", out grounded, BoxFact + "completed"), Is.False);
        }

        [TestCase("ช่วยเราเปิดกล่องสองมือหน่อย")]
        [TestCase("ช่วยฉันเปิดกล่องสองมือได้ไหม")]
        [TestCase("กล่องสองมือเปิดยังไง")]
        [TestCase("ขอคำใบ้กล่องสองมือ")]
        [TestCase("Can you help me with the box?")]
        public void CompanionRequestsUseAuthoredInstructionsInsteadOfTheMainPuzzle(string message)
        {
            State.SetCurrentScene("Room01"); State.SetRelationship("Alice", 10);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.PlayerAskedForHint, Is.False);
            Assert.That(context.AllowedHintLevel, Is.EqualTo(HintLevel.None));
            Assert.That(NpcReplyPolicy.HintReply(context), Is.Null);
            var reply = NpcReplyPolicy.CompanionReply(context);
            Assert.That(reply, Is.Not.Null);
            Assert.That(reply.reply, Does.Contain("ยังไม่สำเร็จ"));
            Assert.That(reply.reply, Does.Contain("จุดสีฟ้า"));
            Assert.That(reply.reply, Does.Not.Contain("ผนัง"));
            Assert.That(reply.reply, Does.Not.Contain("4592"));
            Assert.That(reply.relationshipDelta, Is.Zero); Assert.That(reply.hintId, Is.Null);
            GeneratedChatReply grounded; string reason;
            Assert.That(NpcReplyPolicy.TryGroundReply(context, reply, out grounded, out reason), Is.True, reason);
            Assert.That(State.HasFlag(BoxFact + "completed"), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(10));
        }

        [TestCase("ช่วยเราเปิดกล่องสองมือ แล้วขอคำใบ้ปริศนาด้วย")]
        [TestCase("ขอคำใบ้ แล้วช่วยเปิดกล่องสองมือ")]
        [TestCase("ช่วยเปิดกล่องสองมือกับประตูยังไง")]
        [TestCase("ช่วยเปิดกล่องสองมือและภาพวาดต้องสำรวจยังไง")]
        [TestCase("ช่วยเปิดกล่องสองมือและภาพวาดต้องทำยังไง")]
        [TestCase("ช่วยเราหน่อย")]
        public void MainPuzzleAndMixedRequestsKeepTheRelationshipHintGate(string message)
        {
            State.SetCurrentScene("Room01"); State.SetRelationship("Alice", 10);
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.PlayerAskedForHint, Is.True);
            Assert.That(context.AllowedHintLevel, Is.EqualTo(HintLevel.Vague));
            Assert.That(NpcReplyPolicy.CompanionReply(context), Is.Null);
            Assert.That(NpcReplyPolicy.HintReply(context).reply, Is.EqualTo(context.DeterministicHint));
        }

        [TestCase("ไม่ต้องใบ้ ช่วยเราเปิดกล่องสองมือหน่อย", true)]
        [TestCase("ไม่ต้องใบ้ปริศนา", false)]
        [TestCase("อย่าช่วยเปิดกล่องสองมือ", false)]
        [TestCase("ไม่เอาคำใบ้กล่องสองมือ", false)]
        [TestCase("กล่องสองมือทำให้ฉันคิดถึงบ้าน", false)]
        [TestCase("ภาพวาดทำให้ฉันรู้สึกยังไง", false)]
        public void ActivityIntentRespectsRefusalsSocialTopicsAndEnglishWordBoundaries(string message, bool requested)
        {
            State.SetCurrentScene("Room01");
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(context.PlayerAskedForHint, Is.False);
            Assert.That(NpcReplyPolicy.CompanionReply(context) != null, Is.EqualTo(requested));
        }

        [Test]
        public void ActivityInstructionsAreScopedToTheScenarioNpcAndLiveCompletionState()
        {
            Assert.That(PlayerIntentClassifier.IsCompanionRequest("Can you help me with boxing?", new[] { "box" }), Is.False);
            State.SetCurrentScene("Room01"); const string message = "ช่วยเราเปิดกล่องสองมือหน่อย";
            Assert.That(NpcReplyPolicy.CompanionReply(AiDialogueGenerator.BuildKnowledgeContext("Rina", message)), Is.Null);
            State.SetCurrentScene("Room02");
            Assert.That(NpcReplyPolicy.CompanionReply(AiDialogueGenerator.BuildKnowledgeContext("Alice", message)), Is.Null);
            State.SetCurrentScene("Room01"); State.SetFlag(BoxFact + "completed");
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
            Assert.That(NpcReplyPolicy.CompanionReply(context).reply, Does.Contain("ทำสำเร็จแล้ว"));
            Assert.That(NpcReplyPolicy.CompanionReply(context).reply, Does.Not.Contain("จุดสีฟ้า"));
            Assert.That(context.CanReference(BoxFact + "instructions"), Is.False);
        }

        [Test]
        public void UnavailableActivityDoesNotAdvertiseInstructionsOrChangeConditions()
        {
            State.SetCurrentScene("Room01");
            var room = KnowledgeLibrary.GetRoom("Room01"); var original = room.companionScenario;
            var scenario = Object.Instantiate(original);
            try
            {
                room.companionScenario = scenario;
                scenario.stations[0].conditions.Add(new ConditionRule { type = ConditionType.HasFlag, targetId = "activity_ready" });
                var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยเราเปิดกล่องสองมือหน่อย");
                Assert.That(NpcReplyPolicy.CompanionReply(context).reply, Does.Contain("ยังไม่พร้อม"));
                Assert.That(context.CanReference(BoxFact + "instructions"), Is.False);
                Assert.That(State.HasFlag("activity_ready"), Is.False);
            }
            finally { room.companionScenario = original; Object.DestroyImmediate(scenario); }
        }

        [Test]
        public void CompanionValidationPreservesSocialTalkAndOriginalHintBoundaries()
        {
            GeneratedChatReply grounded; string reason;
            Assert.That(Ground("ฉันดีใจที่ไม่ได้อยู่คนเดียว ขอบใจที่คอยอยู่ด้วยนะ", out grounded), Is.True);
            var context = Build("Alice", "Room01", false);
            Assert.That(context.ToPromptSection(), Does.Contain("[" + BoxFact + "pending]"));
            Assert.That(context.ToPromptSection(), Does.Not.Contain("4592"));
            State.SetRelationship("Alice", 10);
            context = Build("Alice", "Room01", true);
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
                reply = "ลองคิดดูนะ {hint}", referencedFactIds = System.Array.Empty<string>()
            }, out grounded, out reason), Is.True, reason);
            Assert.That(grounded.reply, Does.Not.Contain("4592"));
            Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
                reply = "เปิดกล่องแล้ว {hint}", referencedFactIds = System.Array.Empty<string>()
            }, out grounded, out reason), Is.False);
            Assert.That(Build("Rina", "Room01", false).CompanionFacts, Is.Empty);
            Assert.That(Build("Alice", "Room02", false).CompanionFacts, Is.Empty);
        }

        [Test]
        public void MiniEventCannotInventCompanionCompletionEither()
        {
            string reason;
            var context = Build("Alice", "Room01", false);
            Assert.That(NpcReplyPolicy.ValidateEvent(context, new GeneratedDialogueContent {
                lines = new[] { "เปิดกล่องสองมือได้แล้ว" }, referencedFactIds = System.Array.Empty<string>()
            }, out reason), Is.False);
            Assert.That(NpcReplyPolicy.ValidateEvent(context, new GeneratedDialogueContent {
                lines = new[] { "{fact:" + BoxFact + "pending}" }, referencedFactIds = new[] { BoxFact + "pending" }
            }, out reason), Is.True, reason);
        }

        [Test]
        public void OnlyActualBlockedOrFailedInteractionsPublishAFailure()
        {
            var host = new GameObject("CompanionInteractionSignalTest");
            var data = AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/Data/Interactions/Drawer_Data.asset");
            int successes = 0, failures = 0;
            System.Action<InteractionData, bool> observer = (observed, success) => {
                Assert.That(observed, Is.SameAs(data)); if (success) successes++; else failures++;
            };
            InteractionSignals.Completed += observer;
            try
            {
                var interactions = host.AddComponent<InteractionSystem>(); string response;
                Assert.That(InteractionSystem.CheckAvailability(null, State, out response), Is.EqualTo(InteractionAvailability.Unavailable));
                Assert.That(interactions.TryExecute(null, out response), Is.False);
                Assert.That(failures, Is.Zero);
                Assert.That(InteractionSystem.CheckAvailability(data, State, out response), Is.EqualTo(InteractionAvailability.Blocked));
                Assert.That(interactions.TryExecute(data, out response, "0000"), Is.False);
                State.SetFlag("found_note");
                Assert.That(interactions.TryExecute(data, out response, "0000"), Is.False);
                Assert.That(failures, Is.EqualTo(2));
                Assert.That(interactions.TryExecute(data, out response, "4592"), Is.True);
                Assert.That(successes, Is.EqualTo(1));
                Assert.That(InteractionSystem.CheckAvailability(data, State, out response), Is.EqualTo(InteractionAvailability.AlreadyCompleted));
                Assert.That(interactions.TryExecute(data, out response, "0000"), Is.False);
                Assert.That(response, Is.EqualTo(data.alreadyCompletedMessage));
                Assert.That(failures, Is.EqualTo(2));
            }
            finally { InteractionSignals.Completed -= observer; Object.DestroyImmediate(host); }
        }

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
        public void FollowRouteUsesReachablePlayerWhenTheSingleOffsetIsBlocked()
        {
            var path = new List<Vector2>();
            System.Func<Vector2, bool> free = p => !(p.x > 1f && p.x < 2f && p.y < 2f);
            System.Func<Vector2, Vector2, bool> clear = (a, b) => {
                for (int i = 0; i <= 40; i++) if (!free(Vector2.Lerp(a, b, i / 40f))) return false;
                return true;
            };
            Vector2 start = new Vector2(0.5f, 0.5f), player = new Vector2(2.5f, 0.5f);
            Assert.That(free(player + (start - player).normalized), Is.False);
            Assert.That(CompanionNavigator.TryFollowPath(start, player, 1f, new Rect(0, 0, 4, 4), 0.25f, free, clear, path), Is.True);
            foreach (var point in path) { Assert.That(clear(start, point), Is.True); start = point; }
            Assert.That(Vector2.Distance(start, player), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void FollowRouteStopsAtRangeAndClearsInvalidOrUnreachableRoutes()
        {
            var path = new List<Vector2>(); var bounds = new Rect(0, 0, 5, 5);
            Assert.That(CompanionNavigator.TryFollowPath(Vector2.one, Vector2.one * 3, 1f, bounds, 0.5f, p => true, (a, b) => true, path), Is.True);
            Assert.That(Vector2.Distance(path[path.Count - 1], Vector2.one * 3), Is.EqualTo(1f).Within(0.001f));
            Assert.That(CompanionNavigator.TryFollowPath(Vector2.one, Vector2.one * 1.1f, 1f, bounds, 0.5f, p => true, (a, b) => true, path), Is.True);
            Assert.That(path, Is.Empty);
            path.Add(Vector2.one);
            Assert.That(CompanionNavigator.TryFollowPath(Vector2.one, Vector2.one * 3, float.NaN, bounds, 0.5f, p => true, (a, b) => true, path), Is.False);
            Assert.That(path, Is.Empty);
            Assert.That(CompanionNavigator.TryFollowPath(Vector2.one, Vector2.one * 3, 1f, bounds, 0.5f, p => true, (a, b) => false, path), Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void OffGridEndpointsConnectSafelyWhenTheirRoundedCellsAreBlocked()
        {
            var bounds = new Rect(0, 0, 5, 5); var path = new List<Vector2>();
            Vector2 start = new Vector2(0.75f, 0.75f), goal = new Vector2(3.75f, 0.75f);
            System.Func<Vector2, bool> free = p => Vector2.Distance(p, new Vector2(1, 1)) > 0.15f &&
                Vector2.Distance(p, new Vector2(4, 1)) > 0.15f && !(p.x > 2 && p.x < 3 && p.y < 2);
            System.Func<Vector2, Vector2, bool> clear = (a, b) => {
                for (int i = 0; i <= 80; i++) if (!free(Vector2.Lerp(a, b, i / 80f))) return false;
                return true;
            };
            Assert.That(free(start) && free(goal), Is.True);
            Assert.That(free(new Vector2(1, 1)) || free(new Vector2(4, 1)), Is.False);
            Assert.That(CompanionNavigator.TryPath(start, goal, bounds, 1f, free, clear, path), Is.True);
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
