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

public class CompanionWithdrawalOrderingPlayTests
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
    [UnityTest]
    public IEnumerator CancelThenUnsupportedTaskMustStopOldBoxBeforeItCompletes()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        int before = State.GetRelationship("Alice");
        provider.ProposedAction = "help:two_hands_box";
        Send("ยกเลิก, ช่วยยกเก้าอี้หน่อย"); yield return null;
        DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 80; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Debug.Log("ORDER_REVIEW unsupported after cancel: completed=" + actor.IsCompleted(Box) + "; reward=" + (State.GetRelationship("Alice") - before));
        Assert.That(actor.IsCompleted(Box), Is.False);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(before));
    }
    [UnityTest]
    public IEnumerator CancelThenLockedTaskMustStopOldFollowing()
    {
        Assert.That(actor.RequestFollow(), Is.True); Chat();
        Assert.That(ConditionRule.AllHold(Desk.conditions, State), Is.False);
        Send("ยกเลิก, ช่วยจับขอบโต๊ะหน่อย"); yield return null;
        Debug.Log("ORDER_REVIEW locked after cancel: mode=" + actor.Mode + "; AI calls=" + provider.Calls);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(actor.ActiveStation, Is.Null);
    }
    [UnityTest]
    public IEnumerator CancelThenConversationMustStopOldBoxBeforeItCompletes()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        int before = State.GetRelationship("Alice");
        Send("ยกเลิก, เธอสบายดีไหม"); yield return null;
        DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 80; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Debug.Log("ORDER_REVIEW social after cancel: completed=" + actor.IsCompleted(Box) + "; reward=" + (State.GetRelationship("Alice") - before));
        Assert.That(actor.IsCompleted(Box), Is.False);
    }
    [UnityTest]
    public IEnumerator CancelThenValidTaskControlStillChangesTheOldTask()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Send("ยกเลิก, ตามมา"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(actor.ActiveStation, Is.Null);
    }
    [UnityTest]
    public IEnumerator FinalCancellationControlStillPreventsCompletion()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Send("ช่วยเปิดกล่องหน่อย, ยกเลิก"); yield return null;
        DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 80; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Assert.That(actor.IsCompleted(Box), Is.False);
    }
    [UnityTest]
    public IEnumerator OfflineCancellationStillStopsWorkBeforeAnUnsupportedRequest()
    {
        provider.Available = false; Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Send("ยกเลิก, ช่วยยกเก้าอี้หน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(provider.Calls, Is.Zero); DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 80; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Assert.That(actor.IsCompleted(Box), Is.False);
    }
    [UnityTest]
    public IEnumerator CancellationHappensBeforeASlowProviderAnswers()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        provider.HoldReply = true;
        Send("ยกเลิก, ช่วยยกเก้าอี้หน่อย");
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), "Cancellation is synchronous with player send, not the callback.");
        Assert.That(actor.ActiveStation, Is.Null); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 80; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Assert.That(actor.IsCompleted(Box), Is.False);
    }
    [UnityTest]
    public IEnumerator CancellationDoesNotSwallowALaterEvidenceShare()
    {
        State.SetFlag("found_note"); Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        Send("ยกเลิก, เอาโน้ตให้ดู"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
        Assert.That(actor.IsCompleted(Box), Is.False); Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator CancelledSearchDoesNotGrantEvidenceWhileANewTaskIsRefused()
    {
        Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Searching));
        Send("ยกเลิก, ช่วยจับขอบโต๊ะหน่อย"); yield return null; DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 800; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.GetJournal(), Is.Empty);
    }
    [UnityTest]
    public IEnumerator NonDirectAndNegatedCancellationsCannotStopExistingWork()
    {
        Assert.That(actor.RequestFollow(), Is.True); Chat();
        foreach (string message in new[] { "ยกเลิก, ใช่ไหม", "อย่า ยกเลิก", "what is cancel that", "เขาบอกว่า ยกเลิก, ช่วยยกเก้าอี้หน่อย", "ถ้าฉันบอกว่า ยกเลิก, ช่วยยกเก้าอี้หน่อย", "\"ยกเลิก\", ช่วยยกเก้าอี้หน่อย" })
        {
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), message);
        }
    }
    [UnityTest]
    public IEnumerator InlineWithdrawalCanStillStartALaterModelRequest()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box)); provider.ProposedAction = "follow";
        Send("ช่วยเปิดกล่อง ยกเลิก, Alice walk beside me please"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.IsCompleted(Box), Is.False);
    }
}
