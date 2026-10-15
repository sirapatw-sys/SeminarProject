using System;
using System.Collections;
using System.Reflection;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CompanionPresentationPlayTests
{
    private NpcCompanionController actor;
    private CompanionStationData station;
    private CompanionHud hud;
    private bool persistence, title, intro;
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
        Assert.That(actor, Is.Not.Null); hud = actor.GetComponent<CompanionHud>(); station = actor.Scenario.stations[0];
        Assert.That(hud, Is.Not.Null); Assert.That(InputGate.IsBlocked, Is.False);
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
    private void Tick(float seconds)
    {
        for (int i = 0; i < Mathf.CeilToInt(seconds / 0.05f); i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
    }
    private void FollowInOpenSpace()
    {
        ActorAt(new Vector2(-4f, -2.65f)); PlayerAt(new Vector2(-0.9f, -2.65f));
        Assert.That(actor.RequestFollow(), Is.True);
    }

    [UnityTest]
    public IEnumerator LabelsAreNearbyOnlyAndMarkersRequireAnActiveActivity()
    {
        PlayerAt(new Vector2(-5f, -2.65f));
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Hidden));
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Nearby));
        Assert.That(actor.RequestHelp(station), Is.True);
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Active));
        PlayerAt(new Vector2(-5f, -2.65f));
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Hidden));
        Assert.That(actor.IsCompleted(station), Is.False);
        Assert.That(State.HasItem("key"), Is.False); yield return null;
    }

    [UnityTest]
    public IEnumerator CompletedActivityKeepsItsPropButNoLabelOrMarkers()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        Assert.That(actor.RequestHelp(station), Is.True); Tick(3.2f);
        Assert.That(actor.IsCompleted(station), Is.True);
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Hidden));
        Assert.That(actor.Progress, Is.Zero); Assert.That(actor.MovementSpeed, Is.Zero); yield return null;
    }

    [UnityTest]
    public IEnumerator StationPresentationDoesNotAppearInMenusOtherRoomsOrForUnknownStations()
    {
        PlayerAt(station.playerPoint);
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Nearby));
        Title(true); Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Hidden)); Title(false);
        State.SetCurrentScene("Room02");
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Hidden)); State.SetCurrentScene("Room01");
        Assert.That(hud.StationDisplayFor(null), Is.EqualTo(CompanionStationDisplay.Hidden));
        Assert.That(hud.StationDisplayFor(new CompanionStationData()), Is.EqualTo(CompanionStationDisplay.Hidden));
        actor.enabled = false;
        Assert.That(hud.StationDisplayFor(station), Is.EqualTo(CompanionStationDisplay.Hidden)); yield return null;
    }

    [UnityTest]
    public IEnumerator WalkingAcceleratesGraduallyAndNeverExceedsTheAuthoredSpeed()
    {
        FollowInOpenSpace(); float previousSpeed = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 before = actor.Feet; actor.Tick(0.05f); Physics2D.SyncTransforms();
            Assert.That(actor.MovementSpeed, Is.GreaterThan(previousSpeed));
            Assert.That(actor.MovementSpeed, Is.LessThanOrEqualTo(previousSpeed + actor.Scenario.moveAcceleration * 0.05f + 0.001f));
            Assert.That(Vector2.Distance(before, actor.Feet), Is.LessThanOrEqualTo(actor.Scenario.moveSpeed * 0.05f + 0.001f));
            previousSpeed = actor.MovementSpeed;
        }
        for (int i = 0; i < 100; i++)
        {
            actor.Tick(0.05f); Physics2D.SyncTransforms();
            Assert.That(actor.MovementSpeed, Is.InRange(0f, actor.Scenario.moveSpeed + 0.001f));
        }
        Assert.That(actor.MovementSpeed, Is.Zero);
        Assert.That(Vector2.Distance(actor.Feet, actor.PlayerFeet), Is.InRange(actor.Scenario.followDistance - 0.01f, actor.Scenario.followDistance + 0.07f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator WalkingSlowsDownBeforeReachingItsStoppingDistance()
    {
        FollowInOpenSpace(); float previousSpeed = 0f; bool cruised = false, decelerated = false;
        for (int i = 0; i < 120; i++)
        {
            actor.Tick(0.05f); Physics2D.SyncTransforms();
            cruised |= actor.MovementSpeed > actor.Scenario.moveSpeed * 0.8f;
            decelerated |= cruised && actor.MovementSpeed > 0f && actor.MovementSpeed < previousSpeed - 0.01f;
            previousSpeed = actor.MovementSpeed;
        }
        Assert.That(cruised, Is.True); Assert.That(decelerated, Is.True);
        Assert.That(actor.MovementSpeed, Is.Zero); yield return null;
    }

    [UnityTest]
    public IEnumerator AStoppedCompanionDoesNotShuffleWithinTheFollowRestartBuffer()
    {
        ActorAt(new Vector2(-3f, -2.65f)); PlayerAt(new Vector2(-1.2f, -2.65f));
        Assert.That(actor.RequestFollow(), Is.True); Vector2 before = actor.Feet; Tick(1f);
        Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(actor.MovementSpeed, Is.Zero);
        PlayerAt(new Vector2(0f, -2.65f)); Tick(5f);
        before = actor.Feet;
        for (int i = 0; i < 10; i++)
        {
            PlayerAt(new Vector2(i % 2 == 0 ? 0.08f : -0.08f, -2.65f)); Tick(0.15f);
            Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(actor.MovementSpeed, Is.Zero);
        }
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        PlayerAt(new Vector2(0.8f, -2.65f)); Tick(0.2f);
        Assert.That(Vector2.Distance(before, actor.Feet), Is.GreaterThan(0.01f)); yield return null;
    }

    [UnityTest]
    public IEnumerator ModalPauseAndWaitDiscardMomentumWithoutMovingOrRewarding()
    {
        FollowInOpenSpace(); Tick(0.3f); Assert.That(actor.MovementSpeed, Is.GreaterThan(0f));
        Vector2 before = actor.Feet; Title(true); Tick(1f);
        Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(actor.MovementSpeed, Is.Zero);
        Title(false); actor.Tick(0.05f); Physics2D.SyncTransforms();
        Assert.That(actor.MovementSpeed, Is.LessThanOrEqualTo(actor.Scenario.moveAcceleration * 0.05f + 0.001f));
        Assert.That(actor.RequestWait(), Is.True); before = actor.Feet; Tick(1f);
        Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(actor.MovementSpeed, Is.Zero);
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.IsCompleted(station), Is.False); yield return null;
    }

    [UnityTest]
    public IEnumerator SmoothWalkingStillReachesTheTightFurnitureCorner()
    {
        ActorAt(station.companionPoint); PlayerAt(station.companionPoint + Vector2.right);
        Assert.That(actor.RequestFollow(), Is.True);
        Vector2 target = new Vector2(-6.9f, 1.2f); PlayerAt(target);
        var clear = typeof(NpcCompanionController).GetMethod("IsClear", BindingFlags.Instance | BindingFlags.NonPublic);
        Vector2 previous = actor.Feet;
        for (int i = 0; i < 500 && Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f; i++)
        {
            actor.Tick(0.05f); Physics2D.SyncTransforms();
            Assert.That((bool)clear.Invoke(actor, new object[] { previous, actor.Feet }), Is.True);
            Assert.That(actor.IsFree(actor.Feet), Is.True);
            Assert.That(Vector2.Distance(previous, actor.Feet), Is.LessThanOrEqualTo(actor.Scenario.moveSpeed * 0.05f + 0.001f));
            previous = actor.Feet;
        }
        Assert.That(Vector2.Distance(actor.Feet, target), Is.LessThanOrEqualTo(actor.Scenario.followDistance + 0.07f),
            "Final feet=" + actor.Feet.ToString("F4") + "; speed=" + actor.MovementSpeed);
        yield return null;
    }

    [UnityTest]
    public IEnumerator SlowerWalkingStillCompletesTheActivityFromItsAuthoredSpawn()
    {
        PlayerAt(station.playerPoint); Assert.That(actor.RequestHelp(station), Is.True);
        var clear = typeof(NpcCompanionController).GetMethod("IsClear", BindingFlags.Instance | BindingFlags.NonPublic);
        Vector2 previous = actor.Feet;
        for (int i = 0; i < 400 && !actor.IsCompleted(station); i++)
        {
            actor.Tick(0.05f); Physics2D.SyncTransforms();
            Assert.That(Vector2.Distance(previous, actor.Feet), Is.LessThanOrEqualTo(actor.Scenario.moveSpeed * 0.05f + 0.001f));
            Assert.That((bool)clear.Invoke(actor, new object[] { previous, actor.Feet }), Is.True);
            previous = actor.Feet;
        }
        Assert.That(actor.IsCompleted(station), Is.True);
        Assert.That(actor.MovementSpeed, Is.Zero); Assert.That(State.HasItem("key"), Is.False); yield return null;
    }
}
