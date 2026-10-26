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

public class CompanionCommandSafetyPlayTests
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
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        { Calls++; done(new GeneratedChatReply { reply = "อืม", referencedFactIds = Array.Empty<string>(), companionAction = ProposedAction }); yield break; }
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
    IEnumerator RejectFollow(string message)
    {
        provider.ProposedAction = "follow"; Chat(); Send(message); yield return null;
        Debug.Log("REVIEW movement: " + message + "; calls=" + provider.Calls + "; mode=" + actor.Mode);
        Assert.That(actor.Mode, Is.Not.EqualTo(CompanionMode.Following), "A destination request must not silently become follow-player.");
    }
    [UnityTest] public IEnumerator DoorDestinationMustNotFollowPlayer() { yield return RejectFollow("เดินไปที่ประตูหน่อย"); }
    [UnityTest] public IEnumerator AwayDestinationMustNotFollowPlayer() { yield return RejectFollow("เดินออกไปห่าง ๆ หน่อย"); }
    [UnityTest] public IEnumerator EnglishDoorDestinationMustNotFollowPlayer() { yield return RejectFollow("walk to the door please"); }
    [UnityTest] public IEnumerator DifferentTaskMustNotFollowPlayer() { yield return RejectFollow("come help me lift the chair"); }
    [UnityTest]
    public IEnumerator CompoundWithdrawalCancelsActiveFollowing()
    {
        Chat(); Send("ตามมา"); yield return null; Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Send("ตามมา, ยกเลิก"); yield return null;
        Debug.Log("REVIEW withdrawn following: " + actor.Mode);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), "Final withdrawal must stop the active repeated request.");
    }
    [UnityTest]
    public IEnumerator CompoundWithdrawalCancelsActiveDeskHelp()
    {
        State.SetFlag("inspected_painting"); ActorAt(Desk.companionPoint); PlayerAt(Desk.playerPoint);
        Assert.That(actor.RequestHelp(Desk), Is.True); actor.Tick(0.05f); Assert.That(actor.ActiveStation, Is.SameAs(Desk));
        Chat(); Send("ช่วยจับขอบโต๊ะ, ยกเลิก"); yield return null;
        Debug.Log("REVIEW withdrawn help: " + actor.Mode + "; station=" + (actor.ActiveStation == null ? "none" : actor.ActiveStation.id));
        Assert.That(actor.ActiveStation, Is.Null, "A withdrawn active hold must no longer be eligible to complete.");
    }
    [UnityTest]
    public IEnumerator UnknownSecondObjectMustNotStartDeskHelp()
    {
        State.SetFlag("inspected_painting"); ActorAt(Desk.companionPoint); PlayerAt(Desk.playerPoint);
        Chat(); Send("ช่วยจับขอบโต๊ะกับตู้หนังสือหน่อย"); yield return null;
        Debug.Log("REVIEW mixed help: " + actor.Mode + "; AI calls=" + provider.Calls);
        Assert.That(actor.ActiveStation, Is.Null, "Two objects cannot be silently reduced to the recognized desk.");
    }
    [UnityTest]
    public IEnumerator StandaloneWithdrawalControlStopsFollowing()
    {
        Chat(); Send("ตามมา"); yield return null; Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Send("ยกเลิก"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
    }
    [UnityTest]
    public IEnumerator CancelledBoxMustNotCompleteAfterClosingChat()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        int initialRelationship = State.GetRelationship("Alice");
        Send("ช่วยเปิดกล่องหน่อย, ยกเลิก"); yield return null;
        DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 80; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Debug.Log("REVIEW cancelled box completed=" + actor.IsCompleted(Box) + "; relationship delta=" + (State.GetRelationship("Alice") - initialRelationship));
        Assert.That(actor.IsCompleted(Box), Is.False, "Withdrawal must prevent completion and rewards of the active request.");
    }
    [UnityTest]
    public IEnumerator StandaloneCancelledBoxControlDoesNotComplete()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        int initialRelationship = State.GetRelationship("Alice");
        Send("ยกเลิก"); yield return null;
        DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 80; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Assert.That(actor.IsCompleted(Box), Is.False); Assert.That(State.GetRelationship("Alice"), Is.EqualTo(initialRelationship));
    }
    [UnityTest]
    public IEnumerator OrdinaryDeskRequestControlStartsHelp()
    {
        State.SetFlag("inspected_painting"); ActorAt(Desk.companionPoint); PlayerAt(Desk.playerPoint);
        Chat(); Send("ช่วยจับขอบโต๊ะหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Desk)); Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator FinalWithdrawalsStopFollowingWithoutMovingAfterChatCloses()
    {
        foreach (string message in new[] { "ตามมา ยกเลิก", "ตามมา, ไม่ต้องแล้ว", "follow me, scratch that", "follow me\nnever mind", "เธอตามมาใช่ไหม, ยกเลิก" })
        {
            DialogueManager.Instance.HideDialogue(); State.ResetState(); ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint);
            Assert.That(actor.RequestFollow(), Is.True); Chat(); int calls = provider.Calls;
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message);
            Assert.That(provider.Calls, Is.EqualTo(calls), "Direct cancellation must not depend on a provider.");
            DialogueManager.Instance.HideDialogue(); Vector2 before = actor.Feet; PlayerAt(new Vector2(2f, -3.4f));
            for (int i = 0; i < 60; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
            Assert.That(actor.Feet, Is.EqualTo(before), message);
        }
    }
    [UnityTest]
    public IEnumerator FinalWithdrawalStopsSearchBeforeItCanGrantEvidence()
    {
        Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Searching));
        Send("ช่วยหาหลักฐานหน่อย, ยกเลิก"); yield return null; DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 800; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); }
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.GetJournal(), Is.Empty);
    }
    [UnityTest]
    public IEnumerator ReportedHypotheticalAndConfirmedWithdrawalsDoNotCancelWork()
    {
        Assert.That(actor.RequestFollow(), Is.True); Chat();
        foreach (string message in new[] { "เขาบอกว่า ตามมา, ยกเลิก", "ถ้าฉันบอกว่า ตามมา, ยกเลิก", "\"ตามมา, ยกเลิก\"", "ตามมา, ยกเลิก, ใช่ไหม", "ยกเลิกคืออะไร" })
        {
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), message);
        }
    }
    [UnityTest]
    public IEnumerator LaterNewRequestReplacesCancelledWork()
    {
        Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null; Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Send("ยกเลิก, ตามมา"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(actor.ActiveStation, Is.Null);
        provider.ProposedAction = "follow";
        Send("ตามมา, ยกเลิก, walk beside me please"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(actor.ActiveStation, Is.Null);
    }
    [UnityTest]
    public IEnumerator UnsupportedWaitDestinationsAskBackWithoutInterruptingFollowing()
    {
        Assert.That(actor.RequestFollow(), Is.True); Chat(); provider.ProposedAction = "wait";
        foreach (string message in new[] { "wait by the door please", "ยืนที่ประตูหน่อย", "รอตรงนี้ที่ประตู" })
        {
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), message); Assert.That(actor.ActiveStation, Is.Null);
        }
    }
    [UnityTest]
    public IEnumerator AmbiguousNativeAndModelHelpCannotInterruptFollowing()
    {
        State.SetFlag("inspected_painting"); Assert.That(actor.RequestFollow(), Is.True); Chat(); provider.ProposedAction = "help:desk_support";
        foreach (string message in new[] { "ช่วยจับขอบโต๊ะกับตู้หนังสือหน่อย", "ไปช่วยตรงโต๊ะกับโคมไฟหน่อยสิ" })
        {
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), message); Assert.That(actor.ActiveStation, Is.Null);
        }
    }
}
