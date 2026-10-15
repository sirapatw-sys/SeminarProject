using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CompanionCooperationPlayTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private NpcCompanionController actor;
    private CompanionStationData station;
    private bool persistence, title, intro;
    private GameState State { get { return GameState.Instance; } }
    private class Offline : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null) { done(null); yield break; }
    }
    private static void Title(bool value) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, value); }
    private static void Intro(bool value) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, value); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        DialogueProviders.Override = new Offline(); GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState(); actor = NpcCompanionController.Find("Alice", "Room01");
        Assert.That(actor, Is.Not.Null); station = actor.Scenario.stations[0];
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
        GameObject.FindGameObjectWithTag("Player").transform.position += (Vector3)(point - actor.PlayerFeet);
        Physics2D.SyncTransforms();
    }
    private void ActorAt(Vector2 point) { actor.transform.position += (Vector3)(point - actor.Feet); Physics2D.SyncTransforms(); }
    private void Step(float dt) { actor.Tick(dt); Physics2D.SyncTransforms(); }
    private bool Clear(Vector2 from, Vector2 to)
    {
        return (bool)typeof(NpcCompanionController).GetMethod("IsClear", Private).Invoke(actor, new object[] { from, to });
    }

    private bool PlayerFits(out float minimumGap)
    {
        var player = GameObject.FindGameObjectWithTag("Player").GetComponent<Collider2D>();
        minimumGap = float.PositiveInfinity;
        foreach (var obstacle in UnityEngine.Object.FindObjectsOfType<Collider2D>())
        {
            if (!obstacle.enabled || obstacle.isTrigger || obstacle == player ||
                obstacle.transform.IsChildOf(player.transform) || obstacle.transform == actor.transform ||
                obstacle.transform.IsChildOf(actor.transform)) continue;
            var separation = player.Distance(obstacle);
            if (!separation.isValid) { Debug.Log("DEEP_REVIEW invalid-distance " + obstacle.name); continue; }
            if (separation.isOverlapped) { minimumGap = separation.distance; return false; }
            minimumGap = Mathf.Min(minimumGap, separation.distance);
        }
        return minimumGap > 0.003f;
    }

    private void PositionForChat()
    {
        ActorAt(station.companionPoint); PlayerAt(station.playerPoint);
    }
    private void Chat()
    {
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", Private).GetValue(actor.GetComponent<NPCInteraction>());
        Assert.That(data, Is.Not.Null); DialogueManager.Instance.StartDialogue(data);
    }
    private void Send(string text)
    {
        ((TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance)).text = text;
        DialogueManager.Instance.SendTypedMessage();
    }

    [UnityTest]
    public IEnumerator LegalPlayerPositionsNearFurnitureAndRoomEdgesRemainFollowable()
    {
        var targets = new List<Vector2>();
        for (float y = -0.77f; y >= -0.95f; y -= 0.005f) targets.Add(new Vector2(-4.537f, y));
        foreach (float y in new[] { -4.35f, -4.2f, -3.7f, -2.65f, -1f, 0.5f, 1.3f, 1.65f, 1.8f, 2f })
            foreach (float x in new[] { -7.7f, -7.2f, -6.5f, -5f, -3f, -1f, 0f, 1f, 3f, 5f, 6.5f, 7.2f, 7.7f })
                targets.Add(new Vector2(x, y));
        int sampled = 0, mismatchedFootprints = 0, outsideBounds = 0;
        var issues = new List<string>(); var route = new List<Vector2>();
        foreach (Vector2 target in targets)
        {
            State.ResetState(); ActorAt(station.companionPoint); PlayerAt(station.playerPoint);
            Assert.That(actor.RequestFollow(), Is.True);
            if (Vector2.Distance(actor.Feet, target) <= actor.Scenario.followDistance + actor.Scenario.followStartBuffer) continue;
            PlayerAt(target); float gap;
            if (!PlayerFits(out gap)) continue;
            // Verify a physical player approach, including intermediate footprints.
            bool playerCanApproach = true;
            int samples = Mathf.CeilToInt(Vector2.Distance(station.playerPoint, target) / 0.03f);
            for (int i = 0; i <= samples; i++)
            {
                PlayerAt(Vector2.Lerp(station.playerPoint, target, i / (float)samples));
                if (!PlayerFits(out gap)) { playerCanApproach = false; break; }
            }
            if (!playerCanApproach) continue;
            PlayerAt(target);
            bool reachableStop = false;
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI / 12f;
                Vector2 stop = target + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * actor.Scenario.followDistance;
                if (CompanionNavigator.TryPath(actor.Feet, stop, actor.Scenario.walkBounds,
                    actor.Scenario.cellSize, actor.IsFree, Clear, route)) { reachableStop = true; break; }
            }
            if (!reachableStop) continue;
            sampled++; if (!actor.IsFree(target)) mismatchedFootprints++;
            if (!actor.Scenario.walkBounds.Contains(target)) outsideBounds++;
            for (int i = 0; i < 1800 && Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f; i++) Step(1f / 60f);
            if (Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f)
                issues.Add("target=" + target.ToString("F5") + " npcGoalFree=" + actor.IsFree(target) +
                    " final=" + actor.Feet.ToString("F5") + " distance=" + Vector2.Distance(actor.Feet, target));
        }
        Debug.Log("DEEP_REVIEW legal-player sampled=" + sampled + " footprintMismatch=" + mismatchedFootprints +
            " outsideNpcBounds=" + outsideBounds + " failures=" + issues.Count);
        foreach (string issue in issues) Debug.Log("DEEP_REVIEW legal-player-failure " + issue);
        Assert.That(sampled, Is.GreaterThanOrEqualTo(12));
        Assert.That(issues, Is.Empty, string.Join("\n", issues)); yield return null;
    }

    [UnityTest]
    public IEnumerator MovingPlayerCanReverseAndBeCaughtAfterStopping()
    {
        foreach (float speed in new[] { 1.5f, 5f })
        {
            State.ResetState(); ActorAt(new Vector2(-5f, -3.2f)); PlayerAt(new Vector2(-3f, -3.2f));
            Assert.That(actor.RequestFollow(), Is.True);
            const float dt = 1f / 60f;
            foreach (float x in new[] { 5f, -5f, 5f })
            {
                var target = new Vector2(x, -3.2f);
                for (int i = 0; i < 1200 && Vector2.Distance(actor.PlayerFeet, target) > 0.001f; i++)
                {
                    PlayerAt(Vector2.MoveTowards(actor.PlayerFeet, target, speed * dt));
                    Vector2 before = actor.Feet; Step(dt);
                    Assert.That(actor.IsFree(actor.Feet), Is.True);
                    Assert.That(Vector2.Distance(before, actor.Feet), Is.LessThanOrEqualTo(actor.Scenario.moveSpeed * dt + 0.001f));
                }
            }
            for (int i = 0; i < 1800 && Vector2.Distance(actor.Feet, actor.PlayerFeet) > actor.Scenario.followDistance + 0.07f; i++) Step(dt);
            Assert.That(Vector2.Distance(actor.Feet, actor.PlayerFeet), Is.LessThanOrEqualTo(actor.Scenario.followDistance + 0.07f), "playerSpeed=" + speed);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator FollowingWorksFromSeveralDifferentStartingPositions()
    {
        int cases = 0; var failures = new List<string>();
        foreach (var start in new[] { new Vector2(-5f, -3.2f), new Vector2(-4f, -2.65f),
            new Vector2(0f, -3.2f), new Vector2(5f, -3.2f) })
        foreach (var target in new[] { new Vector2(-6.9f, 1.2f), new Vector2(5.9f, 0.3f),
            new Vector2(-4.5f, 1.3f), new Vector2(4.6f, -1.7f) })
        {
            State.ResetState(); ActorAt(start); PlayerAt(start + Vector2.right * 0.7f);
            Assert.That(actor.IsFree(start), Is.True, "start=" + start); Assert.That(actor.RequestFollow(), Is.True, "request at " + start);
            PlayerAt(target); float gap;
            if (!PlayerFits(out gap)) continue;
            var route = new List<Vector2>();
            Assert.That(CompanionNavigator.TryFollowPath(start, target, actor.Scenario.followDistance,
                actor.Scenario.walkBounds, actor.Scenario.cellSize, actor.IsFree, Clear, route), Is.True, "route=" + start + " -> " + target);
            for (int i = 0; i < 1800 && Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f; i++)
            {
                Vector2 before = actor.Feet; Step(1f / 60f);
                Assert.That(actor.IsFree(actor.Feet), Is.True);
                Assert.That(Vector2.Distance(before, actor.Feet), Is.LessThanOrEqualTo(actor.Scenario.moveSpeed / 60f + 0.001f));
            }
            cases++;
            if (Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.07f)
                failures.Add("start=" + start + " target=" + target + " final=" + actor.Feet);
        }
        Debug.Log("DEEP_REVIEW alternative-start cases=" + cases + " failures=" + failures.Count);
        Assert.That(cases, Is.GreaterThanOrEqualTo(8));
        Assert.That(failures, Is.Empty, string.Join("\n", failures)); yield return null;
    }

    [UnityTest]
    public IEnumerator ARequestFollowedByItsExplicitNegationDoesNotExecute()
    {
        var failures = new List<string>();
        foreach (string text in new[] { "ช่วยเปิดกล่องสองมือ แต่ไม่ต้องช่วยเปิดกล่องแล้ว",
            "Open the box, but don't open the box", "ตามเรามา แต่ห้ามเดินตามเรา" })
        {
            State.ResetState(); State.SetRelationship("Alice", 10); PositionForChat();
            CompanionStationData selected; var parsed = CompanionDialogueCommands.Parse(text, actor.Scenario, out selected);
            Chat(); Send(text); yield return null;
            bool executed = actor.Mode != CompanionMode.Waiting || actor.ActiveStation != null;
            string mode = actor.Mode.ToString(); DialogueManager.Instance.HideDialogue();
            for (int i = 0; i < 100; i++) Step(0.05f);
            string detail = "message=" + text + " parsed=" + parsed + " mode=" + mode +
                " completed=" + actor.IsCompleted(station) + " relationship=" + State.GetRelationship("Alice");
            Debug.Log("DEEP_REVIEW negation " + detail);
            if (executed) failures.Add(detail);
        }
        Assert.That(failures, Is.Empty, string.Join("\n", failures)); yield return null;
    }

    [UnityTest]
    public IEnumerator AnExplicitLaterRequestCanReplaceAnEarlierRefusal()
    {
        foreach (string text in new[] { "ไม่ต้องช่วยเปิดกล่อง แต่ช่วยเปิดกล่องสองมือหน่อย",
            "Don't open the box, but open the box" })
        {
            State.ResetState(); PositionForChat(); Chat(); Send(text); yield return null;
            Assert.That(actor.ActiveStation, Is.SameAs(station), text);
            DialogueManager.Instance.HideDialogue();
        }
    }

    private CompanionStationData DeskStation { get { return actor.Scenario.stations.First(s => s.id == "desk_support"); } }
    private ObjectInteraction DeskObject()
    {
        return UnityEngine.Object.FindObjectsOfType<ObjectInteraction>().First(o =>
            ((InteractionData)typeof(ObjectInteraction).GetField("interactionData", Private).GetValue(o)).interactionId == "inspect_desk");
    }
    [UnityTest]
    public IEnumerator HoldingRequiresPlayerInspectionAndCanBeCancelledAndResumed()
    {
        var desk = DeskStation;
        State.SetFlag("inspected_painting");
        PlayerAt(desk.playerPoint); ActorAt(desk.companionPoint);
        Assert.That(actor.IsFree(desk.companionPoint), Is.True);
        Assert.That(PlayerFits(out var gap), Is.True);
        var deskObject = DeskObject();
        var playerCollider = GameObject.FindGameObjectWithTag("Player").GetComponent<Collider2D>();
        Assert.That(deskObject.GetComponent<Collider2D>().bounds.Intersects(playerCollider.bounds), Is.True, "Player must actually be in E interaction range.");
        Assert.That(InteractionFocus.PickNearest(new IFocusable[] { deskObject, actor.GetComponent<NPCInteraction>() }, actor.PlayerFeet), Is.SameAs(deskObject));
        State.SetRelationship("Alice", 10);
        Chat(); Send("ช่วยจับขอบโต๊ะหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(desk));
        DialogueManager.Instance.HideDialogue(); Step(0.05f);
        Assert.That(actor.TaskPhase, Is.EqualTo(CompanionTaskPhase.Working));
        Assert.That(actor.IsHolding("inspect_desk"), Is.True);
        for (int i = 0; i < 80; i++) Step(0.05f);
        Assert.That(State.HasFlag("found_note"), Is.False);
        Chat(); Send("หยุดช่วยก่อน"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(actor.TaskPhase, Is.EqualTo(CompanionTaskPhase.Cancelled));
        DialogueManager.Instance.HideDialogue();
        Chat(); Send("ช่วยจับขอบโต๊ะหน่อย"); yield return null;
        DialogueManager.Instance.HideDialogue(); Step(0.05f);
        DeskObject().Interact();
        Assert.That(State.HasFlag("found_note"), Is.True);
        Assert.That(actor.IsCompleted(desk), Is.True);
        Assert.That(actor.TaskPhase, Is.EqualTo(CompanionTaskPhase.Completed));
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(12));
        Assert.That(State.GetNpcMemory("Alice"), Does.Contain(desk.memory));
        Assert.That(State.HasItem("key"), Is.False);
        RoomTransitionManager.CloseRoomOverlays();
        DeskObject().Interact();
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(12));
    }
    [UnityTest]
    public IEnumerator SoloInspectionNeedsPreparationAndResetCancelsIt()
    {
        State.SetFlag("inspected_painting"); PlayerAt(DeskStation.playerPoint); ActorAt(Vector2.zero);
        var desk = DeskObject();
        desk.Interact();
        Assert.That(State.HasFlag("found_note"), Is.False);
        yield return new WaitForSeconds(0.2f);
        Assert.That(desk.PreparationProgress, Is.GreaterThan(0f));
        PlayerAt(Vector2.zero); yield return null;
        Assert.That(desk.PreparationProgress, Is.Zero, "Leaving the object cancels solo preparation.");
        PlayerAt(DeskStation.playerPoint); desk.Interact(); yield return new WaitForSeconds(0.1f);
        State.ResetState();
        Assert.That(desk.PreparationProgress, Is.Zero);
        State.SetFlag("inspected_painting"); desk.Interact();
        yield return new WaitForSeconds(2.3f);
        Assert.That(State.HasFlag("found_note"), Is.True);
        Assert.That(actor.IsCompleted(DeskStation), Is.False);
        Assert.That(State.HasItem("key"), Is.False);
    }
    [UnityTest]
    public IEnumerator TypedEvidenceAndDiscussionNeverInventUndiscoveredFacts()
    {
        PositionForChat(); Chat();
        Send("เอากระดาษโน้ตจากขอบโต๊ะให้ดู"); yield return null;
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.False);
        Send("ช่วยคิดด้วยกัน"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ข้อมูลไม่พอ"));
        State.SetFlag("inspected_painting"); State.SetFlag("found_note");
        Send("เอาข้อความหลังกรอบภาพให้ดู"); yield return null;
        Send("เอากระดาษโน้ตจากขอบโต๊ะให้ดู"); yield return null;
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
        Send("ช่วยคิดด้วยกัน"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ข้อสันนิษฐาน"));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("4592"));
        State.SetFlag(actor.Scenario.Flag("observed.drawer_failed"));
        Send("ช่วยคิดด้วยกัน"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ยังไม่สำเร็จ"));
    }
    [UnityTest]
    public IEnumerator InvitationsAreOptionalAndUnavailableBeforeDiscovery()
    {
        PlayerAt(DeskStation.playerPoint); ActorAt(DeskStation.companionPoint);
        actor.RefreshInvitation(); Assert.That(actor.Invitation, Is.Null);
        State.SetFlag("inspected_painting"); actor.RefreshInvitation();
        Assert.That(actor.Invitation, Is.SameAs(DeskStation));
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        Chat(); yield return null;
        Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(DeskStation.invitationLine));
        DialogueManager.Instance.HideDialogue(); actor.RefreshInvitation();
        Assert.That(actor.Invitation, Is.Null);
    }

    [UnityTest]
    public IEnumerator TargetedCancellationAndSaveRestoreNeverLeavePhantomWork()
    {
        State.SetFlag("inspected_painting"); PlayerAt(DeskStation.playerPoint); ActorAt(DeskStation.companionPoint);
        foreach (string cancel in new[] { "ไม่ต้องช่วยจับขอบโต๊ะแล้ว", "ไม่ต้องช่วยจับขอบโต๊ะแล้ว.", "หยุดช่วยก่อน", "ไม่ต้องแล้ว" })
        {
            Chat(); Send("ช่วยจับขอบโต๊ะหน่อย"); yield return null;
            Assert.That(actor.ActiveStation, Is.Not.Null);
            Send(cancel); yield return null;
            Assert.That(actor.ActiveStation, Is.Null, cancel);
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(actor.RequestHelp(DeskStation), Is.True); Step(0.05f);
        var snapshot = State.CreateSnapshot(); State.RestoreSnapshot(snapshot);
        Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(actor.IsHolding("inspect_desk"), Is.False);
        Assert.That(State.HasFlag("found_note"), Is.False);
        Assert.That(actor.RequestHelp(DeskStation), Is.True);
        for (int i = 0; i < 1000; i++) Step(0.05f);
        Assert.That(actor.TaskPhase, Is.EqualTo(CompanionTaskPhase.Failed));
        Assert.That(actor.LastTaskId, Is.EqualTo(DeskStation.id));
        // She was holding the desk, so "I can't get there" would be untrue.
        Assert.That(actor.Bubble, Is.EqualTo(actor.Scenario.releaseLine));
        Assert.That(State.HasFlag("found_note"), Is.False);
    }
    [UnityTest]
    public IEnumerator DeskRouteIsReachableAndRecallRequiresActualSharedExperience()
    {
        State.SetFlag("inspected_painting"); PlayerAt(DeskStation.playerPoint); ActorAt(new Vector2(-4f, -2.65f));
        Assert.That(actor.RequestHelp(DeskStation), Is.True);
        for (int i = 0; i < 300 && actor.Mode != CompanionMode.Helping; i++) Step(0.05f);
        Assert.That(actor.IsHolding("inspect_desk"), Is.True);
        Chat(); Send("จำขอบโต๊ะได้ไหม"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ยังไม่มีความทรงจำ"));
        DialogueManager.Instance.HideDialogue(); DeskObject().Interact(); RoomTransitionManager.CloseRoomOverlays();
        Chat(); Send("จำขอบโต๊ะได้ไหม"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(DeskStation.memory));
    }

    [UnityTest]
    public IEnumerator JointDeskDiscoverySupportsTypedHypothesesWithoutSolvingForPlayer()
    {
        State.SetFlag("inspected_painting");
        ActorAt(new Vector2(-4f, -2.65f)); PlayerAt(DeskStation.playerPoint);
        Chat(); Send("ช่วยจับขอบโต๊ะหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.GoingToHelp));
        DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 300 && !actor.IsHolding("inspect_desk"); i++) Step(0.05f);
        Assert.That(actor.IsHolding("inspect_desk"), Is.True);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("note_direction")), Is.False);
        DeskObject().Interact();
        Assert.That(actor.IsCompleted(DeskStation), Is.True);
        Assert.That(State.HasFlag("found_note"), Is.True);
        Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "note_direction"), Is.True);
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("desk_note", State), Is.False);
        RoomTransitionManager.CloseRoomOverlays(); Chat();
        Send("เธอเจออะไรบ้าง"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ลูกศร"));
        Send("เราคิดว่าต้องอ่านจากขวา เธอว่าไง"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ยังไม่มีข้อมูล"));
        Send("เอาโน้ตให้ดู"); yield return null;
        Assert.That(EvidenceSharing.HasBeenShared(State, "Alice", "Room01", "desk_note"), Is.True);
        State.SetRelationship("Alice", 50);
        Send("เราคิดว่าอ่านจากซ้ายไปขวา เธอว่าไง"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ขัด"));
        Send("เราคิดว่าอ่านจากขวาไปซ้าย เธอว่าไง"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("สอดคล้อง"));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("4592"));
        Assert.That(State.HasItem("key"), Is.False);
        DialogueManager.Instance.HideDialogue();
        string response;
        var drawer = KnowledgeLibrary.GetRoom("Room01").steps.Find(s => s.stepId == "open_drawer");
        Assert.That(drawer, Is.Not.Null);
        var data = UnityEngine.Resources.FindObjectsOfTypeAll<InteractionData>().First(d => d.interactionId == "open_drawer");
        Assert.That(InteractionSystem.Instance.TryExecute(data, out response, "4592"), Is.True);
        Assert.That(State.HasItem("key"), Is.True);
    }

    [UnityTest]
    public IEnumerator SoloFollowUpCanFindDirectionWithoutGivingAliceKnowledge()
    {
        State.SetFlag("inspected_painting");
        PlayerAt(DeskStation.playerPoint); ActorAt(Vector2.zero);
        var desk = DeskObject();
        desk.Interact(); yield return new WaitForSeconds(2.3f);
        Assert.That(State.HasFlag("found_note"), Is.True);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("note_direction")), Is.False);
        RoomTransitionManager.CloseRoomOverlays();
        desk.Interact(); yield return new WaitForSeconds(2.3f);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("note_direction")), Is.True);
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("note_direction", State), Is.False);
        Assert.That(actor.IsCompleted(DeskStation), Is.False);
        Assert.That(State.HasItem("key"), Is.False);
    }

    [UnityTest]
    public IEnumerator NpcSearchActuallyInspectsMarginBeforeReportingIt()
    {
        State.SetFlag("inspected_painting"); State.SetFlag("found_note");
        ActorAt(new Vector2(-4.6f, -2.65f)); PlayerAt(DeskStation.playerPoint);
        Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Searching));
        Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "note_direction"), Is.False);
        DialogueManager.Instance.HideDialogue();
        for (int i = 0; i < 300 && !NpcObservation.Knows(State, "Alice", "Room01", "note_direction"); i++) Step(0.05f);
        Assert.That(NpcObservation.Knows(State, "Alice", "Room01", "note_direction"), Is.True);
        ActorAt(DeskStation.companionPoint); Chat();
        Send("หยุดเดิน"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Send("เธอเจออะไรบ้าง"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("ลูกศร"));
        Assert.That(State.HasFlag("drawer_opened"), Is.False);
    }

    [UnityTest]
    public IEnumerator SharedOutcomeOnlyCelebratesAfterWitnessingRealSuccess()
    {
        ActorAt(DeskStation.companionPoint); PlayerAt(DeskStation.playerPoint);
        Chat(); Send("เราทำได้แล้วค่ะ"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Is.EqualTo(actor.Scenario.unverifiedOutcomeLine));
        Assert.That(State.HasFlag("drawer_opened"), Is.False);
        DialogueManager.Instance.HideDialogue(); State.SetFlag("found_note");
        var drawer = UnityEngine.Resources.FindObjectsOfTypeAll<InteractionData>().First(d => d.interactionId == "open_drawer");
        string response;
        Assert.That(InteractionSystem.Instance.TryExecute(drawer, out response, "4592"), Is.True);
        Assert.That(State.HasFlag(actor.Scenario.Flag("observed.drawer_seen")), Is.True);
        // The player dismisses the key's item card before opening NPC chat.
        RoomTransitionManager.CloseRoomOverlays();
        Chat(); int score = State.GetRelationship("Alice");
        Send("เราทำได้แล้วค่ะ"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("เธอทำได้แล้ว"));
        Send("เราทำได้แล้วค่ะ"); yield return null;
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator CompletedHypothesisAndCourtesyStillRespectLowTrust()
    {
        State.SetFlag("found_note"); State.SetFlag(actor.Scenario.SearchFlag("note_direction"));
        State.SetFlag(NpcObservation.Flag("Alice", "Room01", "note_direction"));
        EvidenceSharing.TryShare(State, "Alice", "desk_note", out _, false);
        State.SetRelationship("Alice", 10); ActorAt(DeskStation.companionPoint); PlayerAt(DeskStation.playerPoint); Chat();
        Send("อ่านจากขวาไปซ้ายใช่ไหมคะ"); yield return null;
        string first = State.GetConversationLog("Alice").Last().Text;
        Assert.That(first, Is.EqualTo(actor.Scenario.reasoningProfile.cautiousLine));
        Send("เราคิดว่าอ่านจากซ้ายไปขวา เธอว่าไงนะครับ"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Is.EqualTo(first));
        State.SetFlag("drawer_opened");
        Send("อ่านจากขวาไปซ้ายใช่ไหมคะ"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Is.EqualTo(actor.Scenario.reasoningProfile.unavailableLine));
    }
}
