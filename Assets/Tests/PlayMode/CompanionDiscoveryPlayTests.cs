using System;
using System.Collections;
using System.Reflection;
using System.Linq;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CompanionDiscoveryPlayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    NpcCompanionController actor;
    bool persistence, title, intro;
    Offline provider;
    GameState State { get { return GameState.Instance; } }
    CompanionStationData Box { get { return actor.Scenario.stations[0]; } }
    CompanionStationData Desk { get { return actor.Scenario.stations[1]; } }
    class Offline : IAiDialogueProvider
    {
        public int Calls;
        public string ProposedAction;
        public bool Available = true;
        public bool HoldReply;
        public bool CanGenerate { get { return Available; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        { Calls++; while (HoldReply) yield return null; done(new GeneratedChatReply { reply = "อืม", referencedFactIds = Array.Empty<string>(), companionAction = ProposedAction }); yield break; }
    }
    static void Title(bool value) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, value); }
    static void Intro(bool value) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, value); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        provider = new Offline(); DialogueProviders.Override = provider; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState(); actor = NpcCompanionController.Find("Alice", "Room01"); Assert.That(actor, Is.Not.Null);
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint);
    }
    [TearDown]
    public void Cleanup()
    {
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = persistence; Title(title); Intro(intro);
    }
    void PlayerAt(Vector2 point) { GameObject.FindGameObjectWithTag("Player").transform.position += (Vector3)(point - actor.PlayerFeet); Physics2D.SyncTransforms(); }
    void ActorAt(Vector2 point) { actor.transform.position += (Vector3)(point - actor.Feet); Physics2D.SyncTransforms(); }
    void Chat(bool isEvent = false)
    {
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", Private).GetValue(actor.GetComponent<NPCInteraction>());
        DialogueManager.Instance.StartDialogue(data, null, isEvent: isEvent);
        Assert.That(DialogueManager.IsDialogueOpen, Is.True);
    }
    void Send(string text)
    { ((TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance)).text = text; DialogueManager.Instance.SendTypedMessage(); }
    void Tick(float seconds)
    { for (int i = 0; i < seconds / 0.05f; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); } }
    void ClearBubble() { typeof(NpcCompanionController).GetField("bubbleSeconds", Private).SetValue(actor, 0f); }
    void SeedBoxObservation()
    { State.SetFlag(actor.Scenario.SearchFlag("companion_box_marks")); State.SetFlag(NpcObservation.Flag("Alice", "Room01", "companion_box_marks")); }
    [UnityTest]
    public IEnumerator RealSearchInvitesWithoutOpeningChatAndReportsWhenPlayerReturns()
    {
        Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("companion_box_marks")), Is.True);
        Assert.That(actor.HasDiscoveryInvitation, Is.True); Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        ClearBubble(); Assert.That(actor.GetComponent<CompanionHud>().DiscoveryIndicatorVisible, Is.True);
        PlayerAt(actor.Feet + Vector2.right * 0.8f); int score = State.GetRelationship("Alice"); Chat();
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(KnowledgeLibrary.GetRoom("Room01").FindFact("companion_box_marks").statement));
        Assert.That(actor.HasDiscoveryInvitation, Is.False);
        Assert.That(State.HasFlag(CompanionDiscovery.ReportedFlag(actor.Scenario, "companion_box_marks")), Is.True);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score)); Assert.That(provider.Calls, Is.Zero);
        Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
    }
    [UnityTest]
    public IEnumerator CancelledSearchCannotInventANewDiscoveryInvitation()
    {
        Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null; Send("หยุดเดิน"); yield return null;
        DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(actor.HasDiscoveryInvitation, Is.False); Assert.That(State.GetJournal(), Is.Empty);
    }
    [UnityTest]
    public IEnumerator SaveRestoreKeepsUnreadDiscoveriesAndReadReceipts()
    {
        SeedBoxObservation(); var unread = State.CreateSnapshot(); State.ResetState(); State.RestoreSnapshot(unread);
        Assert.That(actor.HasDiscoveryInvitation, Is.True); Chat();
        Assert.That(actor.HasDiscoveryInvitation, Is.False); var read = State.CreateSnapshot();
        DialogueManager.Instance.HideDialogue(); State.ResetState(); State.RestoreSnapshot(read);
        Assert.That(actor.HasDiscoveryInvitation, Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator TypedReportAcknowledgesOnlyTheFactsActuallyTold()
    {
        SeedBoxObservation(); Chat(true); int score = State.GetRelationship("Alice");
        Assert.That(actor.HasDiscoveryInvitation, Is.True, "Opening an event must not consume the discovery report.");
        Send("เธอเจออะไรบ้าง"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(KnowledgeLibrary.GetRoom("Room01").FindFact("companion_box_marks").statement));
        Assert.That(actor.HasDiscoveryInvitation, Is.False); Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator RepeatedSearchDoesNotRepeatTheInvitationOrJournalReward()
    {
        SeedBoxObservation(); Chat(); Assert.That(actor.HasDiscoveryInvitation, Is.False);
        Send("ช่วยหาหลักฐานหน่อย"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(actor.HasDiscoveryInvitation, Is.False);
        int entries = State.GetJournal().Count, score = State.GetRelationship("Alice");
        PlayerAt(actor.Feet + Vector2.right * 0.8f); Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null;
        DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(actor.HasDiscoveryInvitation, Is.False); Assert.That(State.GetJournal(), Has.Count.EqualTo(entries));
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
    }
    [UnityTest]
    public IEnumerator HiddenAndOutOfRangeInvitationsCannotBeConsumed()
    {
        SeedBoxObservation(); ClearBubble(); var hud = actor.GetComponent<CompanionHud>();
        Assert.That(hud.DiscoveryIndicatorVisible, Is.True);
        Title(true); Assert.That(hud.DiscoveryIndicatorVisible, Is.False); Assert.That(actor.ReadDiscoveryReport(), Is.Null); Title(false);
        State.SetCurrentScene("Room02"); Assert.That(hud.DiscoveryIndicatorVisible, Is.False); State.SetCurrentScene("Room01");
        PlayerAt(new Vector2(5f, -3.4f)); Assert.That(actor.ReadDiscoveryReport(), Is.Null);
        Assert.That(actor.HasDiscoveryInvitation, Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator RealDeskCooperationReportsObservedArrowWithoutUnsharedDigits()
    {
        State.SetFlag("inspected_painting"); ActorAt(Desk.companionPoint); PlayerAt(Desk.playerPoint);
        Chat(); Send("ช่วยจับขอบโต๊ะหน่อย"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(0.2f);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Helping));
        var desk = UnityEngine.Object.FindObjectsOfType<ObjectInteraction>().First(o =>
            ((InteractionData)typeof(ObjectInteraction).GetField("interactionData", Private).GetValue(o)).interactionId == "inspect_desk");
        desk.Interact(); yield return new WaitForSeconds(2.3f); RoomTransitionManager.CloseRoomOverlays();
        Assert.That(actor.HasDiscoveryInvitation, Is.True); Chat();
        string text = State.GetConversationLog("Alice").Last().Text;
        Assert.That(text, Does.Contain(KnowledgeLibrary.GetRoom("Room01").FindFact("note_direction").statement));
        Assert.That(text, Does.Not.Contain("2 · 9 · 5 · 4")); Assert.That(text, Does.Not.Contain("4592"));
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("desk_note", State), Is.False);
    }
    [UnityTest]
    public IEnumerator StoryEventsKeepPriorityAndDoNotConsumeTheReport()
    {
        SeedBoxObservation(); ClearBubble(); var events = actor.GetComponent<NpcEventController>(); events.enabled = true;
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", Private).GetValue(actor.GetComponent<NPCInteraction>());
        var beat = ScriptableObject.CreateInstance<MiniEventData>();
        try
        {
            beat.eventId = "discovery_priority_test"; beat.npcId = "Alice"; beat.dialogue = data;
            beat.triggerType = MiniEventTriggerType.RandomAmbient; beat.storyBeat = true; beat.useAiDialogue = false;
            typeof(NpcEventController).GetField("pendingEvent", Private).SetValue(events, beat);
            typeof(NpcEventController).GetField("pendingUntil", Private).SetValue(events, Time.time + 1000f);
            Assert.That(actor.GetComponent<CompanionHud>().DiscoveryIndicatorVisible, Is.False);
            actor.GetComponent<NPCInteraction>().Interact();
            Assert.That(DialogueManager.IsDialogueOpen, Is.True); Assert.That(actor.HasDiscoveryInvitation, Is.True);
            DialogueManager.Instance.HideDialogue(); events.enabled = false; Chat();
            Assert.That(actor.HasDiscoveryInvitation, Is.False);
        }
        finally { events.enabled = false; UnityEngine.Object.DestroyImmediate(beat); }
        yield return null;
    }
    [UnityTest]
    public IEnumerator PlayerClaimsDoNotCreateAnInvitationOrEvidence()
    {
        Chat(); Send("เราเจอลูกศรแล้ว"); yield return null;
        Assert.That(actor.HasDiscoveryInvitation, Is.False);
        Assert.That(State.HasFlag(NpcObservation.Flag("Alice", "Room01", "note_direction")), Is.False);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("note_direction")), Is.False);
    }
}
