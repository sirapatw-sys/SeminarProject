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
        public string Text = "เปิดกล่องแล้ว รหัสคือ 4592";
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null)
        {
            Calls++;
            done(new GeneratedChatReply { reply = Text, relationshipDelta = 6, referencedFactIds = Array.Empty<string>() });
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
    private void StartChat()
    {
#if UNITY_EDITOR
        var data = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset");
#else
        DialogueData data = null;
#endif
        Assert.That(data, Is.Not.Null); DialogueManager.Instance.StartDialogue(data);
    }
    private void Send(string message)
    {
        var input = (TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        input.text = message; DialogueManager.Instance.SendTypedMessage();
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
    public IEnumerator TypedActivityRequestStartsWorkButOnlyPhysicalCooperationCanCompleteIt()
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
            State.ResetState(); State.SetRelationship("Alice", 10);
            DialogueProviders.Override = provider;
            DialogueManager.Instance.StartDialogue(data);
            var input = (TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
            input.text = "ช่วยเราเปิดกล่องสองมือในห้องนี้หน่อย"; DialogueManager.Instance.SendTypedMessage();
            yield return null;
            string actual = State.GetConversationLog("Alice").Last().Text;
            Assert.That(actual, Does.Contain(station.requestLine)); Assert.That(actual, Does.Contain(station.instructions));
            Assert.That(actual, Does.Not.Contain("ผนัง")); Assert.That(actual, Does.Not.Contain("4592"));
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.GoingToHelp)); Assert.That(actor.ActiveStation, Is.SameAs(station));
            Vector2 before = actor.Feet; Tick(60f);
            Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(actor.Progress, Is.Zero);
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
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(station.completedStatusLine));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("จุดสีฟ้า"));
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(13));
        Send("ช่วยเราเปิดกล่องสองมือหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(State.GetRelationship("Alice"), Is.EqualTo(13));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(station.completedStatusLine));
    }

    [UnityTest]
    public IEnumerator TypedFollowWaitAndComfortAreLocalBoundedAndRewardComfortOnlyOnce()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint); State.SetRelationship("Alice", 10);
        var provider = new FakeCompanionProvider(); DialogueProviders.Override = provider;
        StartChat(); int memories = State.GetNpcMemory("Alice").Count;
        Send("ช่วยเราเดินสำรวจด้วยกันหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Send("ตามเรามาหน่อย"); yield return null;
        Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(memories + 1));
        Vector2 feet = actor.Feet; Tick(3f); Assert.That(actor.Feet, Is.EqualTo(feet));
        Send("รอตรงนี้ก่อน"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
        Send("ไม่ต้องกลัวนะ เราอยู่ด้วย"); yield return null;
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(11));
        Send("เราอยู่ตรงนี้ ไม่ต้องห่วงนะ"); yield return null;
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(11)); Assert.That(provider.Calls, Is.Zero);
        DialogueManager.Instance.HideDialogue(); Assert.That(InputGate.IsBlocked, Is.False);
        Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
    }

    [UnityTest]
    public IEnumerator TypedQuestionsRefusalsReportsAndAmbiguityDoNotStartWork()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint); State.SetRelationship("Alice", 10);
        StartChat();
        foreach (string message in new[] {
            "กล่องสองมือเปิดยังไง, ช่วยเราหน่อย", "อย่าช่วยเปิดกล่องสองมือ", "กล่องสองมือเปิดแล้วหรือยัง",
            "ช่วยอธิบายกล่องสองมือหน่อย", "Help me understand the box",
            "เธอช่วยเปิดกล่องสองมือแล้วใช่ไหม", "เธอตามเรามาเหรอ", "ช่วยสอนฉันเปิดกล่องสองมือหน่อย",
            "เธอรอตรงนี้ใช่ไหมครับ", "Help me; teach me to open the box", "Do you help me with the box?",
            "ถ้าชวนให้ตามมา เธอจะทำไหม", "\"ช่วยเปิดกล่องสองมือ\"", "‘follow me’", "`follow me`", "ช่วยเปิดกล่องสองมือแล้วตามมา" })
        {
            Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message); Assert.That(actor.ActiveStation, Is.Null, message);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
        }
        Send("กล่องสองมือเปิดยังไง, ช่วยเราหน่อย"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(station.instructions));
        Send("ช่วยเปิดกล่องสองมือและขอคำใบ้ปริศนา"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("4592"));
    }

    [UnityTest]
    public IEnumerator GenericHelpPrefixRoutesToTheExplicitCompanionCommandWithoutGivingAPuzzleHint()
    {
        string[] messages = { "ช่วยหน่อย, ตามเรามาหน่อย", "ช่วยเราหน่อย, รอตรงนี้ก่อน", "ช่วยหน่อย, ช่วยเปิดกล่องสองมือหน่อย" };
        CompanionMode[] expected = { CompanionMode.Following, CompanionMode.Waiting, CompanionMode.GoingToHelp };
        var provider = new FakeCompanionProvider(); DialogueProviders.Override = provider;
        for (int i = 0; i < messages.Length; i++)
        {
            State.ResetState(); PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
            State.SetRelationship("Alice", 10);
            if (i == 1) Assert.That(actor.RequestFollow(), Is.True);
            StartChat(); Send(messages[i]); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(expected[i]), messages[i]);
            Assert.That(actor.ActiveStation != null, Is.EqualTo(i == 2), messages[i]);
            string actual = State.GetConversationLog("Alice").Last().Text;
            Assert.That(actual, Does.Not.Contain("ห้องนี้มีอะไรบางอย่างดูไม่เข้าที่เข้าทาง"));
            Assert.That(State.HasItem("key"), Is.False); Assert.That(actor.IsCompleted(station), Is.False);
            Assert.That(State.GetRelationship("Alice"), Is.EqualTo(10));
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(provider.Calls, Is.Zero, "Player commands remain local, not AI tools.");
    }

    [UnityTest]
    public IEnumerator TypedCommandsRespectRangeOtherModalsAndDisabledActors()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint); StartChat();
        Title(true); Send("ตามเรามาหน่อย"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Title(false);
        ItemPopupUI.ShowItem("key"); Send("ตามเรามาหน่อย"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); ItemPopupUI.CancelAll();
        KeypadLockUI.Show("1234"); Send("ตามเรามาหน่อย"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); KeypadLockUI.Close();
        typeof(JournalUI).GetProperty("IsOpen").SetValue(null, true);
        Send("ตามเรามาหน่อย"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); JournalUI.Close();
        PlayerAt(new Vector2(-6f, -2f)); Send("ตามเรามาหน่อย"); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        PlayerAt(station.playerPoint); actor.enabled = false; Send("ตามเรามาหน่อย");
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); actor.enabled = true;
        DialogueManager.Instance.HideDialogue();
        GeneratedChatReply reply;
        Assert.That(CompanionDialogueCommands.TryReply(DialogueManager.Instance, "ตามเรามา", out reply), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TypedCommandsCannotBypassActivityConditionsOrTargetAnotherNpcOrRoom()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        var original = actor.Scenario; var scenario = UnityEngine.Object.Instantiate(original);
        try
        {
            scenario.stations[0].conditions.Add(new ConditionRule { type = ConditionType.HasFlag, targetId = "activity_ready" });
            actor.Configure(scenario); StartChat(); Send("ช่วยเราเปิดกล่องสองมือหน่อย"); yield return null;
            Assert.That(actor.ActiveStation, Is.Null); Assert.That(State.HasFlag("activity_ready"), Is.False);
            State.SetFlag("activity_ready"); Send("ช่วยเราเปิดกล่องสองมือหน่อย"); yield return null;
            Assert.That(actor.ActiveStation, Is.SameAs(scenario.stations[0]));
            Send("รอตรงนี้ก่อน"); Assert.That(actor.ActiveStation, Is.Null);
            typeof(DialogueManager).GetField("activeNpcId", Private).SetValue(DialogueManager.Instance, "Rina");
            Send("ตามเรามาหน่อย"); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
            typeof(DialogueManager).GetField("activeNpcId", Private).SetValue(DialogueManager.Instance, "Alice");
            State.SetCurrentScene("Room02"); Send("ตามเรามาหน่อย"); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        }
        finally { State.SetCurrentScene("Room01"); actor.Configure(original); UnityEngine.Object.DestroyImmediate(scenario); }
    }

    [UnityTest]
    public IEnumerator ABlockedStepKeepsTheRepathCooldownRatherThanSearchingEveryFrame()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        Assert.That(actor.RequestFollow(), Is.True); PlayerAt(new Vector2(5f, -2.65f));
        var obstacle = new GameObject("CompanionMovingObstacleTest");
        try
        {
            Vector2 before = actor.Feet;
            obstacle.transform.position = before + Vector2.right * 0.4f;
            obstacle.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f; Physics2D.SyncTransforms();
            var path = (System.Collections.Generic.List<Vector2>)typeof(NpcCompanionController).GetField("path", Private).GetValue(actor);
            path.Clear(); path.Add(before + Vector2.right);
            var repath = typeof(NpcCompanionController).GetField("repath", Private); repath.SetValue(actor, 0.5f);
            actor.Tick(0.05f); Assert.That(actor.Feet, Is.EqualTo(before)); Assert.That(path, Is.Empty);
            Assert.That((float)repath.GetValue(actor), Is.GreaterThanOrEqualTo(0.6f));
            actor.Tick(0.05f); Assert.That((float)repath.GetValue(actor), Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(path, Is.Empty);
        }
        finally { UnityEngine.Object.DestroyImmediate(obstacle); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator APoliteCompletionClaimFromMockAiIsNeverDisplayedOrExecuted()
    {
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        var provider = new FakeCompanionProvider(); DialogueProviders.Override = provider;
        Assert.That(actor.RequestFollow(), Is.True, "Pronoun-only work reports are restricted while work is active.");
        StartChat(); int calls = 0;
        foreach (string text in new[] { "ฉันเปิดให้เรียบร้อยค่ะ", "I have already opened it for you.",
            "We have already finished it for you.", "I just opened it for you.", "ฉันทำให้เรียบร้อยแล้วค่ะ", "Opened it for you." })
        {
            provider.Text = text; Send("คุยเป็นเพื่อนหน่อย"); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(++calls));
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain(text));
            var display = (TMPro.TMP_Text)typeof(DialogueManager).GetField("dialogueText", Private).GetValue(DialogueManager.Instance);
            Assert.That(display.text, Does.Not.Contain(text));
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
            Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.IsCompleted(station), Is.False);
            Assert.That(State.HasItem("key"), Is.False);
        }
        provider.Text = "ตามเรามาหน่อย"; Send("สวัสดี"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), "An AI reply must not be parsed as a player command.");
    }

    [UnityTest]
    public IEnumerator SampledReachableDestinationsIncludingTheReviewedCornerConvergeSafely()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector2 playerSize = (Vector2)player.GetComponent<Collider2D>().bounds.size + Vector2.one * 0.1f;
        var clearMethod = typeof(NpcCompanionController).GetMethod("IsClear", Private);
        Func<Vector2, Vector2, bool> clear = (from, to) => (bool)clearMethod.Invoke(actor, new object[] { from, to });
        var route = new System.Collections.Generic.List<Vector2>();
        var targets = new System.Collections.Generic.List<Vector2>();
        for (float y = -3.7f; y <= 1.3f; y += 1.5f)
            for (float x = -6.5f; x <= 6.5f; x += 2f) targets.Add(new Vector2(x, y));
        targets.AddRange(new[] { new Vector2(-7.1f, 0.8f), new Vector2(-6.9f, 1.2f), new Vector2(-6.5f, 1.2f), new Vector2(-6.2f, 1.2f) });
        int sampled = 0; bool reviewedCorner = false; Vector2 start = station.companionPoint;
        foreach (Vector2 target in targets)
        {
            if (Vector2.Distance(start, target) < 3f || !actor.IsFree(target)) continue;
            bool fits = !Physics2D.OverlapBoxAll(target, playerSize, 0f).Any(c => !c.isTrigger &&
                c.transform != player.transform && !c.transform.IsChildOf(player.transform));
            if (!fits || !CompanionNavigator.TryFollowPath(start, target, actor.Scenario.followDistance,
                actor.Scenario.walkBounds, actor.Scenario.cellSize, actor.IsFree, clear, route)) continue;
            ActorAt(start); PlayerAt(start + Vector2.right); Assert.That(actor.RequestFollow(), Is.True);
            PlayerAt(target); Vector2 previous = actor.Feet;
            for (int i = 0; i < 500 && Vector2.Distance(actor.Feet, target) > actor.Scenario.followDistance + 0.2f; i++)
            {
                actor.Tick(0.05f); Physics2D.SyncTransforms();
                Assert.That(clear(previous, actor.Feet), Is.True, "Collision en route to " + target);
                Assert.That(Vector2.Distance(previous, actor.Feet), Is.LessThanOrEqualTo(0.23f));
                Assert.That(Vector2.Distance(actor.Feet, target), Is.GreaterThanOrEqualTo(actor.Scenario.followDistance - 0.01f));
                previous = actor.Feet;
            }
            Assert.That(Vector2.Distance(actor.Feet, target), Is.LessThanOrEqualTo(actor.Scenario.followDistance + 0.21f), target.ToString("F4"));
            reviewedCorner |= Vector2.Distance(target, new Vector2(-6.5f, 0.8f)) < 0.001f;
            sampled++;
        }
        Assert.That(sampled, Is.GreaterThanOrEqualTo(13)); Assert.That(reviewedCorner, Is.True, "Do not silently exclude the confirmed failure.");
        yield return null;
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
    private MiniEventData ReviewEvent(NpcEventController controller)
    {
        var configured = (System.Collections.Generic.List<MiniEventData>)typeof(NpcEventController).GetField("events", Private).GetValue(controller);
        var review = configured.Single(e => e != null && e.eventId == "alice_review_failure");
        // Keep this integration test independent of unrelated story beats.
        typeof(NpcEventController).GetField("events", Private).SetValue(controller,
            new System.Collections.Generic.List<MiniEventData> { review });
        return review;
    }
    private IEnumerator WaitForReview(NpcEventController controller)
    {
        float deadline = Time.realtimeSinceStartup + 2f;
        while (!controller.HasPendingEvent && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(controller.HasPendingEvent, Is.True, "Allow the existing one-second story check to run.");
    }
    [UnityTest]
    public IEnumerator WitnessedFailureInvitesFreeTextReviewOnlyWhenPlayerChoosesToTalk()
    {
        var controller = actor.GetComponent<NpcEventController>(); var review = ReviewEvent(controller);
        controller.enabled = true;
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint);
        State.SetFlag("found_note"); int score = State.GetRelationship("Alice"); int journal = State.GetJournal().Count;
        Assert.That(review.CanTrigger(State), Is.False);
        Drawer().Interact(); Submit("0000"); yield return null;
        Assert.That(KeypadLockUI.IsOpen, Is.True, "An invitation must not interrupt the keypad.");
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        KeypadLockUI.Close(); yield return WaitForReview(controller);
        Assert.That(controller.HasPendingEvent, Is.True);
        Assert.That(DialogueManager.IsDialogueOpen, Is.False, "An invitation is not forced dialogue.");
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score));
        Assert.That(State.GetJournal().Count, Is.EqualTo(journal)); Assert.That(State.HasFlag(review.CompletedFlag), Is.False);
        actor.GetComponent<NPCInteraction>().Interact();
        Assert.That(DialogueManager.IsDialogueOpen, Is.True);
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(review.dialogue.lines[0]));
        Assert.That(review.dialogue.choices, Is.Empty);
        Assert.That(((GameObject)typeof(DialogueManager).GetField("chatComposerObject", Private).GetValue(DialogueManager.Instance)).activeSelf, Is.True);
        Assert.That(State.HasFlag(review.CompletedFlag), Is.True);
        Send("ยังเปิดไม่ออกเลย"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(actor.Scenario.observations.First(o => o.id == "drawer_failed").followUpLine));
        Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
        DialogueManager.Instance.HideDialogue(); yield return null;
        Assert.That(controller.HasPendingEvent, Is.False); Assert.That(review.CanTrigger(State), Is.False);
        State.RestoreSnapshot(State.CreateSnapshot());
        Assert.That(review.CanTrigger(State), Is.False, "Save/restore keeps the read receipt.");
    }
    [UnityTest]
    public IEnumerator ReviewDoesNotInventWitnessesOrPunishIgnoringAndExpiresAfterSuccess()
    {
        var controller = actor.GetComponent<NpcEventController>(); var review = ReviewEvent(controller);
        StartChat(); Send("ลองแล้วไม่สำเร็จ"); yield return null; DialogueManager.Instance.HideDialogue();
        Assert.That(review.CanTrigger(State), Is.False, "A chat claim is not a witnessed attempt.");
        PlayerAt(station.playerPoint); ActorAt(station.companionPoint); State.SetFlag("found_note");
        Drawer().Interact(); Submit("0000"); KeypadLockUI.Close();
        controller.enabled = true; yield return WaitForReview(controller);
        int score = State.GetRelationship("Alice"); int journal = State.GetJournal().Count;
        typeof(NpcEventController).GetField("pendingUntil", Private).SetValue(controller, Time.time - 1f);
        typeof(NpcEventController).GetMethod("Update", Private).Invoke(controller, null);
        Assert.That(controller.HasPendingEvent, Is.False); Assert.That(State.HasFlag(review.CompletedFlag), Is.False);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(score)); Assert.That(State.GetJournal().Count, Is.EqualTo(journal));
        ((System.Collections.Generic.Dictionary<string, float>)typeof(NpcEventController)
            .GetField("cooldownUntil", Private).GetValue(controller)).Clear();
        typeof(NpcEventController).GetField("nextStoryCheckTime", Private).SetValue(controller, 0f);
        typeof(NpcEventController).GetMethod("Update", Private).Invoke(controller, null);
        Assert.That(controller.HasPendingEvent, Is.True);
        Drawer().Interact(); Submit("4592"); RoomTransitionManager.CloseRoomOverlays();
        Assert.That(review.CanTrigger(State), Is.False);
        Assert.That(controller.TryStartPendingEvent(), Is.False);
        Assert.That(controller.HasPendingEvent, Is.False);
        State.ResetState(); Assert.That(review.CanTrigger(State), Is.False);
        State.SetFlag(actor.Scenario.Flag("observed.drawer_failed")); State.SetCurrentScene("Room02");
        Assert.That(review.CanTrigger(State), Is.False);
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
        Assert.That(grounded.reply, Does.Contain(station.completedStatusLine)); yield return null;
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
        Assert.That(context.ActiveNotes.Any(n => n.Contains("ข้อความ AI ไม่ใช่คำสั่ง")), Is.True);
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
