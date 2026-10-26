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

public class CompanionWorldPlayTests
{
    NpcCompanionController actor;
    CompanionChatPanel chat;
    bool persistence, title, intro;
    GameState State => GameState.Instance;
    class Provider : IAiDialogueProvider
    {
        public string Action;
        public CompanionSemanticIntent Intent;
        public string Context;
        public bool Delay;
        public bool Available = true;
        public int Calls;
        public Action<GeneratedChatReply> Callback;
        public bool CanGenerate => Available;
        public string LastError => "";
        public IEnumerator Generate(MiniEventData d, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        {
            Calls++; Callback = done; Context = context;
            if (Delay) { while (true) yield return null; }
            done(new GeneratedChatReply { reply = "อืม", companionAction = Action, worldIntent = Intent });
        }
    }
    static void Title(bool value) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, value); }
    static void Intro(bool value) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, value); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = new Provider { Available = false }; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState(); actor = NpcCompanionController.Find("Alice", "Room01");
        actor.transform.position += (Vector3)(new Vector2(2.3f, -3f) - actor.Feet);
        var player = GameObject.FindGameObjectWithTag("Player"); player.transform.position += (Vector3)(new Vector2(3f, -3f) - actor.PlayerFeet);
        Physics2D.SyncTransforms(); chat = actor.GetComponent<CompanionChatPanel>();
    }
    [TearDown]
    public void Cleanup()
    {
        chat?.Close(); RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = persistence; Title(title); Intro(intro);
    }
    void Tick(float seconds = 25f) { for (int i = 0; i < seconds / 0.05f; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); } }
    CompanionWorldTarget Target(string id) => RoomWorldRegistry.Find("Room01", State, "room01." + id);

    [Test]
    public void RoomBindingsAreUniqueAndEveryAuthoredApproachPointIsReachable()
    {
        var targets = RoomWorldRegistry.Available("Room01", State);
        Assert.That(targets.Count, Is.EqualTo(16));
        RoomWorldRegistry.Install(actor.Scenario, actor.gameObject.scene);
        Assert.That(RoomWorldRegistry.Available("Room01", State).Count, Is.EqualTo(16));
        foreach (var target in targets)
        {
            Assert.That(actor.BeginWorldTask(target, CompanionWorldVerb.Move), Is.True, target.Definition.id + " from " + actor.Feet + " to " + target.Definition.approachPoint + " free=" + actor.IsFree(target.Definition.approachPoint) + " overlaps=" + string.Join(",", Physics2D.OverlapCircleAll(target.Definition.approachPoint, actor.Scenario.clearance).Select(c => c.name + ":" + c.bounds)));
            Tick(); Assert.That(actor.LastWorldResult?.Success, Is.True, target.Definition.id);
            Assert.That(Vector2.Distance(actor.Feet, target.Definition.approachPoint), Is.LessThanOrEqualTo(0.12f));
        }
    }
    [Test]
    public void ChatBlocksPlayerButDoesNotPauseCompanionOrRequireProximityToRecall()
    {
        Assert.That(chat.OpenSession(), Is.True);
        Assert.That(InputGate.IsBlocked, Is.True); Assert.That(InputGate.BlocksCompanionSimulation, Is.False);
        chat.Submit("ไปยืนที่มุมล่างซ้ายหน่อย"); Tick();
        Assert.That(actor.LastWorldResult?.Success, Is.True, chat.LastReply);
        Assert.That(Vector2.Distance(actor.Feet, actor.PlayerFeet), Is.GreaterThan(actor.Scenario.commandDistance));
        chat.Submit("เดินตามเราหน่อย"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        chat.Submit("หยุดก่อน"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
    }
    [Test]
    public void PassiveInspectionLeavesCoreProgressAndKnowledgeToPlayerDiscoveryAndSharing()
    {
        int playerSignals = 0, npcSignals = 0;
        Action<InteractionData, bool> player = (d, success) => playerSignals++;
        Action<InteractionData, bool, string> npc = (d, success, id) => { if (success && id == "Alice") npcSignals++; };
        InteractionSignals.Completed += player; InteractionSignals.CompanionCompleted += npc;
        try
        {
            chat.OpenSession(); chat.Submit("ช่วยไปตรวจภาพวาดหน่อย");
            Assert.That(State.HasFlag("inspected_painting"), Is.False);
            Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False);
            Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "painting_arrow"), Is.False);
            chat.Submit("ลองเช็คโต๊ะให้หน่อย"); Tick();
            Assert.That(State.HasFlag("found_note"), Is.False);
            Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "desk_note"), Is.False);
            Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
            Assert.That(DialogueManager.IsDialogueOpen, Is.False); Assert.That(ItemPopupUI.IsBusy, Is.False);
            Assert.That(playerSignals, Is.Zero); Assert.That(npcSignals, Is.Zero);
            Assert.That(CompanionDiscovery.Pending(actor.Scenario, State, "Room01").Any(f => f.factId == "desk_note"), Is.False);
            string response;
            Assert.That(InteractionSystem.Instance.TryExecute(Target("painting").Interaction.ActiveData, out response), Is.True);
            Assert.That(InteractionSystem.Instance.TryExecute(Target("desk").Interaction.ActiveData, out response), Is.True);
            Assert.That(State.HasFlag("inspected_painting"), Is.True); Assert.That(State.HasFlag("found_note"), Is.True);
            Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("desk_note", State), Is.False);
            chat.Submit("ช่วยอ่านโน้ตให้หน่อย"); Assert.That(chat.LastReply, Does.Contain("2 · 9 · 5 · 4"));
            Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
            Assert.That(chat.LastReply, Does.Not.Contain("4592"));
        }
        finally { InteractionSignals.Completed -= player; InteractionSignals.CompanionCompleted -= npc; }
    }
    [Test]
    public void FinishedInspectionIsReportedRememberedAndRecalledWithoutTheModel()
    {
        // Playtest: Alice walked to the painting silently, then said she did not know what she saw.
        chat.OpenSession(); chat.Submit("ช่วยไปตรวจภาพวาดหน่อย"); Tick();
        string result = actor.LastWorldResult.Message;
        Assert.That(actor.LastWorldResult.Success, Is.True); Assert.That(result, Does.Contain("ภาพวาด"));
        Assert.That(chat.LastReply, Is.EqualTo(result));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Is.EqualTo(result));
        Assert.That(State.GetNpcMemory("Alice").Any(m => m.Contains(result)), Is.True);
        foreach (string question in new[] { "เห็นอะไรบ้าง", "ดูแล้วเป็นยังไงบ้าง", "what did you see?" })
        { chat.Submit(question); Assert.That(chat.LastReply, Is.EqualTo(result), question); }
        foreach (string other in new[] { "เราเจออะไรบ้างแล้ว", "ไม่เห็นอะไรเลย", "โต๊ะเห็นอะไรไหม" })
        { chat.Submit(other); Assert.That(chat.LastReply, Is.Not.EqualTo(result), other); }
        Assert.That(State.HasFlag("inspected_painting"), Is.False);
    }
    [Test]
    public void RecallAnswersOnlyTheLatestActivity()
    {
        // Review: after the painting, newer evidence was found, yet "เห็นอะไรบ้าง" still
        // described the painting. A newer discovery or task makes the old result stale.
        chat.OpenSession(); chat.Submit("ช่วยไปตรวจภาพวาดหน่อย"); Tick();
        string result = actor.LastWorldResult.Message;
        chat.Submit("เห็นอะไรบ้าง"); Assert.That(chat.LastReply, Is.EqualTo(result));
        Assert.That(State.AddJournalEntry("test.newer_evidence", "หลักฐานใหม่", "พบสิ่งใหม่หลังจากนั้น"), Is.True);
        chat.Submit("เห็นอะไรบ้าง"); Assert.That(chat.LastReply, Is.Not.EqualTo(result), "newer journal entry");

        chat.Submit("ช่วยไปตรวจภาพวาดหน่อย"); Tick();
        chat.Submit("เห็นอะไรบ้าง"); Assert.That(chat.LastReply, Is.EqualTo(actor.LastWorldResult.Message), "a fresh task is recalled again");
        if (actor.ApplyRoomCommand(CompanionCommand.Search))
            Assert.That(actor.LastWorldResult, Is.Null, "a newer search supersedes the old result");
    }
    [Test]
    public void LockedDeskCannotBypassPaintingAndDrawerOrDoorCannotBeSolvedByNpc()
    {
        chat.OpenSession(); chat.Submit("ตรวจโต๊ะหน่อย"); Tick();
        Assert.That(actor.LastWorldResult.Success, Is.True); Assert.That(State.HasFlag("found_note"), Is.False);
        chat.Submit("เปิดลิ้นชักหน่อย"); Tick();
        Assert.That(actor.LastWorldResult.Success, Is.False); Assert.That(State.HasItem("key"), Is.False);
        chat.Submit("เปิดประตูหน่อย"); Tick();
        Assert.That(actor.LastWorldResult.Success, Is.False); Assert.That(State.GetCurrentScene(), Is.EqualTo("Room01"));
    }
    [Test]
    public void AmbiguousTargetAsksBackAndShortAnswerResolvesOnlyThatRequest()
    {
        chat.OpenSession(); chat.Submit("ไปตรวจอันนั้นหน่อย");
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(chat.LastReply, Does.Contain("หมายถึง"));
        chat.Submit("ภาพวาด"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.WorldTask)); Tick();
        Assert.That(State.HasFlag("inspected_painting"), Is.False); Assert.That(actor.LastWorldResult?.TargetId, Is.EqualTo("room01.painting"));
        chat.Submit("ไปยืนข้างขวาของมันหน่อย"); Tick();
        Assert.That(actor.LastWorldResult.Success, Is.True);
        Assert.That(Vector2.Distance(actor.Feet, Target("painting").Definition.approachPoint + Vector2.right * 0.7f), Is.LessThanOrEqualTo(0.12f));
    }
    [UnityTest]
    public IEnumerator StopCancelsPendingAiAndLateCallbackCannotRestartAnOldTask()
    {
        var provider = new Provider { Delay = true }; DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("ช่วยพิจารณารายละเอียดโต๊ะหน่อย"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(chat.RequestPending, Is.True);
        var oldCallback = provider.Callback;
        chat.Submit("หยุดก่อน"); Assert.That(chat.RequestPending, Is.False);
        oldCallback(new GeneratedChatReply { reply = "ได้", companionAction = "world:inspect:at:room01.desk" });
        Tick(); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.HasFlag("found_note"), Is.False);
    }
    [UnityTest]
    public IEnumerator SimulatedModelCanInterpretNewWordingButCannotChooseWrongTargetOrOperation()
    {
        var provider = new Provider { Action = "world:inspect:at:room01.painting" }; DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("ช่วยพิจารณารายละเอียดภาพวาดหน่อย"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.WorldTask)); Tick();
        Assert.That(State.HasFlag("inspected_painting"), Is.False); Assert.That(actor.LastWorldResult?.Success, Is.True);
        var session = new CompanionWorldSession(); GeneratedChatReply reply;
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยพิจารณารายละเอียดโต๊ะหน่อย", "world:inspect:at:room01.painting", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยตรวจลิ้นชักหน่อย", "world:open:at:room01.drawer", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "อย่าเปิดลิ้นชัก", "world:open:at:room01.drawer", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "สวัสดี", "world:move:at:room01.desk", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยไปดูโต๊ะ", "world:inspect:at:invented", out reply), Is.False);
    }
    [Test]
    public void TargetDisappearingAndStateResetCannotCompleteWorkOrKeepChatOpen()
    {
        chat.OpenSession(); chat.Submit("ไปตรวจภาพวาดหน่อย"); Target("painting").gameObject.SetActive(false); Tick();
        Assert.That(actor.LastWorldResult.Success, Is.False); Assert.That(State.HasFlag("inspected_painting"), Is.False);
        chat.Submit("ไปตรวจโต๊ะหน่อย"); State.ResetState(); Tick();
        Assert.That(CompanionChatPanel.IsOpen, Is.False); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(State.HasFlag("found_note"), Is.False);
    }
    [Test]
    public void WorldSnapshotNeverIncludesHiddenFactTextOrPuzzleAnswersAndDoesNotStartTasks()
    {
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยตรวจโต๊ะหน่อย");
        Assert.That(context.PlayerAskedForHint, Is.False);
        Assert.That(context.CompanionActions.Any(a => a.Id == "world:inspect:at:room01.desk"), Is.True);
        string actions = string.Join("\n", context.CompanionActions.Select(a => a.Id + a.Description));
        Assert.That(actions, Does.Not.Contain("4592")); Assert.That(actions, Does.Not.Contain("2 · 9 · 5 · 4"));
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.HasFlag("found_note"), Is.False);
    }
    [Test]
    public void AddressedRequestsCorrectionAndDataOnlyNewTargetUseTheSameExecutor()
    {
        var go = new GameObject("TestLantern");
        try
        {
            go.AddComponent<CompanionWorldTarget>().Configure(new CompanionWorldDefinition {
                id = "test.lantern", title = "โคมทดลอง", aliases = new System.Collections.Generic.List<string> { "โคมไฟใหม่" },
                approachPoint = new Vector2(2, -2), allowInspect = true, inspectLine = "โคมไฟดับอยู่" }, null);
            chat.OpenSession(); chat.Submit("อลิซ ช่วยไปตรวจโคมไฟใหม่หน่อย"); Tick();
            Assert.That(actor.LastWorldResult?.Message, Is.EqualTo("โคมไฟดับอยู่"));
            chat.Submit("ไม่ใช่อันนั้น ไปที่โต๊ะหน่อย"); Tick();
            Assert.That(actor.LastWorldResult?.TargetId, Is.EqualTo("room01.desk"));
            Assert.That(State.HasFlag("found_note"), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [Test]
    public void ResetClearsTranscriptAndInspectingAnOpenedDrawerDoesNotDescribeItAsLocked()
    {
        State.SetFlag("drawer_opened"); State.SetFlag("interaction.open_drawer.completed");
        chat.OpenSession(); chat.Submit("ตรวจลิ้นชักหน่อย"); Tick();
        Assert.That(chat.LastReply, Does.Contain("เปิดออกแล้ว")); Assert.That(chat.LastReply, Does.Not.Contain("แม่กุญแจรหัส"));
        State.ResetState(); Assert.That(chat.LastReply, Is.Null);
        chat.OpenSession(); Assert.That(chat.LastReply, Is.Null);
    }
    [UnityTest]
    public IEnumerator ClosingOrReplacingRequestInvalidatesDelayedProviderReply()
    {
        var provider = new Provider { Delay = true }; DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("ช่วยพิจารณารายละเอียดภาพวาดหน่อย"); yield return null;
        var first = provider.Callback;
        chat.Submit("ไปยืนกลางห้องหน่อย"); first(new GeneratedChatReply { companionAction = "world:inspect:at:room01.painting", reply = "ได้" });
        Tick(); Assert.That(actor.LastWorldResult.TargetId, Is.EqualTo("room01.center")); Assert.That(State.HasFlag("inspected_painting"), Is.False);
        chat.Submit("ช่วยพิจารณารายละเอียดภาพวาดหน่อย"); yield return null;
        var second = provider.Callback; chat.Close(); second(new GeneratedChatReply { companionAction = "world:inspect:at:room01.painting", reply = "ได้" });
        Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False);
    }
    [UnityTest]
    public IEnumerator WorldInspectionPoseUsesWorldTargetWithoutDependingOnASearchSpot()
    {
        var target = Target("painting");
        actor.transform.position += (Vector3)(target.Definition.approachPoint - actor.Feet); Physics2D.SyncTransforms();
        chat.OpenSession(); chat.Submit("ตรวจภาพวาดหน่อย"); actor.Tick(0.05f);
        Assert.That(actor.ActiveSearchSpot, Is.Null); Assert.That(actor.IsInspecting, Is.True);
        yield return null;
        Assert.That(actor.GetComponent<CompanionWorkVisuals>().IsWorking, Is.True);
        Assert.That(actor.WorkAnimationTime, Is.GreaterThan(0f));
    }
    [Test]
    public void OptedInOpenInteractionRunsConditionsOnceWithoutOpeningPlayerPopup()
    {
        var go = new GameObject("TestCabinet"); var data = ScriptableObject.CreateInstance<InteractionData>();
        try
        {
            data.interactionId = "test.open_cabinet"; data.allowCompanionExecution = true; data.repeatable = false;
            data.companionOperation = CompanionInteractionOperation.Open;
            data.companionMessage = "เปิดตู้แล้ว พบเหรียญ"; data.alreadyCompletedMessage = "ตู้เปิดแล้ว"; data.popupItemId = "key";
            data.actions.Add(new ActionCommand { type = ActionType.SetFlag, targetId = "test.cabinet.open" });
            var obj = go.AddComponent<ObjectInteraction>();
            typeof(ObjectInteraction).GetField("interactionData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(obj, data);
            var target = go.AddComponent<CompanionWorldTarget>(); target.Configure(new CompanionWorldDefinition {
                id = "test.cabinet", title = "ตู้ทดลอง", allowOpen = true, approachPoint = new Vector2(2, -2) }, obj);
            chat.OpenSession(); chat.Submit("เปิดตู้ทดลองหน่อย"); Tick();
            Assert.That(State.HasFlag("test.cabinet.open"), Is.True); Assert.That(actor.LastWorldResult.Success, Is.True);
            Assert.That(ItemPopupUI.IsBusy, Is.False);
            chat.Submit("เปิดตู้ทดลองหน่อย"); Tick(); Assert.That(actor.LastWorldResult.Success, Is.False);
            Assert.That(chat.LastReply, Is.EqualTo("ตู้เปิดแล้ว"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(data); }
    }
    [UnityTest]
    public IEnumerator RoomChatPreservesNaturalInvitationsAndExistingCooperativeHelp()
    {
        var provider = new Provider { Delay = true }; DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("มาช่วยดูอันนี้ด้วยกันหน่อย");
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(provider.Calls, Is.Zero);
        chat.Submit("หยุดก่อน"); State.SetFlag("inspected_painting");
        chat.Submit("ช่วยจับโต๊ะหน่อย");
        Assert.That(actor.ActiveStation?.id, Is.EqualTo("desk_support")); Assert.That(provider.Calls, Is.Zero);
        chat.Submit("หยุดก่อน"); Assert.That(actor.ActiveStation, Is.Null);
        provider.Delay = false; provider.Action = "inspect_together";
        chat.Submit("ช่วยมาดูด้วยกันสักครู่ได้ไหม"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
    }
    [Test]
    public void ClarificationCannotTreatConversationalMentionAsPermissionToAct()
    {
        foreach (string message in new[] { "เราชอบภาพวาด", "ภาพวาดสวยดี", "อยากคุยเรื่องภาพวาด" })
        {
            chat.Close(); chat.OpenSession(); chat.Submit("ไปตรวจอันนั้นหน่อย");
            Assert.That(chat.LastReply, Does.Contain("หมายถึง"));
            chat.Submit(message);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message);
        }
    }
    [Test]
    public void ReadingEvidenceExpiresAnUnansweredWorldClarification()
    {
        State.SetFlag("found_note"); chat.OpenSession(); chat.Submit("ไปตรวจอันนั้นหน่อย");
        chat.Submit("ช่วยอ่านโน้ตให้หน่อย"); Assert.That(chat.LastReply, Does.Contain("2 · 9 · 5 · 4"));
        chat.Submit("ภาพวาด"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
    }
    [Test]
    public void ModelCannotSwapRequestedSideOrInventAnUnrequestedSide()
    {
        chat.OpenSession(); var session = new CompanionWorldSession(); GeneratedChatReply reply;
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยเดินไปข้างขวาของภาพวาดหน่อย", "world:move:left:room01.painting", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยเดินไปที่ภาพวาดหน่อย", "world:move:right:room01.painting", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยเดินไปข้างหลังภาพวาดหน่อย", "world:move:at:room01.painting", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยเดินไปข้างขวาของภาพวาดหน่อย", "world:move:right:room01.painting", out reply), Is.True);
    }
    [Test]
    public void EnglishAreaNameDoesNotTurnIntoAnExtraDirectionalOffset()
    {
        chat.OpenSession(); chat.Submit("walk to bottom left"); Tick();
        Assert.That(actor.LastWorldResult?.Success, Is.True);
        Assert.That(Vector2.Distance(actor.Feet, Target("lower_left").Definition.approachPoint), Is.LessThanOrEqualTo(0.12f));
        chat.Submit("ไปยืนข้างหลังกลางห้องหน่อย"); Tick();
        Assert.That(actor.LastWorldResult?.Success, Is.True);
        Assert.That(actor.Feet.y, Is.GreaterThan(Target("center").ApproachPoint.y));
    }
    [UnityTest]
    public IEnumerator ExternalStopInvalidatesAnOutstandingModelInterpretation()
    {
        var provider = new Provider { Delay = true }; DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("ช่วยพิจารณารายละเอียดภาพวาดหน่อย"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); var callback = provider.Callback;
        actor.ApplyRoomCommand(CompanionCommand.Wait);
        callback(new GeneratedChatReply { reply = "ได้", companionAction = "world:inspect:at:room01.painting" });
        yield return null; Tick();
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(chat.RequestPending, Is.False); Assert.That(State.HasFlag("inspected_painting"), Is.False);
    }
    [Test]
    public void DisabledInteractionCannotExecuteAfterNpcHasAcceptedTheTask()
    {
        chat.OpenSession(); chat.Submit("ไปตรวจภาพวาดหน่อย"); Target("painting").Interaction.enabled = false;
        Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False);
        Assert.That(actor.LastWorldResult?.Success, Is.False);
    }
    [Test]
    public void ActionWordInAnAuthoredObjectNameDoesNotChangeThePlayersVerb()
    {
        var go = new GameObject("TestOpenSign");
        try
        {
            go.AddComponent<CompanionWorldTarget>().Configure(new CompanionWorldDefinition {
                id = "test.open_sign", title = "ป้ายเปิดร้าน", approachPoint = new Vector2(2, -2),
                allowInspect = true, allowOpen = true, inspectLine = "ตัวหนังสือบนป้ายยังอ่านได้" }, null);
            chat.OpenSession(); chat.Submit("ตรวจป้ายเปิดร้านหน่อย"); Tick();
            Assert.That(actor.LastWorldResult?.Verb, Is.EqualTo(CompanionWorldVerb.Inspect));
            Assert.That(actor.LastWorldResult?.Success, Is.True);
            var session = new CompanionWorldSession(); GeneratedChatReply reply;
            Assert.That(CompanionWorldCommands.TryProposal(actor, session, "ช่วยตรวจป้ายเปิดร้านหน่อย", "world:inspect:at:test.open_sign", out reply), Is.True);
            Tick(); Assert.That(actor.LastWorldResult?.Verb, Is.EqualTo(CompanionWorldVerb.Inspect));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    [Test]
    public void InspectingAnOpenableObjectCannotExecuteItsOpenEffects()
    {
        var go = new GameObject("TestCabinet"); var data = ScriptableObject.CreateInstance<InteractionData>();
        try
        {
            data.interactionId = "test.cabinet.open"; data.allowCompanionExecution = true;
            data.companionOperation = CompanionInteractionOperation.Open; data.repeatable = false;
            data.companionMessage = "ตู้เปิดแล้ว"; data.alreadyCompletedMessage = "ตู้เปิดแล้ว";
            data.actions.Add(new ActionCommand { type = ActionType.SetFlag, targetId = "test.cabinet.opened" });
            var obj = go.AddComponent<ObjectInteraction>();
            typeof(ObjectInteraction).GetField("interactionData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(obj, data);
            go.AddComponent<CompanionWorldTarget>().Configure(new CompanionWorldDefinition { id = "test.cabinet",
                title = "ตู้ทดลอง", allowInspect = true, allowOpen = true, approachPoint = new Vector2(2, -2), inspectLine = "ตู้ยังปิดอยู่" }, obj);
            chat.OpenSession(); chat.Submit("ตรวจตู้ทดลองหน่อย"); Tick();
            Assert.That(State.HasFlag("test.cabinet.opened"), Is.False);
            Assert.That(actor.LastWorldResult?.Success, Is.True); Assert.That(chat.LastReply, Is.EqualTo("ตู้ยังปิดอยู่"));
            var denied = InteractionSystem.Instance.TryExecuteCompanion(data, "Alice");
            Assert.That(denied.Success, Is.False); Assert.That(State.HasFlag("test.cabinet.opened"), Is.False);
            chat.Submit("เปิดตู้ทดลองหน่อย"); Tick(); Assert.That(State.HasFlag("test.cabinet.opened"), Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(data); }
    }
    [Test]
    public void EvidenceAskBackAcceptsAShortNameButNotAConversationalMention()
    {
        State.SetFlag("found_note"); chat.OpenSession(); chat.Submit("อ่านอันนี้หน่อย");
        Assert.That(chat.LastReply, Does.Contain("หลักฐาน")); chat.Submit("เราชอบโน้ต");
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        chat.Submit("อ่านอันนี้หน่อย"); chat.Submit("โน้ตนะครับ");
        Assert.That(chat.LastReply, Does.Contain("2 · 9 · 5 · 4"));
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
        Assert.That(State.HasFlag("drawer_opened"), Is.False);
    }
    [Test]
    public void EvidenceAskBackExpiresOnAnotherTaskCloseOrAnUnrelatedTurn()
    {
        State.SetFlag("found_note"); chat.OpenSession(); chat.Submit("อ่านอันนี้หน่อย");
        chat.Submit("ไปยืนกลางห้องหน่อย"); Tick(); chat.Submit("โน้ต");
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        chat.Submit("อ่านอันนี้หน่อย"); chat.Close(); chat.OpenSession(); chat.Submit("โน้ต");
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        chat.Submit("อ่านอันนี้หน่อย"); chat.Submit("สวัสดี"); chat.Submit("โน้ต");
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
    }
    [Test]
    public void DecorationsAreInspectableAndLocationQuestionsDoNotMoveOrRevealPuzzleFacts()
    {
        chat.OpenSession(); chat.Submit("โซฟาอยู่ตรงไหน");
        Assert.That(chat.LastReply, Does.Contain("ด้านขวาล่าง")); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        chat.Submit("ช่วยไปดูโต๊ะข้างโซฟาหน่อย"); Tick();
        Assert.That(actor.LastWorldResult?.TargetId, Is.EqualTo("room01.side_table"), chat.LastReply);
        Assert.That(actor.LastWorldResult?.Success, Is.True); Assert.That(actor.LastWorldResult.FactIds, Is.Empty);
        Assert.That(State.HasFlag("found_note"), Is.False); Assert.That(State.HasItem("key"), Is.False);
    }
    [Test]
    public void LocationQuestionCannotSubstituteAKnownLandmarkForAnUnknownObject()
    {
        GeneratedChatReply reply;
        Assert.That(CompanionWorldCommands.TryLocation(actor, "กุญแจอยู่ไหน ใกล้โต๊ะไหม", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryLocation(actor, "ตู้เย็นอยู่ไหน", out reply), Is.False);
        Assert.That(CompanionWorldCommands.TryLocation(actor, "where is the sofa?", out reply), Is.True);
        Assert.That(reply.reply, Does.Contain("โซฟา"));
    }
    [Test]
    public void LocalBetweenRequestUsesReachableFloorAndBothObjectNames()
    {
        chat.OpenSession(); chat.Submit("ไปยืนตรงที่ว่างระหว่างโต๊ะกับประตูหน่อย"); Tick();
        Assert.That(actor.LastWorldResult?.Success, Is.True, chat.LastReply);
        Assert.That(chat.LastReply, Does.Contain("ระหว่าง").And.Contain("โต๊ะ").And.Contain("ประตู"));
        Assert.That(Vector2.Distance(actor.Feet, (Target("desk").ApproachPoint + Target("door").ApproachPoint) * 0.5f), Is.LessThanOrEqualTo(1.2f));
        Assert.That(actor.IsFree(actor.Feet), Is.True); Assert.That(State.HasFlag("found_note"), Is.False);
    }
    [UnityTest]
    public IEnumerator SemanticRequestDoesNotRequireTheLegacyCommandPrefix()
    {
        var provider = new Provider { Intent = new CompanionSemanticIntent {
            speechAct = "request", verb = "inspect", targetId = "room01.painting", relation = "at", referenceId = "" } };
        DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("ภาพวาดนั่น ฝากพิจารณารายละเอียดให้ที"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.WorldTask), chat.LastReply);
        Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False); Assert.That(actor.LastWorldResult?.TargetId, Is.EqualTo("room01.painting"));
    }
    [UnityTest]
    public IEnumerator SemanticQuestionCannotExecuteALegacyActionAndPronounsUseVerifiedFocus()
    {
        var provider = new Provider { Action = "world:open:at:room01.drawer", Intent = new CompanionSemanticIntent {
            speechAct = "question", verb = "none", targetId = "", relation = "at", referenceId = "" } };
        DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("ลิ้นชักมีอะไรน่าสนใจบ้าง"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.HasFlag("drawer_opened"), Is.False);
        chat.Submit("เดินไปที่ภาพวาดหน่อย"); Tick();
        provider.Action = null; provider.Intent = new CompanionSemanticIntent {
            speechAct = "request", verb = "inspect", targetId = "room01.painting", relation = "at", referenceId = "" };
        chat.Submit("อันนั้น ฝากพิจารณารายละเอียดให้ที"); yield return null;
        Assert.That(provider.Context, Does.Contain("วัตถุที่โฟกัสจากงานล่าสุด: room01.painting"));
        Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False, chat.LastReply); Assert.That(actor.LastWorldResult?.Success, Is.True);
    }
    [TestCase("อย่าเปิดลิ้นชัก", "open", "room01.drawer", "at", "")]
    [TestCase("เมื่อกี้เดินไปที่โซฟาแล้วใช่ไหม", "move", "room01.sofa", "at", "")]
    [TestCase("สวัสดี", "move", "room01.sofa", "at", "")]
    [TestCase("โต๊ะ ฝากพิจารณารายละเอียดให้ที", "inspect", "room01.painting", "at", "")]
    [TestCase("ช่วยตรวจลิ้นชัก", "open", "room01.drawer", "at", "")]
    [TestCase("ช่วยเดินไปด้านซ้ายโซฟา", "move", "room01.sofa", "right", "")]
    [TestCase("ช่วยเดินไประหว่างโต๊ะกับประตู", "move", "room01.desk", "between", "room01.painting")]
    [TestCase("อันนั้น ฝากพิจารณารายละเอียดให้ที", "inspect", "room01.painting", "at", "")]
    [TestCase("ฝากพิจารณารายละเอียดให้ที", "inspect", "invented", "at", "")]
    public void SemanticProposalsRejectContradictionsUnknownTargetsAndMissingFocus(string text, string verb, string target, string relation, string reference)
    {
        chat.OpenSession(); GeneratedChatReply reply;
        Assert.That(CompanionWorldCommands.TrySemantic(actor, new CompanionWorldSession(), text, new CompanionSemanticIntent {
            speechAct = "request", verb = verb, targetId = target, relation = relation, referenceId = reference }, out reply), Is.False);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.HasFlag("drawer_opened"), Is.False);
    }
    [Test]
    public void SpatialTasksTrackNewObjectPositionsAndCancelWhenEitherReferenceChanges()
    {
        var sofa = Target("sofa"); var original = sofa.transform.position;
        try
        {
            Vector2 before = sofa.ApproachPoint; sofa.transform.position += Vector3.left; Physics2D.SyncTransforms();
            Assert.That(sofa.ApproachPoint, Is.EqualTo(before + Vector2.left));
            actor.transform.position += (Vector3)(new Vector2(0, -3.5f) - actor.Feet); Physics2D.SyncTransforms();
            Assert.That(actor.BeginWorldTask(sofa, CompanionWorldVerb.Move, "front"), Is.True);
            sofa.transform.position += Vector3.left; Physics2D.SyncTransforms(); Tick();
            Assert.That(actor.LastWorldResult?.Success, Is.False); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
            Assert.That(actor.BeginWorldTask(Target("desk"), CompanionWorldVerb.Move, "between", Target("door")), Is.True);
            Target("door").gameObject.SetActive(false); Tick(); Assert.That(actor.LastWorldResult?.Success, Is.False);
        }
        finally { sofa.transform.position = original; Physics2D.SyncTransforms(); }
    }
    [UnityTest]
    public IEnumerator PortraitDialogueUsesTheSameSemanticExecutorAfterClosingConversation()
    {
        var provider = new Provider { Intent = new CompanionSemanticIntent {
            speechAct = "request", verb = "inspect", targetId = "room01.painting", relation = "at", referenceId = "" } };
        DialogueProviders.Override = provider;
        DialogueManager.Instance.StartDialogue("Alice", new[] { "คุยกัน" }, false);
        var field = (TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(DialogueManager.Instance);
        field.text = "โซฟาอยู่ไหน"; DialogueManager.Instance.SendTypedMessage();
        Assert.That(provider.Calls, Is.Zero); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        field.text = "ภาพวาดนั่น ฝากพิจารณารายละเอียดให้ที"; DialogueManager.Instance.SendTypedMessage(); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.WorldTask));
        Tick(1); Assert.That(State.HasFlag("inspected_painting"), Is.False, "Portrait conversation still pauses movement.");
        DialogueManager.Instance.HideDialogue(); Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False); Assert.That(actor.LastWorldResult?.Success, Is.True);
    }
    [Test]
    public void SpatialDestinationCannotCrossWrongSideOrTeleportIntoBlockedFloor()
    {
        var sofa = Target("sofa");
        Assert.That(actor.BeginWorldTask(sofa, CompanionWorldVerb.Move, "front"), Is.True); Tick();
        Assert.That(actor.LastWorldResult.Success, Is.True);
        Assert.That(actor.Feet.y, Is.LessThan(sofa.SpatialBounds.min.y));
        Assert.That(actor.IsFree(actor.Feet), Is.True);
        Assert.That(actor.BeginWorldTask(sofa, CompanionWorldVerb.Move, "invented"), Is.False);
        Assert.That(actor.BeginWorldTask(sofa, CompanionWorldVerb.Open), Is.False);
        Assert.That(actor.BeginWorldTask(sofa, CompanionWorldVerb.Move, "between", sofa), Is.False);
        // The upper-right corner is close to both walls. Never silently substitute 'at' for an impossible side.
        var area = new GameObject("BlockedSpatialArea");
        try
        {
            var target = area.AddComponent<CompanionWorldTarget>();
            target.Configure(new CompanionWorldDefinition { id = "test.edge", title = "ขอบขวา", approachPoint = new Vector2(7.7f, 0) }, null);
            Assert.That(actor.BeginWorldTask(target, CompanionWorldVerb.Move, "right"), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(area); }
    }
    [Test]
    public void GreetingOffersOnlyFollowAndNoWorldSchema()
    {
        // Ordinary chat keeps one cheap movement option so wording the rules miss can still
        // ask her along; world targets, stations and evidence stay out.
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "สวัสดี Alice");
        Assert.That(context.CompanionActions.Select(a => a.Id), Is.EqualTo(new[] { CompanionActionOption.Follow }));
        string prompt = AiDialogueGenerator.BuildReplyPrompt("Alice", "Alice", "", "สวัสดี Alice", null, context);
        Assert.That(prompt, Does.Not.Contain("worldIntent").And.Not.Contain("world:move:").And.Not.Contain("ตำแหน่งบนจอ"));
        Assert.That(prompt.Length, Is.LessThan(5000));
    }
    [TestCase("วันนี้เธอดูเหนื่อยนะ")]
    [TestCase("ขอบคุณที่ช่วยนะ")]
    [TestCase("อยากให้เราช่วยอะไรไหม")]
    [TestCase("เธอดูเหนื่อยนะ")]
    [TestCase("thanks for helping")]
    [TestCase("you look tired")]
    public void SocialMentionsOfActionWordsDoNotDumpWorldCapabilities(string message)
    {
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
        Assert.That(context.CompanionActions.Select(a => a.Id), Is.EqualTo(new[] { CompanionActionOption.Follow }), message);
        string prompt = AiDialogueGenerator.BuildReplyPrompt("Alice", "Alice", "", message, null, context);
        Assert.That(prompt, Does.Not.Contain("worldIntent").And.Not.Contain("world:move:"));
        Assert.That(prompt.Length, Is.LessThan(5000));
        TestContext.WriteLine("ROUTED_SOCIAL|" + message + "|actions=" + context.CompanionActions.Count + "|promptChars=" + prompt.Length);
    }
    [TestCase("เดินเคียงข้างกันหน่อยนะ")]
    [TestCase("walk beside me please")]
    public void PlainMovementRequestsKeepFollowWithoutTheWorldMenu(string message)
    {
        // Playtest: a walk-with-me request cost ~5x a greeting because every target was offered.
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
        Assert.That(context.CompanionActions.Any(a => a.Id == CompanionActionOption.Follow), Is.True, message);
        Assert.That(context.CompanionActions.Any(a => a.Id.StartsWith("world:")), Is.False, message);
        string prompt = AiDialogueGenerator.BuildReplyPrompt("Alice", "Alice", "", message, null, context);
        Assert.That(prompt, Does.Not.Contain("worldIntent").And.Contain(CompanionActionOption.Follow));
        Assert.That(prompt.Length, Is.LessThan(5000)); // the same budget as a greeting
        TestContext.WriteLine("ROUTED_MOVE|" + message + "|actions=" + context.CompanionActions.Count + "|promptChars=" + prompt.Length);
    }
    [TestCase("พิจารณาภาพวาดให้ที")]
    [TestCase("ภาพวาดนั่น พิจารณารายละเอียดหน่อย")]
    [TestCase("Assess the painting for me")]
    [TestCase("พิจารณาของที่แขวนเอียงให้ที")]
    [TestCase("ช่วยประเมินสิ่งแขวนเอียงให้ที")] // review: a description without a listed cue word
    public void SemanticRequestsDoNotNeedAListedActionVerb(string message)
    {
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
        var intent = new CompanionSemanticIntent {
            speechAct = "request", verb = "inspect", targetId = "room01.painting", relation = "at", referenceId = "" };
        Assert.That(intent.IsOffered(AiDialogueGenerator.CompanionActionIds(context)), Is.True);
        GeneratedChatReply reply;
        Assert.That(CompanionWorldCommands.TrySemantic(actor, new CompanionWorldSession(), message, intent, out reply), Is.True);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.WorldTask));
        Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False);
        Assert.That(actor.LastWorldResult?.TargetId, Is.EqualTo("room01.painting"));
    }
    [UnityTest]
    public IEnumerator RoomChatExecutesAModelRequestWithoutAListedActionVerb()
    {
        DialogueProviders.Override = new Provider { Intent = new CompanionSemanticIntent {
            speechAct = "request", verb = "inspect", targetId = "room01.painting", relation = "at", referenceId = "" } };
        Assert.That(chat.OpenSession(), Is.True);
        chat.Submit("พิจารณาภาพวาดให้ที"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.WorldTask), chat.LastReply);
        Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.False);
    }
    [Test]
    public void FullDialogueSynchronouslyClosesRoomChatAndCancelsItsPendingRequest()
    {
        var provider = new Provider { Delay = true }; DialogueProviders.Override = provider;
        chat.OpenSession(); chat.Submit("ภาพวาดนั่น ฝากพิจารณารายละเอียดให้ที");
        Assert.That(chat.RequestPending, Is.True);
        var callback = provider.Callback;
        DialogueManager.Instance.StartDialogue("Alice", new[] { "คุยกัน" }, false);
        Assert.That(CompanionChatPanel.IsOpen, Is.False, "Two conversation surfaces must never share one frame.");
        Assert.That(chat.RequestPending, Is.False);
        callback(new GeneratedChatReply { reply = "ได้", companionAction = "world:inspect:at:room01.painting" });
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(chat.OpenSession(), Is.False);
    }
    [Test]
    public void MainPuzzleProgressRequiresThePlayerNotDelegatedInspection()
    {
        chat.OpenSession(); chat.Submit("ช่วยไปตรวจภาพวาดหน่อย"); Tick();
        Assert.That(State.HasFlag("inspected_painting"), Is.False);
        Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "painting_arrow"), Is.False);
        chat.Submit("ช่วยไปตรวจโต๊ะหน่อย"); Tick();
        Assert.That(State.HasFlag("found_note"), Is.False);
        Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "desk_note"), Is.False);
        chat.Submit("ช่วยค้นหารอบห้องหน่อย"); Tick();
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("note_direction")), Is.False);
    }
    [Test]
    public void EmptyWorldIntentCannotSilentlyEraseAValidLegacyFollowProposal()
    {
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยมาดูด้วยกันสักครู่ได้ไหม");
        var reply = new GeneratedChatReply { reply = "อืม", companionAction = "inspect_together", worldIntent = new CompanionSemanticIntent {
            speechAct = "chat", verb = "none", targetId = "", relation = "at", referenceId = "" } };
        CompanionSemanticIntent.SanitizeProposal(reply, AiDialogueGenerator.CompanionActionIds(context));
        Assert.That(reply.companionAction, Is.EqualTo("inspect_together"));
        Assert.That(reply.worldIntent, Is.Null);
    }
    [TestCase("help:two_hands_box", "ช่วยจับกล่องสองมือหน่อย", CompanionMode.GoingToHelp)]
    [TestCase("search", "ช่วยหาหลักฐานหน่อย", CompanionMode.Searching)]
    [TestCase("follow", "เดินตามเราหน่อย", CompanionMode.Following)]
    public void RoomAiProposalsUseTheSameValidatedLegacyExecutor(string action, string message, CompanionMode expected)
    {
        chat.OpenSession(); GeneratedChatReply reply;
        Assert.That(CompanionDialogueCommands.TryApplyRoomProposedAction(actor, message, action, out reply), Is.True);
        Assert.That(actor.Mode, Is.EqualTo(expected)); Assert.That(reply.reply, Is.EqualTo(actor.Bubble));
    }
    [TestCase("help:two_hands_box", "อย่าจับกล่องสองมือ")]
    [TestCase("search", "เธอค้นหาแล้วใช่ไหม")]
    [TestCase("follow", "สวัสดี")]
    public void RoomAiProposalsCannotTurnQuestionsRefusalsOrGreetingIntoActions(string action, string message)
    {
        chat.OpenSession(); GeneratedChatReply reply;
        Assert.That(CompanionDialogueCommands.TryApplyRoomProposedAction(actor, message, action, out reply), Is.False);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
    }
    [Test]
    public void PuzzleStepCannotBeDelegatedEvenIfAnAssetAccidentallyOptsIn()
    {
        var data = Target("painting").Interaction.ActiveData; bool original = data.allowCompanionExecution;
        try
        {
            data.allowCompanionExecution = true;
            Assert.That(InteractionSystem.Instance.TryExecuteCompanion(data, "Alice").Success, Is.False);
            Assert.That(State.HasFlag("inspected_painting"), Is.False);
        }
        finally { data.allowCompanionExecution = original; }
    }
    [Test]
    public void SearchCannotRevealAnUninspectedMainClueEvenAfterThePlayerFoundTheNote()
    {
        State.SetFlag("found_note"); chat.OpenSession(); chat.Submit("ช่วยหาหลักฐานหน่อย"); Tick(40);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("note_direction")), Is.False);
        Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "note_direction"), Is.False);
    }
    [Test]
    public void DataDialogueEntryAlsoClosesRoomChatImmediately()
    {
        chat.OpenSession();
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(actor.GetComponent<NPCInteraction>());
        DialogueManager.Instance.StartDialogue(data);
        Assert.That(DialogueManager.IsDialogueOpen, Is.True); Assert.That(CompanionChatPanel.IsOpen, Is.False);
        Assert.That(actor.GetComponentsInChildren<Canvas>(true).Any(c => c.gameObject.activeInHierarchy), Is.False);
    }
    [Test]
    public void WorldMenuIsCompactScopedAndStillAllowsSemanticSides()
    {
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ภาพวาดนั่น ฝากพิจารณารายละเอียดให้ที");
        var ids = AiDialogueGenerator.CompanionActionIds(context);
        Assert.That(context.CompanionActions.Count(a => a.Id.StartsWith("world:")), Is.LessThanOrEqualTo(3));
        Assert.That(context.ToCompanionActionSection(), Does.Not.Contain("ตำแหน่งบนจอ").And.Not.Contain("ขอบเขต"));
        Assert.That(new CompanionSemanticIntent { speechAct = "request", verb = "move", targetId = "room01.painting", relation = "right", referenceId = "" }.IsOffered(ids), Is.True);
        Assert.That(new CompanionSemanticIntent { speechAct = "request", verb = "move", targetId = "room01.desk", relation = "at", referenceId = "" }.IsOffered(ids), Is.False);
    }
    [Test]
    public void MeasureReviewRequestPayloadSizesWithoutCallingAnApi()
    {
        var generator = UnityEngine.Object.FindObjectOfType<AiDialogueGenerator>();
        var own = generator == null ? new GameObject("PayloadProbe") : null;
        if (own != null) generator = own.AddComponent<AiDialogueGenerator>();
        try
        {
            foreach (string message in new[] { "สวัสดี Alice", "วันนี้เป็นยังไงบ้าง", "ภาพวาดนั่น ฝากพิจารณารายละเอียดให้ที" })
            {
                var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
                string prompt = AiDialogueGenerator.BuildReplyPrompt("Alice", "Alice", "", message, null, context);
                var ids = AiDialogueGenerator.CompanionActionIds(context);
                var method = typeof(AiDialogueGenerator).GetMethod("BuildCompatibleReplyRequestJson", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                string wire = (string)method.Invoke(generator, new object[] { prompt, ids });
                TestContext.WriteLine("PAYLOAD|" + message + "|actions=" + context.CompanionActions.Count + "|promptChars=" + prompt.Length +
                    "|requestChars=" + wire.Length + "|requestUtf8Bytes=" + System.Text.Encoding.UTF8.GetByteCount(wire));
            }
        }
        finally { if (own != null) UnityEngine.Object.DestroyImmediate(own); }
    }
    [Test]
    public void MeasureCompanionHotPathsWithoutUsingAProvider()
    {
        State.SetFlag("found_note"); State.SetFlag(actor.Scenario.SearchFlag("note_direction"));
        State.SetFlag(NpcObservation.Flag("Alice", "Room01", "note_direction"));
        int checksum = 0;
        var allocationMethod = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        var allocated = allocationMethod == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), allocationMethod);
        if (allocated != null)
        {
            long probeStart = allocated(); var probe = new byte[4096]; long probeBytes = allocated() - probeStart;
            GC.KeepAlive(probe); if (probeBytes <= 0) allocated = null; // Some Unity Mono runtimes expose a zero-returning stub.
        }
        TestContext.WriteLine("PERF_ALLOCATION_COUNTER_SUPPORTED=" + (allocated != null));
        Action<string, int, Action> measure = (label, count, action) => {
            for (int i = 0; i < 10; i++) action();
            for (int sample = 0; sample < 3; sample++)
            {
                var timer = new System.Diagnostics.Stopwatch(); long before = allocated != null ? allocated() : 0;
                timer.Start(); for (int i = 0; i < count; i++) action(); timer.Stop();
                long bytes = allocated != null ? allocated() - before : -1;
                TestContext.WriteLine("PERF|" + label + "|sample=" + sample + "|count=" + count + "|ms=" +
                    timer.Elapsed.TotalMilliseconds.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "|bytes=" + bytes);
            }
        };
        measure("discovery_pending", 100, () => checksum += CompanionDiscovery.Pending(actor.Scenario, State, "Room01").Count);
        measure("discovery_indicator", 100, () => checksum += actor.HasDiscoveryInvitation ? 1 : 0);
        measure("world_find", 500, () => checksum += RoomWorldRegistry.Find("Room01", State, "room01.desk") != null ? 1 : 0);
        measure("npc_find", 500, () => checksum += NpcCompanionController.Find("Alice", "Room01") != null ? 1 : 0);
        chat.OpenSession();
        var addLine = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), chat,
            typeof(CompanionChatPanel).GetMethod("AddLine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        measure("chat_message_burst", 100, () => addLine("ข้อความทดสอบ"));
        Assert.That(checksum, Is.GreaterThan(0)); Assert.That(State.HasFlag("drawer_opened"), Is.False);
    }
    [Test]
    public void RegisteredTargetsReflectVisibilityDuplicatesReconfigurationAndDestructionImmediately()
    {
        var first = new GameObject("RegistryFirst"); var second = new GameObject("RegistrySecond");
        try
        {
            var target = first.AddComponent<CompanionWorldTarget>();
            target.Configure(new CompanionWorldDefinition { id = "perf.target", title = "วัตถุทดลอง", approachPoint = new Vector2(2, -2),
                visibleWhen = new System.Collections.Generic.List<ConditionRule> { new ConditionRule { type = ConditionType.HasFlag, targetId = "perf.visible" } } }, null);
            Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.target"), Is.Null);
            State.SetFlag("perf.visible"); Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.target"), Is.SameAs(target));
            target.enabled = false; Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.target"), Is.Null);
            target.enabled = true; Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.target"), Is.SameAs(target));
            var duplicate = second.AddComponent<CompanionWorldTarget>();
            duplicate.Configure(new CompanionWorldDefinition { id = "perf.target", title = "อีกชิ้น", approachPoint = new Vector2(2, -2) }, null);
            Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.target"), Is.Null);
            Assert.That(RoomWorldRegistry.Available("Room01", State).Any(t => t.Definition.id == "perf.target"), Is.False);
            second.SetActive(false); Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.target"), Is.SameAs(target));
            target.Configure(new CompanionWorldDefinition { id = "perf.changed", title = "เปลี่ยนชื่อ", approachPoint = new Vector2(2, -2) }, null);
            Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.target"), Is.Null);
            var snapshot = RoomWorldRegistry.Available("Room01", State); snapshot.Clear();
            Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.changed"), Is.SameAs(target));
            UnityEngine.Object.DestroyImmediate(first); Assert.That(RoomWorldRegistry.Find("Room01", State, "perf.changed"), Is.Null);
        }
        finally { if (first != null) UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
    }
    [Test]
    public void NpcLookupRetainsSceneLookupSemanticsAcrossDisableEnableAndDestroy()
    {
        Assert.That(NpcCompanionController.Find("Alice", "Room01"), Is.SameAs(actor));
        actor.enabled = false;
        Assert.That(NpcCompanionController.Find("Alice", "Room01"), Is.SameAs(actor));
        Assert.That(actor.CanReceiveRoomCommand, Is.False); actor.enabled = true;
        var go = new GameObject("RegistryNpc");
        try
        {
            var other = go.AddComponent<NpcCompanionController>(); other.Configure(actor.Scenario);
            var originalOrder = UnityEngine.Object.FindObjectsOfType<NpcCompanionController>().First(a => a.Scenario?.npcId == "Alice" && a.RoomId == "Room01");
            Assert.That(NpcCompanionController.Find("Alice", "Room01"), Is.SameAs(originalOrder));
            actor.gameObject.SetActive(false); Assert.That(NpcCompanionController.Find("Alice", "Room01"), Is.SameAs(other));
            UnityEngine.Object.DestroyImmediate(go); Assert.That(NpcCompanionController.Find("Alice", "Room01"), Is.Null);
        }
        finally { if (go != null) UnityEngine.Object.DestroyImmediate(go); actor.gameObject.SetActive(true); }
        Assert.That(NpcCompanionController.Find("Alice", "Room01"), Is.SameAs(actor));
    }
    [Test]
    public void DiscoveryIndicatorRechecksPermissionsAndSnapshotRestoreWithoutCachedAnswers()
    {
        State.SetFlag("found_note"); State.SetFlag(actor.Scenario.SearchFlag("note_direction"));
        State.SetFlag(NpcObservation.Flag("Alice", "Room01", "note_direction"));
        var profile = KnowledgeLibrary.GetNpc("Alice"); var forbidden = profile.forbiddenFactIds;
        var fact = KnowledgeLibrary.GetRoom("Room01").FindFact("note_direction"); bool shareable = fact.canShareAsEvidence;
        var unread = State.CreateSnapshot();
        try
        {
            Assert.That(actor.HasDiscoveryInvitation, Is.True);
            profile.forbiddenFactIds = new System.Collections.Generic.List<string> { "note_direction" };
            Assert.That(actor.HasDiscoveryInvitation, Is.False);
            profile.forbiddenFactIds = forbidden; fact.canShareAsEvidence = false;
            Assert.That(actor.HasDiscoveryInvitation, Is.False); fact.canShareAsEvidence = shareable;
            Assert.That(actor.HasDiscoveryInvitation, Is.True);
            CompanionDiscovery.Acknowledge(actor.Scenario, State, "Room01", new[] { "note_direction" });
            Assert.That(actor.HasDiscoveryInvitation, Is.False);
            State.ResetState(); Assert.That(actor.HasDiscoveryInvitation, Is.False);
            State.RestoreSnapshot(unread); Assert.That(actor.HasDiscoveryInvitation, Is.True);
            Assert.That(CompanionDiscovery.Pending(actor.Scenario, State, "Room01").Select(f => f.factId), Is.EqualTo(new[] { "note_direction" }));
        }
        finally { profile.forbiddenFactIds = forbidden; fact.canShareAsEvidence = shareable; }
    }
    [UnityTest]
    public IEnumerator BatchedChatShowsLatestMessagesAndHiddenTaskResultsWhenReopened()
    {
        chat.OpenSession(); chat.Submit("ไปยืนกลางห้องหน่อย"); chat.Close(); Tick();
        string result = chat.LastReply; Assert.That(result, Does.Contain("ถึง"));
        chat.OpenSession(); yield return null;
        var text = (TMPro.TMP_Text)typeof(CompanionChatPanel).GetField("transcript", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(chat);
        Assert.That(text.text, Does.Contain(result));
        chat.Submit("หยุดก่อน"); chat.Submit("ไปยืนข้างขวาของภาพวาดหน่อย"); string latest = chat.LastReply;
        yield return null;
        Assert.That(text.text, Does.Contain(latest)); Assert.That(text.text, Does.Contain("ไปยืนข้างขวาของภาพวาดหน่อย"));
        Assert.That(text.richText, Is.False);
        var scroll = (UnityEngine.UI.ScrollRect)typeof(CompanionChatPanel).GetField("historyScroll", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(chat);
        if (scroll.content.rect.height > scroll.viewport.rect.height) Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(0.02f));
    }
}
