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
        public bool Delay;
        public int Calls;
        public Action<GeneratedChatReply> Callback;
        public bool CanGenerate => true;
        public string LastError => "";
        public IEnumerator Generate(MiniEventData d, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        {
            Calls++; Callback = done;
            if (Delay) { while (true) yield return null; }
            done(new GeneratedChatReply { reply = "อืม", companionAction = Action });
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
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
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
        Assert.That(targets.Count, Is.EqualTo(9));
        RoomWorldRegistry.Install(actor.Scenario, actor.gameObject.scene);
        Assert.That(RoomWorldRegistry.Available("Room01", State).Count, Is.EqualTo(9));
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
    public void InspectingPaintingThenDeskRevealsOnlyVerifiedEvidenceAndNoPlayerUi()
    {
        int playerSignals = 0, npcSignals = 0;
        Action<InteractionData, bool> player = (d, success) => playerSignals++;
        Action<InteractionData, bool, string> npc = (d, success, id) => { if (success && id == "Alice") npcSignals++; };
        InteractionSignals.Completed += player; InteractionSignals.CompanionCompleted += npc;
        try
        {
            chat.OpenSession(); chat.Submit("ช่วยไปตรวจภาพวาดหน่อย");
            Assert.That(State.HasFlag("inspected_painting"), Is.False);
            Tick(); Assert.That(State.HasFlag("inspected_painting"), Is.True);
            Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "painting_arrow"), Is.True);
            chat.Submit("ลองเช็คโต๊ะให้หน่อย"); Tick();
            Assert.That(State.HasFlag("found_note"), Is.True);
            Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "desk_note"), Is.True);
            Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
            Assert.That(DialogueManager.IsDialogueOpen, Is.False); Assert.That(ItemPopupUI.IsBusy, Is.False);
            Assert.That(playerSignals, Is.Zero); Assert.That(npcSignals, Is.EqualTo(2));
            Assert.That(CompanionDiscovery.Pending(actor.Scenario, State, "Room01").Any(f => f.factId == "desk_note"), Is.True);
            chat.Submit("ช่วยอ่านโน้ตให้หน่อย"); Assert.That(chat.LastReply, Does.Contain("2 · 9 · 5 · 4"));
            Assert.That(chat.LastReply, Does.Not.Contain("4592"));
        }
        finally { InteractionSignals.Completed -= player; InteractionSignals.CompanionCompleted -= npc; }
    }
    [Test]
    public void LockedDeskCannotBypassPaintingAndDrawerOrDoorCannotBeSolvedByNpc()
    {
        chat.OpenSession(); chat.Submit("ตรวจโต๊ะหน่อย"); Tick();
        Assert.That(actor.LastWorldResult.Success, Is.False); Assert.That(State.HasFlag("found_note"), Is.False);
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
        Assert.That(State.HasFlag("inspected_painting"), Is.True);
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
        Assert.That(State.HasFlag("inspected_painting"), Is.True);
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
}
