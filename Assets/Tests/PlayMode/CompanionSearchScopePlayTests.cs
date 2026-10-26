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

public class CompanionSearchScopePlayTests
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
    void Chat()
    {
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", Private).GetValue(actor.GetComponent<NPCInteraction>());
        DialogueManager.Instance.StartDialogue(data);
        Assert.That(DialogueManager.IsDialogueOpen, Is.True);
    }
    void Send(string text)
    { ((TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance)).text = text; DialogueManager.Instance.SendTypedMessage(); }
    void ResetForRequest()
    {
        DialogueManager.Instance.HideDialogue(); State.ResetState(); ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint); Chat();
    }
    void Tick(float seconds)
    { for (int i = 0; i < seconds / 0.05f; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); } }
    [UnityTest]
    public IEnumerator NativeRestrictedSearchDoesNotMoveOrGrantEvidence()
    {
        foreach (string message in new[] { "ช่วยหาหลักฐานในลิ้นชักเท่านั้น", "ช่วยหาหลักฐานตรงโต๊ะหน่อย", "ช่วยหาหลักฐานในกล่องสองมือหน่อย", "ช่วยหาหลักฐานแล้วช่วยยกเก้าอี้หน่อย" })
        {
            ResetForRequest(); int calls = provider.Calls; int relationship = State.GetRelationship("Alice");
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message); Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(provider.Calls, Is.EqualTo(calls), "Known unsupported search scope is resolved locally.");
            Assert.That(State.GetConversationLog("Alice").Last().Text, Is.EqualTo(actor.Scenario.searchScopeLine));
            DialogueManager.Instance.HideDialogue(); Vector2 before = actor.Feet; Tick(40f);
            Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(State.GetJournal(), Is.Empty);
            Assert.That(State.HasFlag(actor.Scenario.SearchFlag("companion_box_marks")), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship)); Assert.That(actor.IsCompleted(Box), Is.False);
        }
    }
    [UnityTest]
    public IEnumerator ModelSearchCannotSilentlyBecomeHoldingOrSearchADifferentObject()
    {
        provider.ProposedAction = "search";
        foreach (string message in new[] { "look in the drawer please", "inspect the desk please", "ลองดูตรงกล่องหน่อย", "ลองดูตรงกล่องสองมือหน่อย" })
        {
            ResetForRequest(); State.SetFlag("inspected_painting"); int calls = provider.Calls;
            Send(message); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(calls + 1), "Exercise provider interpretation: " + message);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message); Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(State.GetConversationLog("Alice").Last().Text, Is.EqualTo(actor.Scenario.searchScopeLine), message);
            DialogueManager.Instance.HideDialogue(); Tick(40f); Assert.That(State.GetJournal(), Is.Empty);
            Assert.That(State.HasFlag(actor.Scenario.SearchFlag("companion_box_marks")), Is.False);
        }
    }
    [UnityTest]
    public IEnumerator RefusedSearchScopeDoesNotInterruptExistingFollowing()
    {
        provider.ProposedAction = "search";
        foreach (string message in new[] { "ช่วยหาหลักฐานในลิ้นชักเท่านั้น", "inspect the desk please" })
        {
            ResetForRequest(); DialogueManager.Instance.HideDialogue(); Assert.That(actor.RequestFollow(), Is.True); Chat();
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), message); Assert.That(actor.ActiveStation, Is.Null);
        }
    }
    [UnityTest]
    public IEnumerator PlayerCanExplicitlyConfirmWholeRoomSearchAfterAskBack()
    {
        Chat(); Send("ช่วยหาหลักฐานในลิ้นชักเท่านั้น"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Send("ช่วยค้นห้องหน่อย"); yield return null; Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Searching));
        DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("companion_box_marks")), Is.True);
        Assert.That(State.HasFlag("found_note"), Is.False); Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator WholeRoomModelSearchStillStartsTheAuthoredRoute()
    {
        Chat(); provider.ProposedAction = "search"; Send("ลองดูรอบๆ ห้องให้ที"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Searching));
        DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("companion_box_marks")), Is.True);
    }
    [UnityTest]
    public IEnumerator MisclassifiedSearchForAnActualHelpRequestStillUsesTheNamedStation()
    {
        State.SetFlag("inspected_painting"); Chat(); provider.ProposedAction = "search";
        Send("ไปช่วยตรงโต๊ะหน่อยสิ"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.ActiveStation, Is.SameAs(Desk));
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.GoingToHelp)); Assert.That(State.HasFlag("found_note"), Is.False);
    }
    [UnityTest]
    public IEnumerator SocialRequestsRemainConversationEvenWhenModelProposesSearch()
    {
        Chat(); provider.ProposedAction = "search";
        foreach (string message in new[] { "ช่วยหาเรื่องคุยหน่อย", "ช่วยหาน้ำหน่อย" })
        {
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message);
            Assert.That(State.GetConversationLog("Alice").Last().Text, Is.Not.EqualTo(actor.Scenario.searchScopeLine));
        }
    }
    [UnityTest]
    public IEnumerator RefusalsAndNonDirectSearchSpeechStillCannotStartSearch()
    {
        Chat(); provider.ProposedAction = "search";
        foreach (string message in new[] { "อย่าช่วยหาหลักฐาน", "ถ้าฉันบอกว่า ช่วยหาหลักฐาน", "\"ช่วยหาหลักฐาน\"", "เธอหาหลักฐานแล้วใช่ไหม" })
        {
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message); Assert.That(actor.ActiveStation, Is.Null);
        }
    }
}
