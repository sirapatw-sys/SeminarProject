using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GameplayLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private class Provider : IAiDialogueProvider
    {
        public bool Ready;
        public bool Hang;
        public int Calls;
        public int Disposals;
        public Action<GeneratedDialogueContent> Callback;
        public bool CanGenerate { get { return Ready; } }
        public string LastError { get { return string.Empty; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done)
        {
            Calls++; Callback = done;
            try
            {
                if (Hang) yield return new WaitForSecondsRealtime(60f);
                else done(null);
            }
            finally { Disposals++; }
        }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null)
        { done(null); yield break; }
    }

    private Provider provider;
    private RoomTransitionManager transition;
    private GameObject eventHost;
    private NpcEventController controller;
    private MiniEventData ambient;
    private MiniEventData story;
    private bool originalPersistence;
    private bool originalTitle;
    private bool originalIntro;
    private float originalMessageDuration;
    private readonly List<NpcEventController> disabledEvents = new List<NpcEventController>();

    private static void Title(bool open) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, open); }
    private static void Intro(bool playing) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, playing); }
    private void Set(string field, object value) { typeof(NpcEventController).GetField(field, Private).SetValue(controller, value); }
    private void Tick() { typeof(NpcEventController).GetMethod("Update", Private).Invoke(controller, null); }
    private void Queue() { typeof(NpcEventController).GetMethod("QueueEvent", Private).Invoke(controller, new object[] { ambient }); }
    private static bool PanelVisible(AiSettingsPanel panel)
    { return (bool)typeof(AiSettingsPanel).GetField("isOpen", Private).GetValue(panel); }
    private void QuietEvents()
    {
        foreach (var item in UnityEngine.Object.FindObjectsOfType<NpcEventController>())
            if (item.enabled) { disabledEvents.Add(item); item.enabled = false; }
    }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        originalPersistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        originalTitle = TitleMenu.IsOpen; originalIntro = IntroSequence.IsPlaying;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        provider = new Provider(); DialogueProviders.Override = provider;
        GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        // Isolate scene managers so each test begins like a fresh game boot.
        if (GameState.Instance != null) UnityEngine.Object.DestroyImmediate(GameState.Instance.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays(); QuietEvents();
        transition = RoomTransitionManager.Instance;
        originalMessageDuration = (float)typeof(RoomTransitionManager).GetField("messageDisplayDuration", Private).GetValue(transition);
        typeof(RoomTransitionManager).GetField("messageDisplayDuration", Private).SetValue(transition, 0f);
#if UNITY_EDITOR
        var source = UnityEditor.AssetDatabase.LoadAssetAtPath<MiniEventData>("Assets/Data/Events/Alice_Chatter_Event.asset");
#else
        MiniEventData source = null;
#endif
        Assert.That(source, Is.Not.Null); ambient = UnityEngine.Object.Instantiate(source);
        eventHost = new GameObject("GameplayLifecycleTestEvent"); controller = eventHost.AddComponent<NpcEventController>();
        Set("events", new List<MiniEventData> { ambient }); Set("triggerChancePerCheck", 1f);
    }

    [TearDown]
    public void Cleanup()
    {
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (eventHost != null) UnityEngine.Object.DestroyImmediate(eventHost);
        if (ambient != null) UnityEngine.Object.DestroyImmediate(ambient);
        if (story != null) UnityEngine.Object.DestroyImmediate(story);
        foreach (var item in disabledEvents) if (item != null) item.enabled = true;
        disabledEvents.Clear();
        if (transition != null)
        {
            typeof(RoomTransitionManager).GetField("messageDisplayDuration", Private).SetValue(transition, originalMessageDuration);
            if (RoomTransitionManager.IsBusy) UnityEngine.Object.DestroyImmediate(transition.gameObject);
        }
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = originalPersistence; Title(originalTitle); Intro(originalIntro);
    }

    private IEnumerator WaitForRoom(string name)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (RoomTransitionManager.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(RoomTransitionManager.IsBusy, Is.False);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name)); QuietEvents();
    }

    [UnityTest]
    public IEnumerator NewGameInSameSceneReappliesAuthoredMetricsImmediately()
    {
        var state = GameState.Instance;
        state.SetNpcNeed("Alice", "thirst", 2f); state.SetNpcEmotion("Alice", "homesickness", 3f);
        state.ResetState();
        Assert.That(state.GetNpcNeed("Alice", "thirst"), Is.EqualTo(60f));
        Assert.That(state.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(62f));
        yield return null;
        Assert.That(state.GetNpcNeed("Alice", "thirst"), Is.GreaterThanOrEqualTo(60f));
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room01"));
    }

    [UnityTest]
    public IEnumerator SaveLoadKeepsSavedMetricsInsteadOfSceneDefaults()
    {
        var save = new StateSnapshot { CurrentSceneId = "Room02" };
        save.NpcNeeds.Add(new NpcNeedSnapshot { NpcId = "Alice", NeedId = "thirst", Value = 93f });
        save.NpcEmotions.Add(new NpcEmotionSnapshot { NpcId = "Alice", EmotionId = "homesickness", Value = 77f });
        Assert.That(SaveSystem.TryLoadSnapshot(save), Is.True); yield return WaitForRoom("Room02");
        Assert.That(GameState.Instance.GetNpcNeed("Alice", "thirst"), Is.GreaterThanOrEqualTo(93f));
        Assert.That(GameState.Instance.GetNpcEmotion("Alice", "homesickness"), Is.GreaterThanOrEqualTo(77f));
    }

    [UnityTest]
    public IEnumerator MenuDoesNotGenerateEvenAfterTheAuthoredFortySecondWait()
    {
        provider.Ready = true; Title(true);
        float thirst = GameState.Instance.GetNpcNeed("Alice", "thirst");
        float homesick = GameState.Instance.GetNpcEmotion("Alice", "homesickness");
        yield return new WaitForSecondsRealtime(ambient.minimumRoomTimeSeconds + 0.2f);
        Set("nextCheckTime", 0f); Tick(); Queue();
        Assert.That(provider.Calls, Is.Zero); Assert.That(controller.HasPendingEvent, Is.False);
        Assert.That(GameState.Instance.GetNpcNeed("Alice", "thirst"), Is.EqualTo(thirst));
        Assert.That(GameState.Instance.GetNpcEmotion("Alice", "homesickness"), Is.EqualTo(homesick));
    }

    [UnityTest]
    public IEnumerator IntroDoesNotStartNewRequestsOrTickNpcMetrics()
    {
        ambient.minimumRoomTimeSeconds = 0f; provider.Ready = true; Intro(true);
        float need = GameState.Instance.GetNpcNeed("Alice", "thirst");
        Tick(); Queue(); yield return null;
        Assert.That(provider.Calls, Is.Zero);
        Assert.That(GameState.Instance.GetNpcNeed("Alice", "thirst"), Is.EqualTo(need));
    }

    [UnityTest]
    public IEnumerator TransitionDoesNotStartNewEventRequests()
    {
        ambient.minimumRoomTimeSeconds = 0f; provider.Ready = true;
        Assert.That(transition.TryTransitionToRoom("Room02", "Test", 0.05f), Is.True);
        Tick(); Queue(); yield return WaitForRoom("Room02"); Assert.That(provider.Calls, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ActiveGameplayStillGeneratesSoftInvitations()
    {
        ambient.minimumRoomTimeSeconds = 0f; provider.Ready = true;
        Assert.That(InputGate.IsGameplayActive, Is.True);
        Assert.That(ambient.CanTrigger(GameState.Instance), Is.True);
        // Setup may span a frame, allowing Update to schedule its next poll
        // while the authored minimum room time still made the event ineligible.
        Debug.Log("Active invitation poll: next=" + typeof(NpcEventController).GetField("nextCheckTime", Private).GetValue(controller) + ", now=" + Time.time);
        Set("nextCheckTime", 0f); Set("nextStoryCheckTime", 0f);
        // Earlier tests may have just ended an ordinary "!", which starts the quiet gaps.
        Set("nextAmbientTime", 0f);
        typeof(NpcEventController).GetField("nextSharedAmbientTime", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 0f);
        Tick(); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(controller.HasPendingEvent, Is.True);
        Assert.That(DialogueManager.IsDialogueOpen, Is.False, "An invitation does not interrupt play.");
    }

    [UnityTest]
    public IEnumerator ExistingRequestStillTimesOutAndDisposesWhileMenuIsOpen()
    {
        ambient.minimumRoomTimeSeconds = 0f; provider.Ready = true; provider.Hang = true;
        Set("generationTimeoutSeconds", 0.05f); Queue(); Title(true);
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(provider.Calls, Is.EqualTo(1)); Assert.That(provider.Disposals, Is.EqualTo(1));
        Assert.That((bool)typeof(NpcEventController).GetField("generationInProgress", Private).GetValue(controller), Is.False);
        Assert.That(controller.HasPendingEvent, Is.True);
        Assert.That(controller.TryStartPendingEvent(), Is.False);
        Set("pendingUntil", -1f); Tick(); Assert.That(controller.HasPendingEvent, Is.False);
        Assert.That(provider.Calls, Is.EqualTo(1));
        provider.Callback(null); Assert.That(controller.HasPendingEvent, Is.False);
    }

    [UnityTest]
    public IEnumerator PendingAmbientCannotStartStoryGenerationWhileMenuIsOpen()
    {
        ambient.minimumRoomTimeSeconds = 0f; provider.Ready = true; Queue();
        Assert.That(controller.HasPendingEvent, Is.True);
        story = UnityEngine.Object.Instantiate(ambient); story.eventId = "lifecycle_story"; story.storyBeat = true;
        Set("events", new List<MiniEventData> { story }); Title(true); Set("nextStoryCheckTime", 0f); Tick();
        Assert.That(provider.Calls, Is.EqualTo(1));
        Title(false); Set("nextStoryCheckTime", 0f); Tick(); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(2), "Story preemption still works after gameplay resumes.");
    }

    [UnityTest]
    public IEnumerator DirectSceneLoadsKeepTheSurvivingAiModalOwnerConsistent()
    {
        var owner = UnityEngine.Object.FindObjectOfType<AiSettingsPanel>();
        AiSettingsPanel.Open(); Assert.That(PanelVisible(owner), Is.True);
        foreach (string room in new[] { "Room02", "Room03" })
        {
            yield return SceneManager.LoadSceneAsync(room); yield return null; QuietEvents();
            Assert.That(owner, Is.Not.Null); Assert.That(owner.isActiveAndEnabled, Is.True);
            Assert.That(AiSettingsPanel.IsOpen, Is.True); Assert.That(PanelVisible(owner), Is.True);
            Assert.That(InputGate.IsBlocked, Is.True);
            Assert.That(typeof(AiSettingsPanel).GetField("active", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null), Is.SameAs(owner));
        }
        AiSettingsPanel.Close(); Assert.That(AiSettingsPanel.IsOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator NormalTransitionClosesAiAndIgnoresOpeningUntilReady()
    {
        var owner = UnityEngine.Object.FindObjectOfType<AiSettingsPanel>(); AiSettingsPanel.Open();
        Assert.That(transition.TryTransitionToRoom("Room02", "Test", 0.05f), Is.True);
        Assert.That(AiSettingsPanel.IsOpen, Is.False); AiSettingsPanel.Open();
        Assert.That(AiSettingsPanel.IsOpen, Is.False); Assert.That(PanelVisible(owner), Is.False);
        yield return WaitForRoom("Room02"); Assert.That(InputGate.IsBlocked, Is.False);
        AiSettingsPanel.Open(); Assert.That(AiSettingsPanel.IsOpen, Is.True); Assert.That(PanelVisible(owner), Is.True);
        Assert.That(InputGate.IsBlocked, Is.True); AiSettingsPanel.Close(); Assert.That(InputGate.IsBlocked, Is.False);
    }

    [UnityTest]
    public IEnumerator DestroyingDuplicatePanelCannotClearTheOwnerModalOrPrompt()
    {
        AiSettingsPanel.Open(); AiSettingsPanel.SetInteractionPrompt("owner prompt");
        var duplicateHost = new GameObject("DuplicateAiPanel");
        try
        {
            var duplicate = duplicateHost.AddComponent<AiSettingsPanel>();
            Assert.That(duplicate.enabled, Is.False); UnityEngine.Object.DestroyImmediate(duplicate);
            Assert.That(AiSettingsPanel.IsOpen, Is.True); Assert.That(InputGate.IsBlocked, Is.True);
            Assert.That(AiSettingsPanel.InteractionPrompt, Is.EqualTo("owner prompt"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(duplicateHost);
            AiSettingsPanel.ClearInteractionPrompt("owner prompt");
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisablingOwnerReleasesModalAndReenableStartsClosed()
    {
        var owner = UnityEngine.Object.FindObjectOfType<AiSettingsPanel>(); AiSettingsPanel.Open(); owner.enabled = false;
        Assert.That(AiSettingsPanel.IsOpen, Is.False); Assert.That(PanelVisible(owner), Is.False);
        Assert.That(InputGate.IsBlocked, Is.False); owner.enabled = true; AiSettingsPanel.Open();
        Assert.That(AiSettingsPanel.IsOpen, Is.True); Assert.That(PanelVisible(owner), Is.True); yield return null;
    }
}
