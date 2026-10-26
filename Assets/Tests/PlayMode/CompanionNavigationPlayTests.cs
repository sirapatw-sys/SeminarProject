using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CompanionNavigationPlayTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private NpcCompanionController actor;
    private CompanionStationData station;
    private bool persistence, title, intro;
    private Vector2 spawn;
    private GameState State { get { return GameState.Instance; } }
    private class Offline : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null) { done(null); yield break; }
    }
    private static void Title(bool value) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, value); }
    private static void Intro(bool value) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, value); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        DialogueProviders.Override = new Offline(); GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState(); actor = NpcCompanionController.Find("Alice", "Room01");
        Assert.That(actor, Is.Not.Null); station = actor.Scenario.stations[0]; spawn = actor.Feet;
    }
    [TearDown]
    public void Cleanup()
    {
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = persistence; Title(title); Intro(intro);
    }
    private void PlayerAt(Vector2 point)
    {
        GameObject.FindGameObjectWithTag("Player").transform.position += (Vector3)(point - actor.PlayerFeet);
        Physics2D.SyncTransforms();
    }
    private void ActorAt(Vector2 point) { actor.transform.position += (Vector3)(point - actor.Feet); Physics2D.SyncTransforms(); }
    private void Step(float dt) { actor.Tick(dt); Physics2D.SyncTransforms(); }
    private bool Clear(Vector2 from, Vector2 to)
    {
        return (bool)typeof(NpcCompanionController).GetMethod("IsClear", Private).Invoke(actor, new object[] { from, to });
    }

    [UnityTest]
    public IEnumerator SaturatedOverlapBuffersNeverLoseARealBlocker()
    {
        var center = new Vector2(0, -3.4f);
        var objects = new List<GameObject>();
        try
        {
            for (int i = 0; i < 80; i++)
            {
                var item = new GameObject("QueryOverflow" + i); objects.Add(item);
                item.transform.position = center;
                var collider = item.AddComponent<CircleCollider2D>(); collider.radius = 0.04f; collider.isTrigger = true;
            }
            var blocker = new GameObject("OverflowBlocker"); objects.Add(blocker);
            blocker.transform.position = center; blocker.AddComponent<CircleCollider2D>().radius = 0.05f;
            Physics2D.SyncTransforms();
            Assert.That(actor.IsFree(center), Is.False);
            Assert.That(Clear(center + Vector2.left, center + Vector2.right), Is.False);
            UnityEngine.Object.DestroyImmediate(blocker); Physics2D.SyncTransforms();
            Assert.That(actor.IsFree(center), Is.True);
            Assert.That(Clear(center + Vector2.left, center + Vector2.right), Is.True);
        }
        finally { foreach (var item in objects) if (item != null) UnityEngine.Object.DestroyImmediate(item); }
        yield return null;
    }
    // Independent endpoint-overlap samples, rather than trusting only the
    // sweep predicate that the production path finder itself uses.
    private bool FreeAlongStep(Vector2 from, Vector2 to)
    {
        for (int i = 0; i <= 8; i++)
            if (!actor.IsFree(Vector2.Lerp(from, to, i / 8f))) return false;
        return true;
    }

    private void FollowTo(Vector2 target, float dt)
    {
        State.ResetState(); ActorAt(station.companionPoint); PlayerAt(station.companionPoint + Vector2.right);
        Assert.That(actor.RequestFollow(), Is.True); PlayerAt(target);
        var route = new List<Vector2>();
        Assert.That(CompanionNavigator.TryFollowPath(actor.Feet, target, actor.Scenario.followDistance,
            actor.Scenario.walkBounds, actor.Scenario.cellSize, actor.IsFree, Clear, route), Is.True,
            "Reviewed target must remain reachable: " + target);
        for (int i = 0; i < Mathf.CeilToInt(30f / dt) &&
            Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f; i++)
        {
            Vector2 before = actor.Feet; Step(dt);
            Assert.That(FreeAlongStep(before, actor.Feet), Is.True, "Unsafe footprint: " + before + " -> " + actor.Feet);
            Assert.That(Vector2.Distance(before, actor.Feet), Is.LessThanOrEqualTo(actor.Scenario.moveSpeed * dt + 0.001f));
            Assert.That(Vector2.Distance(actor.Feet, target), Is.GreaterThanOrEqualTo(actor.Scenario.followDistance - 0.01f));
        }
        Assert.That(Vector2.Distance(actor.Feet, target), Is.LessThanOrEqualTo(actor.Scenario.followDistance + 0.07f),
            "dt=" + dt + " target=" + target + " feet=" + actor.Feet.ToString("F4") + " speed=" + actor.MovementSpeed);
    }

    [UnityTest]
    public IEnumerator ReportedStallTargetsRemainReachableAtEveryReviewedFrameStep()
    {
        foreach (float dt in new[] { 1f / 60f, 1f / 30f, 0.1f })
            foreach (Vector2 target in new[] { new Vector2(4.6f, -1.7f), new Vector2(5.9f, -1.7f),
                new Vector2(5.9f, 0.3f), new Vector2(-5.8f, 1.3f), new Vector2(-4.5f, 1.3f), new Vector2(-6.2f, 1.2f) })
                FollowTo(target, dt);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SweepsRejectReviewedCornerCutsEvenWhenBothEndpointsAreFree()
    {
        var segments = new[] {
            new[] { new Vector2(2.559973f, -1.966959f), new Vector2(3f, -1.9f) },
            new[] { new Vector2(3.649340f, -0.5855661f), new Vector2(4.411108f, -0.2858337f) },
            new[] { new Vector2(-1.591596f, 1.079247f), new Vector2(-1.8f, 1.3f) },
            new[] { new Vector2(-5.818243f, -0.7765707f), new Vector2(-6.121441f, -0.3980703f) }
        };
        foreach (var segment in segments)
        {
            Assert.That(actor.IsFree(segment[0]) && actor.IsFree(segment[1]), Is.True, "Endpoints should be free.");
            Assert.That(Clear(segment[0], segment[1]), Is.False, "Do not shortcut through a shallow corner.");
            Assert.That(Clear(segment[1], segment[0]), Is.False, "The same swept footprint must block both directions.");
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator SweepsBlockThinRotatedAndRoundObstaclesButIgnoreTriggersAndTheActors()
    {
        Vector2 start = new Vector2(-4.7f, -3f), end = new Vector2(-3.3f, -3f);
        Assert.That(Clear(start, end), Is.True);
        var obstacle = new GameObject("CompanionSweepRegressionObstacle");
        try
        {
            obstacle.transform.position = (start + end) * 0.5f;
            var box = obstacle.AddComponent<BoxCollider2D>(); box.size = new Vector2(0.015f, 0.8f);
            Physics2D.SyncTransforms();
            Assert.That(actor.IsFree(start) && actor.IsFree(end), Is.True);
            Assert.That(Clear(start, end) || Clear(end, start), Is.False, "A thin wall cannot be skipped.");
            Assert.That(Clear(start, start), Is.True); Assert.That(Clear(obstacle.transform.position, obstacle.transform.position), Is.False);
            obstacle.transform.rotation = Quaternion.Euler(0f, 0f, 35f); Physics2D.SyncTransforms();
            Assert.That(Clear(start, end) || Clear(end, start), Is.False, "Rotated walls still block.");
            box.isTrigger = true; Physics2D.SyncTransforms(); Assert.That(Clear(start, end), Is.True);
            box.isTrigger = false; obstacle.transform.SetParent(actor.transform, true); Physics2D.SyncTransforms();
            Assert.That(Clear(start, end), Is.True, "Ignore the companion's own colliders.");
            obstacle.transform.SetParent(GameObject.FindGameObjectWithTag("Player").transform, true); Physics2D.SyncTransforms();
            Assert.That(Clear(start, end), Is.True, "Do not treat the player as furniture.");
            obstacle.transform.SetParent(null, true); UnityEngine.Object.DestroyImmediate(box);
            obstacle.AddComponent<CircleCollider2D>().radius = 0.15f; Physics2D.SyncTransforms();
            Assert.That(Clear(start, end) || Clear(end, start), Is.False, "Round obstacles still block.");
            obstacle.transform.position += Vector3.down * 0.8f; Physics2D.SyncTransforms();
            Assert.That(Clear(start, end) && Clear(end, start), Is.True, "Do not block a clear nearby corridor.");
            Assert.That(Clear(new Vector2(-100f, -100f), start), Is.False, "Stay inside the authored room bounds.");
        }
        finally { UnityEngine.Object.DestroyImmediate(obstacle); Physics2D.SyncTransforms(); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ARemovedObstacleAllowsFollowingToResumeWithoutTeleporting()
    {
        State.ResetState(); ActorAt(station.companionPoint); PlayerAt(station.companionPoint + Vector2.right);
        Assert.That(actor.RequestFollow(), Is.True); Vector2 target = new Vector2(4.6f, -1.7f); PlayerAt(target);
        Vector2 before = actor.Feet;
        var obstacle = new GameObject("CompanionTemporaryObstacle");
        try
        {
            // Enclose the footprint temporarily, as a moving prop might do.
            obstacle.transform.position = before; obstacle.AddComponent<BoxCollider2D>().size = Vector2.one;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 20; i++) Step(0.05f);
            Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(actor.MovementSpeed, Is.Zero);
        }
        finally { UnityEngine.Object.DestroyImmediate(obstacle); Physics2D.SyncTransforms(); }
        for (int i = 0; i < 600 && Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f; i++)
        {
            before = actor.Feet; Step(0.05f);
            Assert.That(FreeAlongStep(before, actor.Feet), Is.True);
            Assert.That(Vector2.Distance(before, actor.Feet), Is.LessThanOrEqualTo(actor.Scenario.moveSpeed * 0.05f + 0.001f));
        }
        Assert.That(Vector2.Distance(actor.Feet, target), Is.LessThanOrEqualTo(actor.Scenario.followDistance + 0.07f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FollowConvergesAcrossDenseRoomSamplesAtDifferentFrameSteps()
    {
        var failures = new List<string>(); int sampled = 0;
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector2 playerSize = (Vector2)player.GetComponent<Collider2D>().bounds.size + Vector2.one * 0.1f;
        var targets = new List<Vector2>();
        for (float y = -3.7f; y <= 1.3f; y += 1f)
            for (float x = -7.1f; x <= 7.1f; x += 1.3f) targets.Add(new Vector2(x, y));
        targets.AddRange(new[] { new Vector2(-6.9f, 1.2f), new Vector2(-6.5f, 0.8f), new Vector2(-6.2f, 1.2f) });
        var route = new List<Vector2>();
        foreach (float dt in new[] { 1f / 60f, 1f / 30f, 0.1f })
        foreach (Vector2 target in targets)
        {
            Vector2 start = station.companionPoint;
            if (Vector2.Distance(start, target) < 2.3f || !actor.IsFree(target)) continue;
            if (Physics2D.OverlapBoxAll(target, playerSize, 0f).Any(c => !c.isTrigger &&
                c.transform != player.transform && !c.transform.IsChildOf(player.transform))) continue;
            State.ResetState(); ActorAt(start); PlayerAt(start + Vector2.right);
            if (!CompanionNavigator.TryFollowPath(start, target, actor.Scenario.followDistance,
                actor.Scenario.walkBounds, actor.Scenario.cellSize, actor.IsFree, Clear, route)) continue;
            Assert.That(actor.RequestFollow(), Is.True); PlayerAt(target);
            Vector2 previous = actor.Feet; bool unsafeStep = false;
            for (int i = 0; i < Mathf.CeilToInt(30f / dt) &&
                Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f; i++)
            {
                Step(dt);
                if (!Clear(previous, actor.Feet) || !FreeAlongStep(previous, actor.Feet) || !actor.IsFree(actor.Feet) ||
                    Vector2.Distance(previous, actor.Feet) > actor.Scenario.moveSpeed * dt + 0.001f ||
                    Vector2.Distance(actor.Feet, target) < actor.Scenario.followDistance - 0.01f)
                { unsafeStep = true; break; }
                previous = actor.Feet;
            }
            sampled++;
            float remaining = Vector2.Distance(actor.Feet, target);
            string detail = "dt=" + dt + " target=" + target.ToString("F4") +
                " feet=" + actor.Feet.ToString("F4") + " remaining=" + remaining + " speed=" + actor.MovementSpeed;
            if (unsafeStep || remaining > actor.Scenario.followDistance + 0.07f)
            {
                bool canReplan = CompanionNavigator.TryFollowPath(actor.Feet, target, actor.Scenario.followDistance,
                    actor.Scenario.walkBounds, actor.Scenario.cellSize, actor.IsFree, Clear, route);
                failures.Add(detail + " unsafe=" + unsafeStep + " canReplan=" + canReplan +
                    " route=" + string.Join(";", route.Take(6).Select(p => p.ToString("F4"))));
            }
        }
        Debug.Log("NAVIGATION_REGRESSION dense follow sampled=" + sampled + " failures=" + failures.Count);
        foreach (string failure in failures) Debug.Log("NAVIGATION_REGRESSION FAILURE " + failure);
        Assert.That(sampled, Is.GreaterThanOrEqualTo(114), "Do not hide reachable review cases by skipping routes.");
        Assert.That(failures, Is.Empty, string.Join("\n", failures)); yield return null;
    }

    [UnityTest]
    public IEnumerator SlowHelpCompletesAtDifferentFrameStepsWithoutDuplicateRewards()
    {
        foreach (float dt in new[] { 1f / 60f, 1f / 30f, 0.1f })
        {
            State.ResetState(); State.SetRelationship("Alice", 10); ActorAt(spawn); PlayerAt(station.playerPoint);
            Assert.That(actor.RequestHelp(station), Is.True);
            for (int i = 0; i < Mathf.CeilToInt(40f / dt) && !actor.IsCompleted(station); i++) Step(dt);
            Assert.That(actor.IsCompleted(station), Is.True, "dt=" + dt + " feet=" + actor.Feet);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(13));
            Assert.That(actor.MovementSpeed, Is.Zero);
            Assert.That(actor.RequestHelp(station), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(13));
            Assert.That(State.HasItem("key"), Is.False);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator SaveRestoreDoesNotRetainMomentumOrUnfinishedTasks()
    {
        ActorAt(new Vector2(-4f, -2.65f)); PlayerAt(new Vector2(-0.9f, -2.65f));
        Assert.That(actor.RequestFollow(), Is.True);
        for (int i = 0; i < 10; i++) Step(1f / 60f);
        Assert.That(actor.MovementSpeed, Is.GreaterThan(0f)); Vector2 before = actor.Feet;
        var snapshot = State.CreateSnapshot(); string error;
        Assert.That(State.TryRestoreSnapshot(snapshot, out error), Is.True, error);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.MovementSpeed, Is.Zero);
        Step(0.1f); Assert.That(actor.Feet, Is.EqualTo(before));
        ActorAt(station.companionPoint); PlayerAt(station.playerPoint);
        Assert.That(actor.RequestHelp(station), Is.True); Step(0.1f);
        Assert.That(actor.Progress, Is.GreaterThan(0f));
        snapshot = State.CreateSnapshot();
        Assert.That(State.TryRestoreSnapshot(snapshot, out error), Is.True, error);
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.Progress, Is.Zero);
        Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CompanionVisualIsBoundAndStopsItsWalkBlendDuringPause()
    {
        var visual = actor.GetComponent<CharacterVisualController>(); Assert.That(visual, Is.Not.Null);
        Assert.That(typeof(CharacterVisualController).GetField("companion", Private).GetValue(visual), Is.SameAs(actor));
        ActorAt(new Vector2(-4f, -2.65f)); PlayerAt(new Vector2(-0.9f, -2.65f));
        actor.RequestFollow();
        for (int i = 0; i < 15; i++) { Step(1f / 60f); yield return null; }
        float blend = (float)typeof(CharacterVisualController).GetField("companionWalkBlend", Private).GetValue(visual);
        Assert.That(blend, Is.GreaterThan(0f));
        Title(true);
        // Hidden editors can run 25 frames in far less than the 0.2 seconds
        // needed to fade a full blend. Wait elapsed time, not a frame count.
        yield return new WaitForSecondsRealtime(0.35f); yield return null;
        Assert.That(actor.MovementSpeed, Is.Zero);
        Assert.That((float)typeof(CharacterVisualController).GetField("companionWalkBlend", Private).GetValue(visual), Is.Zero.Within(0.001f));
        Title(false); yield return null;
    }
}
