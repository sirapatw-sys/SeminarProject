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
    public IEnumerator NaturalInvitationStartsFollowingAndExplainsWhenMovementBegins()
    {
        Chat(); Send("มาช่วยดูด้วยกันหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Assert.That(State.HasFlag("found_note"), Is.False, "Walking together cannot discover evidence automatically.");
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("คุย"));
        Vector2 before = actor.Feet; Tick(2f); Assert.That(actor.Feet, Is.EqualTo(before));
        DialogueManager.Instance.HideDialogue(); PlayerAt(new Vector2(-1f, -3f)); Tick(5f);
        Assert.That(Vector2.Distance(actor.Feet, before), Is.GreaterThan(0.2f));
    }
    [UnityTest]
    public IEnumerator ReadingDiscoveredEvidenceIsNotLockedBehindRelationshipOrPuzzleHints()
    {
        State.SetFlag("found_note"); State.SetRelationship("Alice", 10); Chat();
        Send("ช่วยอ่านโน้ตให้หน่อย"); yield return null;
        var note = KnowledgeLibrary.GetRoom("Room01").FindFact("desk_note");
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(note.statement));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
        Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("4592"));
        Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator LiteralReadingVariantsAndRepeatCannotFarmRelationship()
    {
        State.SetFlag("found_note"); Chat();
        int afterFirst = -1;
        foreach (string message in new[] { "ช่วยอ่านโน้ตให้หน่อย", "อ่านกระดาษโน้ตได้ไหม", "อยากให้เธออ่านโน้ตหน่อย", "โน้ตเขียนว่าอะไร", "พอจะอ่านโน้ตให้ฟังหน่อยได้ไหม", "please read desk_note" })
        {
            Send(message); yield return null;
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("2 · 9 · 5 · 4"), message);
            if (afterFirst < 0) afterFirst = State.GetRelationship("Alice");
            else Assert.That(State.GetRelationship("Alice"), Is.EqualTo(afterFirst), message);
        }
        Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator AmbiguousReadingAsksThenAcceptsOneShortEvidenceName()
    {
        State.SetFlag("found_note"); Chat(); Send("ช่วยอ่านอันนี้หน่อย"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ชิ้นไหน"));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        Send("โน้ตค่ะ"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("2 · 9 · 5 · 4"));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "painting_arrow"), Is.False);
        Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator NextTurnReferenceUsesExplicitMentionWithoutTeachingOnMention()
    {
        State.SetFlag("found_note"); Chat(); Send("โน้ต"); yield return null;
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        Send("ช่วยอ่านอันนี้หน่อย"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("2 · 9 · 5 · 4"));
    }
    [UnityTest]
    public IEnumerator UnrelatedTurnAndRefusalDiscardEvidenceFocusAndQuestion()
    {
        State.SetFlag("found_note"); Chat();
        foreach (string message in new[] { "เธอคิดถึงบ้านไหม", "ไม่ต้องแล้ว", "สมมติให้อ่านโน้ต", "\"อ่านโน้ต\"", "โน้ตใช่ไหม" })
        {
            Send("โน้ต"); yield return null; Send(message); yield return null;
            Send("ช่วยอ่านอันนี้หน่อย"); yield return null;
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ชิ้นไหน"), message);
            Send("ไม่ต้องแล้ว"); yield return null; Send("โน้ต"); yield return null;
            Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False, message);
        }
    }
    [UnityTest]
    public IEnumerator ReadingContextCannotSurviveConversationResetRestoreDisableOrTaskChange()
    {
        foreach (string change in new[] { "close", "reset", "restore", "disable", "task", "room", "roomRoundTrip" })
        {
            DialogueManager.Instance.HideDialogue(); State.ResetState(); State.SetFlag("inspected_painting"); State.SetFlag("found_note"); PositionForQuestion(); Chat();
            Send("โน้ต"); yield return null;
            if (change == "close") { DialogueManager.Instance.HideDialogue(); Chat(); }
            if (change == "reset") { State.ResetState(); State.SetFlag("inspected_painting"); State.SetFlag("found_note"); }
            if (change == "restore") State.RestoreSnapshot(State.CreateSnapshot());
            if (change == "disable") { actor.enabled = false; actor.enabled = true; }
            if (change == "task") { Send("ตามมา"); yield return null; }
            if (change == "room") { State.SetCurrentScene("Room02"); Send("อ่านอันนี้"); yield return null; State.SetCurrentScene("Room01"); }
            if (change == "roomRoundTrip") { State.SetCurrentScene("Room02"); State.SetCurrentScene("Room01"); }
            Send("ช่วยอ่านอันนี้หน่อย"); yield return null;
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ชิ้นไหน"), change);
            Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False, change);
        }
    }
    [UnityTest]
    public IEnumerator UnknownUnfoundEvidenceAndWrongModelTargetsCannotBeRead()
    {
        Chat();
        foreach (string message in new[] { "ช่วยอ่านโน้ตหน่อย", "อ่านจดหมายลับหน่อย", "อ่านอันอื่นหน่อย" })
        {
            Send(message); yield return null;
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("2 · 9 · 5 · 4"), message);
            Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False, message);
        }
        State.SetFlag("found_note");
        GeneratedChatReply reply;
        CompanionNaturalRequests.TryApplyProposal(DialogueManager.Instance, actor, "อ่านโน้ตหน่อย", "read:painting_arrow", out reply);
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "painting_arrow"), Is.False);
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
    }
    [UnityTest]
    public IEnumerator MaliciousModelCannotTurnRefusalsQuotesStatusOrSelfReadingIntoActions()
    {
        provider.Available = true; State.SetFlag("found_note"); Chat();
        foreach (string message in new[] { "ไม่ต้องอ่านโน้ต", "อย่าอ่านโน้ต", "ถ้าช่วยอ่านโน้ต", "\"ช่วยอ่านโน้ต\"", "เคยอ่านโน้ตไหม", "ฉันอยากอ่านโน้ต", "ให้ฉันอ่านโน้ตหน่อย", "โน้ต" })
        {
            provider.Action = "read:desk_note"; Send(message); yield return null;
            Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False, message);
        }
        foreach (string message in new[] { "ไม่ต้องมาช่วยดูด้วยกัน", "ถ้ามาช่วยดูด้วยกัน", "\"มาช่วยดูด้วยกัน\"", "มาช่วยดูด้วยกันแล้วใช่ไหม", "โน้ต", "มาดูประตูด้วยกัน" })
        {
            provider.Action = "inspect_together"; Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message);
        }
    }
    [UnityTest]
    public IEnumerator ModelCanInterpretBoundedInvitationAndStopStillCancelsImmediately()
    {
        provider.Available = true; provider.Action = "inspect_together"; Chat();
        Send("ขอให้เธอมาตรวจหลักฐานเป็นเพื่อนหน่อย"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        int calls = provider.Calls; Send("หยุดเดิน"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(provider.Calls, Is.EqualTo(calls));
        Send("อยากให้เธอมาอยู่ข้างๆตอนเราดูหลักฐาน"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(calls + 1)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
    }
    [UnityTest]
    public IEnumerator TypedShowingTeachesOnlyThatEvidenceAndRepeatedShowingHasNoReward()
    {
        State.SetFlag("found_note"); Chat(); Send("ช่วยดูโน้ตให้หน่อย"); yield return null;
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
        int score = State.GetRelationship("Alice"); Send("เอาโน้ตให้ดู"); yield return null;
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "painting_arrow"), Is.False);
    }
    [UnityTest]
    public IEnumerator EqualEvidenceAliasesAskRatherThanSelectingTheFirstAsset()
    {
        var painting = KnowledgeLibrary.GetRoom("Room01").FindFact("painting_arrow");
        var original = painting.evidenceAliases;
        try
        {
            painting.evidenceAliases = new System.Collections.Generic.List<string> { "โน้ต" };
            State.SetFlag("found_note"); Chat(); Send("อ่านโน้ตหน่อย"); yield return null;
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ชิ้นไหน"));
            Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
            Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "painting_arrow"), Is.False);
        }
        finally { painting.evidenceAliases = original; }
    }
    [UnityTest]
    public IEnumerator ReadingRechecksLiveNpcRangeAndCannotStealAnotherPhysicalTask()
    {
        State.SetFlag("found_note"); provider.Available = true; provider.Action = "read:desk_note"; Chat();
        PlayerAt(new Vector2(-6f, -3f)); Send("อ่านโน้ตหน่อย"); yield return null;
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        PositionForQuestion(); Send("มาช่วยดูด้วยกันหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); int revision = actor.TaskRevision;
        Send("อ่านโน้ตหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(actor.TaskRevision, Is.EqualTo(revision));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
    }
    [UnityTest]
    public IEnumerator SharedEvidenceFocusAndLongestTitleResolveWithoutAliasCollision()
    {
        State.SetFlag("found_note"); State.SetFlag("companion.room01_alice.search.note_direction.found"); Chat();
        Assert.That(DialogueManager.Instance.TryShareEvidence("desk_note"), Is.True);
        Send("ช่วยอ่านอันนี้หน่อย"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("2 · 9 · 5 · 4"));
        Send("อ่านลูกศรบนขอบกระดาษหน่อย"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("บนขอบกระดาษมีลูกศรเล็กชี้จากขวาไปซ้าย"));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("2 · 9 · 5 · 4"));
    }
    [UnityTest]
    public IEnumerator PromptOffersOnlyAvailableEvidenceTitlesAndDoesNotSuppressAnswerQuestions()
    {
        Chat(); var before = AiDialogueGenerator.BuildKnowledgeContext("Alice", "อ่านโน้ตหน่อย");
        Assert.That(before.CompanionActions.Any(a => a.Id == "read:desk_note"), Is.False);
        State.SetFlag("found_note");
        var reading = AiDialogueGenerator.BuildKnowledgeContext("Alice", "อ่านโน้ตหน่อย");
        Assert.That(reading.PlayerAskedForHint, Is.False);
        Assert.That(reading.CompanionActions.Any(a => a.Id == "read:desk_note"), Is.True);
        Assert.That(reading.CompanionActions.Any(a => a.Id == "read:drawer_code"), Is.False);
        Assert.That(reading.ToCompanionActionSection(), Does.Not.Contain("2 · 9 · 5 · 4").And.Not.Contain("4592"));
        Assert.That(AiDialogueGenerator.BuildKnowledgeContext("Alice", "อ่านโน้ตจากขวาไปซ้ายได้รหัสอะไร").PlayerAskedForHint, Is.True);
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False, "Prompt construction is pure.");
        yield return null;
    }
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
        provider.Available = true;
        foreach (string action in new[] { "help:desk_support", "search" })
        foreach (string message in new[] { "ขอบโต๊ะ", "ขอบโต๊ะด้วย", "ขอบโต๊ะนี่", "the desk", "กล่องนี้", "the box", "ห้องนี้", "the room" })
        {
            DialogueManager.Instance.HideDialogue(); State.ResetState(); State.SetFlag("inspected_painting"); PositionForQuestion(); Chat();
            provider.Action = action; int calls = provider.Calls; int revision = actor.TaskRevision;
            int relationship = State.GetRelationship("Alice");
            Send(message); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(calls + 1), "Exercise model proposal: " + message);
            Assert.That(actor.ActiveStation, Is.Null, message + " / " + action);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message + " / " + action);
            Assert.That(actor.TaskRevision, Is.EqualTo(revision), message);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship), message);
        }
    }
    [UnityTest]
    public IEnumerator DirectHelpIntentStillAllowsModelInterpretationAndTargetFirstRequests()
    {
        provider.Available = true;
        foreach (string action in new[] { "help:desk_support", "search" })
        foreach (string message in new[] { "ไปช่วยตรงโต๊ะหน่อยสิ", "มาช่วยตรงโต๊ะหน่อย", "ขอบโต๊ะมันโยกนะช่วยจับไว้หน่อย", "please hold the desk" })
        {
            DialogueManager.Instance.HideDialogue(); State.ResetState(); State.SetFlag("inspected_painting"); PositionForQuestion(); Chat();
            provider.Action = action; Send(message); yield return null;
            Assert.That(actor.ActiveStation, Is.SameAs(Desk), message + " / " + action);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.GoingToHelp), message);
            Assert.That(State.HasFlag("found_note"), Is.False, "A request never completes the physical task.");
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
