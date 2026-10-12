using System;
using System.Collections;
using System.Linq;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Room01CompanionTests
{
    private class OfflineProvider : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null) { done(null); yield break; }
    }
    private NpcCompanionController actor;
    private CompanionStationData station;
    private bool persistence, title, intro;
    private GameState State { get { return GameState.Instance; } }
    private static void Title(bool open) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, open); }
    private static void Intro(bool open) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, open); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        DialogueProviders.Override = new OfflineProvider(); GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState();
        actor = NpcCompanionController.Find("Alice", "Room01");
        Assert.That(actor, Is.Not.Null); Assert.That(actor.GetComponent<CompanionHud>(), Is.Not.Null);
        station = actor.Scenario.stations[0];
        Assert.That(InputGate.IsBlocked, Is.False);
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
        var player = GameObject.FindGameObjectWithTag("Player");
        player.transform.position += (Vector3)(point - actor.PlayerFeet); Physics2D.SyncTransforms();
    }
    private void ActorAt(Vector2 point) { actor.transform.position += (Vector3)(point - actor.Feet); Physics2D.SyncTransforms(); }
    private void Tick(float seconds)
    {
        for (int i = 0; i < Mathf.CeilToInt(seconds / 0.05f); i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
    }
    private void BeginAtStation()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        Assert.That(actor.RequestHelp(station), Is.True);
    }
    [UnityTest]
    public IEnumerator FollowMovesWithoutTeleportingAndWaitWorksFromFarAway()
    {
        Assert.That(actor.RequestFollow(), Is.True); Vector2 before = actor.Feet;
        PlayerAt(new Vector2(-2f, -2.7f)); Tick(0.1f);
        Assert.That(Vector2.Distance(before, actor.Feet), Is.InRange(0.01f, 0.5f));
        Assert.That(actor.IsFree(actor.Feet), Is.True);
        PlayerAt(new Vector2(-6f, -2f));
        Assert.That(actor.CanCommand, Is.False); Assert.That(actor.RequestWait(), Is.True);
        before = actor.Feet; Tick(2f); Assert.That(actor.Feet, Is.EqualTo(before)); yield return null;
    }
    [UnityTest]
    public IEnumerator AliceWalksToHerSideFromTheAuthoredSpawnAndResumesFollowing()
    {
        PlayerAt(station.playerPoint);
        Assert.That(actor.IsFree(station.companionPoint), Is.True);
        Assert.That(actor.IsFree(station.playerPoint), Is.True);
        Assert.That(actor.RequestFollow(), Is.True);
        Assert.That(actor.RequestHelp(station), Is.True);
        Vector2 previous = actor.Feet;
        for (int i = 0; i < 240 && !actor.IsCompleted(station); i++)
        {
            actor.Tick(0.05f); Physics2D.SyncTransforms();
            Assert.That(Vector2.Distance(previous, actor.Feet), Is.LessThanOrEqualTo(0.23f));
            Assert.That(actor.IsFree(actor.Feet), Is.True); previous = actor.Feet;
        }
        Assert.That(actor.IsCompleted(station), Is.True);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        yield return null;
    }
    [UnityTest]
    public IEnumerator LowRelationshipCanCooperateButCannotFarmCompletionRewards()
    {
        State.SetRelationship("Alice", 10); BeginAtStation();
        Tick(2f); Assert.That(actor.IsCompleted(station), Is.False);
        Tick(1.2f); Assert.That(actor.IsCompleted(station), Is.True);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(13));
        Assert.That(State.GetNpcMemory("Alice"), Does.Contain(station.memory));
        Assert.That(State.GetJournal().Any(e => e.Id == actor.Scenario.CompletedFlag(station)), Is.True);
        Assert.That(actor.RequestHelp(station), Is.False); Tick(5f);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(13));
        Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator LeavingPadResetsProgressAndModalPausesBothActorsAndTimer()
    {
        BeginAtStation(); Tick(1f); Assert.That(actor.Progress, Is.GreaterThan(0f));
        PlayerAt(station.playerPoint + Vector2.down); Tick(0.1f); Assert.That(actor.Progress, Is.Zero);
        PlayerAt(station.playerPoint); Tick(1f); float progress = actor.Progress; Vector2 feet = actor.Feet;
        Title(true); Tick(60f); Assert.That(actor.Progress, Is.EqualTo(progress)); Assert.That(actor.Feet, Is.EqualTo(feet));
        Assert.That(actor.RequestFollow(), Is.False); Title(false);
        ItemPopupUI.ShowItem("key"); Tick(60f);
        Assert.That(actor.Progress, Is.EqualTo(progress)); Assert.That(actor.RequestWait(), Is.False);
        ItemPopupUI.CancelAll();
        Tick(2.2f); Assert.That(actor.IsCompleted(station), Is.True); yield return null;
    }
    [UnityTest]
    public IEnumerator CancelAndTimeoutHaveNoSuccessSideEffects()
    {
        int relationship = State.GetRelationship("Alice"); BeginAtStation(); Tick(1f);
        Assert.That(actor.RequestWait(), Is.True); Tick(5f); Assert.That(actor.IsCompleted(station), Is.False);
        BeginAtStation(); PlayerAt(station.playerPoint + Vector2.down * 2); Tick(actor.Scenario.taskTimeoutSeconds + 1f);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship));
        yield return null;
    }
    [UnityTest]
    public IEnumerator SaveRestoreKeepsMemoryButNeverResumesAnUnfinishedTask()
    {
        BeginAtStation(); Tick(3.2f); var completed = State.CreateSnapshot();
        State.ResetState(); BeginAtStation(); Tick(1f); string error;
        Assert.That(State.TryRestoreSnapshot(completed, out error), Is.True, error);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.Progress, Is.Zero);
        Assert.That(actor.IsCompleted(station), Is.True); Assert.That(actor.RequestHelp(station), Is.False);
        Assert.That(State.GetNpcMemory("Alice"), Does.Contain(station.memory));
        State.ResetState(); Assert.That(actor.IsCompleted(station), Is.False); Assert.That(actor.CanHelp(station), Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator WitnessesAreNearByVerifiedDeduplicatedEventsNotChatClaims()
    {
#if UNITY_EDITOR
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<InteractionData>("Assets/Data/Interactions/Drawer_Data.asset");
#else
        InteractionData data = null;
#endif
        Assert.That(data, Is.Not.Null);
        string flag = actor.Scenario.Flag("observed.drawer_failed");
        PlayerAt(new Vector2(-6f, 1f)); ActorAt(new Vector2(6f, -2.7f));
        InteractionSignals.Publish(data, false); Assert.That(State.HasFlag(flag), Is.False);
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        string response; Assert.That(InteractionSystem.Instance.TryExecute(data, out response, "0000"), Is.False);
        Assert.That(State.HasFlag(flag), Is.True); int memories = State.GetNpcMemory("Alice").Count;
        InteractionSystem.Instance.TryExecute(data, out response, "0000"); Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(memories));
        Assert.That(State.HasFlag("found_note"), Is.False); Assert.That(State.HasFlag("drawer_opened"), Is.False);
        var context = NpcKnowledgeContextBuilder.Build("Alice", "Room01", State, false);
        Assert.That(context.ActiveNotes.Any(n => n.Contains("การพูดในแชทไม่ใช่")), Is.True);
        Assert.That(context.ToPromptSection(), Does.Not.Contain("4592")); yield return null;
    }
    [UnityTest]
    public IEnumerator ComfortOnlyRewardsOnceAndOtherRoomsHaveNoCompanionHud()
    {
        int relationship = State.GetRelationship("Alice"); PlayerAt(station.playerPoint);
        Assert.That(actor.RequestComfort(), Is.True); Assert.That(actor.RequestComfort(), Is.True);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship + 1));
        yield return SceneManager.LoadSceneAsync("Room02"); yield return null;
        Assert.That(UnityEngine.Object.FindObjectsOfType<NpcCompanionController>(), Is.Empty);
        Assert.That(UnityEngine.Object.FindObjectsOfType<CompanionHud>(), Is.Empty);
    }
}
