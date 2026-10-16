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

public class CompanionClarificationPlayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    NpcCompanionController actor;
    bool persistence, title, intro;
    Provider provider;
    GameState State => GameState.Instance;
    CompanionStationData Desk => actor.Scenario.stations.First(s => s.id == "desk_support");
    class Provider : IAiDialogueProvider
    {
        public bool Available;
        public int Calls;
        public string Action;
        public bool CanGenerate => Available;
        public string LastError => "";
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null)
        { Calls++; done(new GeneratedChatReply { reply = "อืม", companionAction = Action, referencedFactIds = Array.Empty<string>() }); yield break; }
    }
    static void Title(bool v) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, v); }
    static void Intro(bool v) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, v); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        provider = new Provider(); DialogueProviders.Override = provider;
        GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState(); actor = NpcCompanionController.Find("Alice", "Room01");
        State.SetFlag("inspected_painting"); PositionForQuestion();
    }
    [TearDown]
    public void Cleanup()
    {
        CompanionPlaytestTrace.End();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = persistence; Title(title); Intro(intro);
    }
    void PlayerAt(Vector2 p) { GameObject.FindGameObjectWithTag("Player").transform.position += (Vector3)(p - actor.PlayerFeet); Physics2D.SyncTransforms(); }
    void ActorAt(Vector2 p) { actor.transform.position += (Vector3)(p - actor.Feet); Physics2D.SyncTransforms(); }
    void PositionForQuestion() { ActorAt(new Vector2(2.3f, -3f)); PlayerAt(new Vector2(3f, -3f)); }
    void Chat()
    {
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", Private).GetValue(actor.GetComponent<NPCInteraction>());
        DialogueManager.Instance.StartDialogue(data); Assert.That(DialogueManager.IsDialogueOpen, Is.True);
    }
    void Send(string text)
    { ((TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance)).text = text; DialogueManager.Instance.SendTypedMessage(); }
    void Tick(float seconds) { for (int i = 0; i < seconds / 0.05f; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); } }

    [UnityTest]
    public IEnumerator MissingTargetThenShortAnswerStartsRealHelpOnlyOnce()
    {
        provider.Available = true; Chat(); int score = State.GetRelationship("Alice");
        Send("ช่วยจับให้หน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Send("ขอบโต๊ะ"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Desk));
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.GoingToHelp));
        Assert.That(State.HasFlag("found_note"), Is.False);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score)); Assert.That(provider.Calls, Is.Zero);
        int revision = actor.TaskRevision; Send("ขอบโต๊ะ"); yield return null;
        Assert.That(actor.TaskRevision, Is.EqualTo(revision));
        DialogueManager.Instance.HideDialogue(); Tick(10f);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Helping));
        PlayerAt(Desk.playerPoint);
        var desk = UnityEngine.Object.FindObjectsOfType<ObjectInteraction>().First(o =>
            ((InteractionData)typeof(ObjectInteraction).GetField("interactionData", Private).GetValue(o)).interactionId == "inspect_desk");
        desk.Interact(); yield return new WaitForSeconds(2.3f); RoomTransitionManager.CloseRoomOverlays();
        Assert.That(State.HasFlag("found_note"), Is.True); Assert.That(actor.IsCompleted(Desk), Is.True);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score + Desk.relationshipReward));
    }
    [UnityTest]
    public IEnumerator ANameWithoutAQuestionCannotBecomeAModelHelpCommand()
    {
        provider.Available = true; Chat();
        foreach (string action in new[] { "help:desk_support", "search" })
        {
            provider.Action = action; Send("ขอบโต๊ะ"); yield return null;
            Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), action);
        }
    }
    [UnityTest]
    public IEnumerator OfflineClarificationNeedsNoProvider()
    {
        Chat(); Send("ช่วยจับให้หน่อย"); yield return null; Send("โต๊ะหน่อยค่ะ"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Desk)); Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator RefusalAmbiguityAndUnrelatedTalkExpireTheQuestion()
    {
        foreach (string answer in new[] { "ไม่ต้องแล้ว", "ไม่ใช่โต๊ะ", "ขอบโต๊ะกับกล่อง", "เธอคิดถึงบ้านไหม", "ประตู", "ถ้าเป็นโต๊ะ", "โต๊ะใช่ไหม" })
        {
            DialogueManager.Instance.HideDialogue(); State.ResetState(); State.SetFlag("inspected_painting"); PositionForQuestion(); Chat();
            Send("ช่วยจับให้หน่อย"); yield return null; Send(answer); yield return null; Send("ขอบโต๊ะ"); yield return null;
            Assert.That(actor.ActiveStation, Is.Null, answer); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), answer);
        }
    }
    [UnityTest]
    public IEnumerator CloseResetRestoreAndRoomChangeDiscardTheQuestion()
    {
        foreach (string change in new[] { "close", "reset", "restore", "room" })
        {
            DialogueManager.Instance.HideDialogue(); State.ResetState(); State.SetFlag("inspected_painting"); PositionForQuestion(); Chat();
            Send("ช่วยจับให้หน่อย"); yield return null;
            if (change == "close") { DialogueManager.Instance.HideDialogue(); Chat(); }
            if (change == "reset") State.ResetState();
            if (change == "restore") State.RestoreSnapshot(State.CreateSnapshot());
            if (change == "room") { State.SetCurrentScene("Room02"); Send("ขอบโต๊ะ"); yield return null; State.SetCurrentScene("Room01"); }
            State.SetFlag("inspected_painting"); Send("ขอบโต๊ะ"); yield return null;
            Assert.That(actor.ActiveStation, Is.Null, change);
        }
    }
    [UnityTest]
    public IEnumerator ChangedConditionsAndRangeAreRevalidated()
    {
        Chat(); Send("ช่วยจับให้หน่อย"); yield return null; State.SetFlag("found_note");
        Send("ขอบโต๊ะ"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        DialogueManager.Instance.HideDialogue(); State.ResetState(); State.SetFlag("inspected_painting"); PositionForQuestion(); Chat();
        Send("ช่วยจับให้หน่อย"); yield return null; PlayerAt(new Vector2(-6f, -3f));
        Send("ขอบโต๊ะ"); yield return null; PositionForQuestion(); Send("ขอบโต๊ะ"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null);
    }
    [UnityTest]
    public IEnumerator TraceRecordsOneTaskEndForCancellationAndNoPlayerText()
    {
        string folder = System.IO.Path.Combine(Application.temporaryCachePath, "CompanionTraceTests", Guid.NewGuid().ToString("N"));
        try
        {
            Assert.That(CompanionPlaytestTrace.Begin(folder), Is.True);
            Chat(); Send("ช่วยจับให้หน่อย"); yield return null; Send("กล่อง"); yield return null;
            Assert.That(actor.ActiveStation, Is.Not.Null);
            Send("ยกเลิก"); yield return null; Send("ยกเลิก"); yield return null;
            CompanionPlaytestTrace.End();
            string[] lines = System.IO.File.ReadAllLines(CompanionPlaytestTrace.LastPath);
            Assert.That(lines.Count(l => l.Contains("\"kind\":\"action_start\"")), Is.EqualTo(1));
            Assert.That(lines.Count(l => l.Contains("\"kind\":\"action_end\"")), Is.EqualTo(1));
            Assert.That(lines.Any(l => l.Contains("ช่วยจับให้หน่อย") || l.Contains("ยกเลิก")), Is.False);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        }
        finally
        {
            CompanionPlaytestTrace.End();
            if (System.IO.Directory.Exists(folder))
            { foreach (string file in System.IO.Directory.GetFiles(folder)) System.IO.File.Delete(file); System.IO.Directory.Delete(folder); }
        }
    }
    [UnityTest]
    public IEnumerator ExplicitNewCommandReplacesQuestionAndSearchScopeDoesNotCreateOne()
    {
        Chat(); Send("ช่วยจับให้หน่อย"); yield return null; Send("ตามมา"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Send("ขอบโต๊ะ"); yield return null; Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(actor.ActiveStation, Is.Null);
        DialogueManager.Instance.HideDialogue(); State.ResetState(); PositionForQuestion(); Chat();
        Send("ช่วยหาหลักฐานในลิ้นชักเท่านั้น"); yield return null; Send("กล่อง"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
    }
}
