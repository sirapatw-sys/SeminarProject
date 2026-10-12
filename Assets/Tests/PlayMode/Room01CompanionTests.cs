using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class Room01CompanionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private class OfflineProvider : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null) { done(null); yield break; }
    }
    private class FakeCompanionProvider : IAiDialogueProvider
    {
        public int Calls;
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null)
        {
            Calls++;
            done(new GeneratedChatReply { reply = "เปิดกล่องแล้ว รหัสคือ 4592", relationshipDelta = 6, referencedFactIds = Array.Empty<string>() });
            yield break;
        }
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
    private ObjectInteraction Drawer()
    {
        return UnityEngine.Object.FindObjectsOfType<ObjectInteraction>().First(o =>
            ((InteractionData)typeof(ObjectInteraction).GetField("interactionData", Private).GetValue(o)).interactionId == "open_drawer");
    }
    private void Submit(string code)
    {
        typeof(KeypadLockUI).GetField("currentInput", Private).SetValue(KeypadLockUI.Instance, code);
        typeof(KeypadLockUI).GetMethod("TrySubmit", Private).Invoke(KeypadLockUI.Instance, null);
    }
    [UnityTest]
    public IEnumerator TypedActivityHelpUsesLiveAuthoredStateWithoutCommandsRewardsOrMainHints()
    {
#if UNITY_EDITOR
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset");
#else
        DialogueData data = null;
#endif
        Assert.That(data, Is.Not.Null); State.SetRelationship("Alice", 10);
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        foreach (var provider in new IAiDialogueProvider[] { new OfflineProvider(), new FakeCompanionProvider() })
        {
            DialogueProviders.Override = provider;
            DialogueManager.Instance.StartDialogue(data);
            var input = (TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
            input.text = "ช่วยเราเปิดกล่องสองมือหน่อย"; DialogueManager.Instance.SendTypedMessage();
            yield return null;
            string actual = State.GetConversationLog("Alice").Last().Text;
            Assert.That(actual, Does.Contain("ยังไม่สำเร็จ")); Assert.That(actual, Does.Contain("จุดสีฟ้า"));
            Assert.That(actual, Does.Not.Contain("ผนัง")); Assert.That(actual, Does.Not.Contain("4592"));
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(10)); Assert.That(State.GetJournal(), Is.Empty);
            if (provider is FakeCompanionProvider fake) Assert.That(fake.Calls, Is.Zero, "Authored activity help must not consume provider quota.");
            DialogueManager.Instance.HideDialogue();
        }
        BeginAtStation(); Tick(1f); float progress = actor.Progress;
        DialogueManager.Instance.StartDialogue(data);
        var chat = (TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        chat.text = "ช่วยฉันเปิดกล่องสองมือได้ไหม"; DialogueManager.Instance.SendTypedMessage(); yield return null;
        Assert.That(actor.Progress, Is.EqualTo(progress)); Assert.That(actor.ActiveStation, Is.SameAs(station));
        DialogueManager.Instance.HideDialogue(); Tick(2.2f); Assert.That(actor.IsCompleted(station), Is.True);
        DialogueManager.Instance.StartDialogue(data);
        chat.text = "กล่องสองมือเปิดแล้วหรือยัง"; DialogueManager.Instance.SendTypedMessage(); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ทำสำเร็จแล้ว"));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("จุดสีฟ้า"));
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(13));
    }

    [UnityTest]
    public IEnumerator FollowingRoutesAroundTheActualSofaWhenTheOldOffsetIsBlocked()
    {
        Vector2 start = new Vector2(1.337f, -1.037f), player = new Vector2(3.6f, -3.3f);
        ActorAt(start); PlayerAt(player);
        Assert.That(actor.IsFree(start), Is.True); Assert.That(actor.IsFree(player), Is.True);
        Assert.That(actor.IsFree(player + (start - player).normalized * actor.Scenario.followDistance), Is.False);
        Assert.That(actor.RequestFollow(), Is.True);
        var clear = typeof(NpcCompanionController).GetMethod("IsClear", Private);
        Vector2 previous = actor.Feet;
        for (int i = 0; i < 240; i++)
        {
            actor.Tick(0.05f); Physics2D.SyncTransforms();
            Assert.That(Vector2.Distance(previous, actor.Feet), Is.LessThanOrEqualTo(0.23f));
            Assert.That((bool)clear.Invoke(actor, new object[] { previous, actor.Feet }), Is.True, "No furniture clipping on the swept route.");
            Assert.That(actor.IsFree(actor.Feet), Is.True);
            Assert.That(Vector2.Distance(actor.Feet, player), Is.GreaterThanOrEqualTo(actor.Scenario.followDistance - 0.01f));
            previous = actor.Feet;
        }
        Assert.That(Vector2.Distance(actor.Feet, player), Is.LessThanOrEqualTo(actor.Scenario.followDistance + 0.21f));
        previous = actor.Feet; Tick(2f); Assert.That(actor.Feet, Is.EqualTo(previous));
        // Moving again must immediately invalidate the stopped route, not leave Alice waiting forever.
        PlayerAt(new Vector2(0.9f, -2.65f)); Tick(4f);
        Assert.That(Vector2.Distance(actor.Feet, actor.PlayerFeet), Is.LessThanOrEqualTo(actor.Scenario.followDistance + 0.21f));
        yield return null;
    }
    [UnityTest]
    public IEnumerator WrongKeypadCodeIsObservedThroughTheRealObjectRouteWithoutClosingTheKeypad()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        State.SetFlag("found_note"); Drawer().Interact(); Assert.That(KeypadLockUI.IsOpen, Is.True);
        Submit("0000");
        Assert.That(KeypadLockUI.IsOpen, Is.True); Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        Assert.That(State.HasFlag("drawer_opened"), Is.False);
        Assert.That(State.HasFlag(actor.Scenario.Flag("observed.drawer_failed")), Is.True);
        int memories = State.GetNpcMemory("Alice").Count;
        Submit("0000"); Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(memories));
        Submit("4592"); Assert.That(KeypadLockUI.IsOpen, Is.False);
        Assert.That(State.HasFlag("drawer_opened"), Is.True); Assert.That(State.HasItem("key"), Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator IncompleteInputCancelAndAlreadyOpenDrawerNeverInventAFailedAttempt()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        State.SetFlag("found_note"); Drawer().Interact(); Submit("00"); KeypadLockUI.Close();
        Assert.That(State.HasFlag(actor.Scenario.Flag("observed.drawer_failed")), Is.False);
        Drawer().Interact(); Submit("4592");
        Assert.That(State.HasFlag("drawer_opened"), Is.True);
        DialogueManager.Instance.HideDialogue(); ItemPopupUI.CancelAll();
        Drawer().Interact();
        Assert.That(State.HasFlag(actor.Scenario.Flag("observed.drawer_failed")), Is.False);
        Assert.That(State.GetNpcMemory("Alice"), Does.Not.Contain(actor.Scenario.observations.First(o => o.id == "drawer_failed").memory));
        yield return null;
    }
    [UnityTest]
    public IEnumerator KeypadCallbacksAreClearedOnCancelSuccessReplacementAndSceneChange()
    {
        int successes = 0, failures = 0;
        Action show = () => KeypadLockUI.Show("1234", onSuccess: () => successes++, onFailure: answer => { Assert.That(answer, Is.EqualTo("0000")); failures++; });
        show(); Submit("00"); Assert.That(failures, Is.Zero);
        Submit("0000"); Assert.That(failures, Is.EqualTo(1));
        KeypadLockUI.Close(); Submit("0000"); Assert.That(failures, Is.EqualTo(1));
        show(); KeypadLockUI.Show("1234"); Submit("0000"); Assert.That(failures, Is.EqualTo(1));
        show(); Submit("1234"); Assert.That(successes, Is.EqualTo(1)); Submit("0000");
        Assert.That(typeof(KeypadLockUI).GetField("onUnlockFailure", Private).GetValue(KeypadLockUI.Instance), Is.Null);
        show(); KeypadLockUI.Instance.enabled = false; Submit("0000");
        Assert.That(KeypadLockUI.IsOpen, Is.False); Assert.That(failures, Is.EqualTo(1));
        KeypadLockUI.Instance.enabled = true;
        show(); yield return SceneManager.LoadSceneAsync("Room02"); yield return null;
        Assert.That(KeypadLockUI.IsOpen, Is.False); Submit("0000");
        Assert.That(failures, Is.EqualTo(1)); Assert.That(successes, Is.EqualTo(1));
        Assert.That(typeof(KeypadLockUI).GetField("onUnlockFailure", Private).GetValue(KeypadLockUI.Instance), Is.Null);
    }
    [UnityTest]
    public IEnumerator AiCannotClaimAnUnfinishedActivitySucceededAndGetsLiveCanonAfterCompletion()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        var context = NpcKnowledgeContextBuilder.Build("Alice", "Room01", State, false);
        GeneratedChatReply grounded; string reason;
        Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
            reply = "ฉันเปิดกล่องสองมือให้แล้ว เราทำสำเร็จด้วยกัน", referencedFactIds = Array.Empty<string>()
        }, out grounded, out reason), Is.False);
        string following = actor.Scenario.Flag("mode.Following");
        Assert.That(context.CanReference(following), Is.False);
        actor.RequestFollow(); actor.RequestHelp(station); Tick(3.2f);
        context = NpcKnowledgeContextBuilder.Build("Alice", "Room01", State, false);
        Assert.That(context.CanReference(following), Is.True);
        string completed = actor.Scenario.CompletedFlag(station);
        Assert.That(NpcReplyPolicy.TryGroundReply(context, new GeneratedChatReply {
            reply = "{fact:" + completed + "}", referencedFactIds = new[] { completed }
        }, out grounded, out reason), Is.True, reason);
        Assert.That(grounded.reply, Does.Contain("ทำสำเร็จแล้ว")); yield return null;
    }
    [UnityTest]
    public IEnumerator RepeatedHelpPreservesProgressTimerAndOriginalFollowIntent()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint); actor.RequestFollow();
        Assert.That(actor.RequestHelp(station), Is.True); Assert.That(actor.RequestHelp(station), Is.True);
        Tick(1f); float progress = actor.Progress; string bubble = actor.Bubble;
        Assert.That(progress, Is.GreaterThan(0f));
        Assert.That(actor.RequestHelp(station), Is.True);
        Assert.That(actor.Progress, Is.EqualTo(progress)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Helping));
        Assert.That(actor.Bubble, Is.EqualTo(bubble));
        Tick(2.2f); Assert.That(actor.IsCompleted(station), Is.True);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        State.ResetState(); BeginAtStation(); PlayerAt(station.playerPoint + Vector2.down);
        for (int i = 0; i < 100 && actor.ActiveStation != null; i++)
        { Assert.That(actor.RequestHelp(station), Is.True); Tick(0.5f); }
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(actor.IsCompleted(station), Is.False); yield return null;
    }
    [UnityTest]
    public IEnumerator ExplicitWaitClearsThePreviousFollowIntent()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint); actor.RequestFollow();
        actor.RequestHelp(station); Tick(0.5f); Assert.That(actor.RequestWait(), Is.True);
        actor.RequestHelp(station); Tick(3.2f);
        Assert.That(actor.IsCompleted(station), Is.True); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        yield return null;
    }
    [UnityTest]
    public IEnumerator SwitchingToAnotherActivityPreservesTheOriginalFollowIntent()
    {
        var original = actor.Scenario;
        var scenario = UnityEngine.Object.Instantiate(original);
        var second = new CompanionStationData {
            id = "second_station", title = "Second activity", radius = station.radius,
            holdSeconds = station.holdSeconds, companionPoint = station.companionPoint + Vector2.up * 0.6f,
            playerPoint = station.playerPoint + Vector2.up * 0.6f, memory = "Second activity completed", journalText = "Second activity completed"
        };
        scenario.stations.Add(second);
        try
        {
            actor.Configure(scenario); station = scenario.stations[0];
            PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
            Assert.That(actor.RequestFollow(), Is.True); Assert.That(actor.RequestHelp(station), Is.True); Tick(0.5f);
            Assert.That(actor.RequestHelp(second), Is.True);
            Assert.That(actor.Progress, Is.Zero); Assert.That(actor.ActiveStation, Is.SameAs(second));
            ActorAt(second.companionPoint); PlayerAt(second.playerPoint); Tick(3.2f);
            Assert.That(actor.IsCompleted(second), Is.True); Assert.That(actor.IsCompleted(station), Is.False);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        }
        finally { actor.Configure(original); UnityEngine.Object.DestroyImmediate(scenario); }
        yield return null;
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
