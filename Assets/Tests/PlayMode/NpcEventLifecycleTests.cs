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

public class NpcEventLifecycleTests
{
    private enum Behaviour { Abandon, Hang, NullIterator, FactoryThrow, NestedThrow, Immediate }
    private class Provider : IAiDialogueProvider
    {
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return string.Empty; } }
        public Behaviour Mode;
        public Action<GeneratedDialogueContent> Callback;
        public int Calls;
        public int Disposals;
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> complete)
        {
            Calls++; Callback = complete;
            if (Mode == Behaviour.FactoryThrow) throw new InvalidOperationException();
            if (Mode == Behaviour.NullIterator) return null;
            return Run(complete);
        }
        private IEnumerator Run(Action<GeneratedDialogueContent> complete)
        {
            try
            {
                if (Mode == Behaviour.Immediate) { complete(Content("บทจาก AI")); yield break; }
                if (Mode == Behaviour.NestedThrow) { yield return ThrowNested(); yield break; }
                if (Mode == Behaviour.Hang) yield return new WaitForSeconds(60f);
                else yield return null;
            }
            finally { Disposals++; }
        }
        private static IEnumerator ThrowNested()
        { yield return null; throw new InvalidOperationException(); }
        public IEnumerator GenerateReply(string npc, string name, string context, string message,
            Action<GeneratedChatReply> complete, string personality = null)
        { complete(null); yield break; }
    }

    private GameObject host;
    private NpcEventController controller;
    private MiniEventData ambient;
    private DialogueData dialogue;
    private Provider provider;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static GeneratedDialogueContent Content(string line)
    {
        return new GeneratedDialogueContent { lines = new[] { line },
            choices = Array.Empty<GeneratedDialogueChoice>(), referencedFactIds = Array.Empty<string>() };
    }
    private object Field(string name) { return typeof(NpcEventController).GetField(name, Private).GetValue(controller); }
    private void SetField(string name, object value) { typeof(NpcEventController).GetField(name, Private).SetValue(controller, value); }
    private void Queue(MiniEventData data)
    { typeof(NpcEventController).GetMethod("QueueEvent", Private).Invoke(controller, new object[] { data }); }
    private void AssertPending(MiniEventData data, string line = null)
    {
        Assert.That(controller.HasPendingEvent, Is.True);
        Assert.That(Field("pendingEvent"), Is.SameAs(data));
        Assert.That((bool)Field("generationInProgress"), Is.False);
        if (line != null) Assert.That(((GeneratedDialogueContent)Field("pendingDialogue")).lines[0], Is.EqualTo(line));
        Assert.That(GameState.Instance.HasFlag(data.CompletedFlag), Is.False, "Queuing must not complete an event.");
    }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SaveSystem.PersistenceEnabled = false;
        GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        provider = new Provider(); DialogueProviders.Override = provider;
        if (DialogueManager.Instance != null) DialogueManager.Instance.HideDialogue();
        if (GameState.Instance != null) GameState.Instance.ResetState();
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        DialogueManager.Instance.HideDialogue(); GameState.Instance.ResetState();
        GameState.Instance.SetCurrentScene("Room01");
        foreach (var existing in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) existing.enabled = false;
        host = new GameObject("EventLifecycleTest"); controller = host.AddComponent<NpcEventController>();
        SetField("triggerChancePerCheck", 0f);
        dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.speakerId = "Alice"; dialogue.speakerName = "Alice"; dialogue.dialogueId = "lifecycle_test";
        dialogue.lines.Add("บทสำรอง");
        ambient = ScriptableObject.CreateInstance<MiniEventData>();
        ambient.eventId = "lifecycle_test"; ambient.npcId = "Alice"; ambient.dialogue = dialogue;
        ambient.triggerType = MiniEventTriggerType.RandomAmbient; ambient.useAiDialogue = true;
    }

    [TearDown]
    public void Cleanup()
    {
        if (DialogueManager.Instance != null) DialogueManager.Instance.HideDialogue();
        if (controller != null) controller.enabled = false;
        if (host != null) UnityEngine.Object.Destroy(host);
        if (ambient != null) UnityEngine.Object.Destroy(ambient);
        if (dialogue != null) UnityEngine.Object.Destroy(dialogue);
        DialogueProviders.Override = null; GameDefinition.Override = null;
        SaveSystem.PersistenceEnabled = true; KnowledgeLibrary.ClearCache();
    }

    [UnityTest]
    public IEnumerator ProviderEndingWithoutCallbackUsesOfflineAndReleasesRequest()
    {
        Queue(ambient); yield return null; yield return null;
        AssertPending(ambient, "บทสำรอง");
        Assert.That(provider.Disposals, Is.EqualTo(1));
        provider.Callback(Content("คำตอบเก่า"));
        AssertPending(ambient, "บทสำรอง");
    }

    [UnityTest]
    public IEnumerator NullIteratorUsesOfflineImmediately()
    {
        provider.Mode = Behaviour.NullIterator; Queue(ambient); yield return null;
        AssertPending(ambient, "บทสำรอง");
    }

    [UnityTest]
    public IEnumerator FactoryExceptionUsesOfflineImmediately()
    {
        provider.Mode = Behaviour.FactoryThrow;
        LogAssert.Expect(LogType.Warning, "NPC event provider failed: InvalidOperationException");
        Queue(ambient); yield return null; AssertPending(ambient, "บทสำรอง");
    }

    [UnityTest]
    public IEnumerator NestedCoroutineExceptionUsesOfflineAndDisposesRequest()
    {
        provider.Mode = Behaviour.NestedThrow;
        LogAssert.Expect(LogType.Warning, "NPC event provider failed: InvalidOperationException");
        Queue(ambient); yield return null; yield return null;
        AssertPending(ambient, "บทสำรอง"); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator HangingProviderTimesOutIndependentlyOfPausedGameTime()
    {
        provider.Mode = Behaviour.Hang; SetField("generationTimeoutSeconds", 0.05f);
        float previous = Time.timeScale;
        try
        {
            Time.timeScale = 0f; Queue(ambient);
            yield return new WaitForSecondsRealtime(0.15f);
            AssertPending(ambient, "บทสำรอง");
            Assert.That(provider.Disposals, Is.EqualTo(1));
            provider.Callback(Content("คำตอบสาย")); AssertPending(ambient, "บทสำรอง");
        }
        finally { Time.timeScale = previous; }
    }

    [UnityTest]
    public IEnumerator AuthoredStoryBeatPreemptsHangingAmbientAndIgnoresItsLateCallback()
    {
        provider.Mode = Behaviour.Hang;
#if UNITY_EDITOR
        var kept = UnityEditor.AssetDatabase.LoadAssetAtPath<MiniEventData>("Assets/Data/Events/Alice_PromiseKept_Event.asset");
#else
        MiniEventData kept = null;
#endif
        Assert.That(kept, Is.Not.Null);
        SetField("events", new List<MiniEventData> { kept, ambient });
        Queue(ambient); Action<GeneratedDialogueContent> stale = provider.Callback;
        var state = GameState.Instance;
        state.SetFlag("alice_evidence_arc.started"); state.SetFlag("alice_evidence_arc.promised");
        state.SetFlag("inspected_painting"); EvidenceShareResult result;
        Assert.That(EvidenceSharing.TryShare(state, "Alice", "painting_arrow", out result), Is.True);
        SetField("nextStoryCheckTime", 0f);
        yield return null; yield return null;
        AssertPending(kept); Assert.That(provider.Disposals, Is.EqualTo(1));
        stale(Content("คำตอบเก่า")); AssertPending(kept);
    }

    [UnityTest]
    public IEnumerator AbandonedAmbientDoesNotBlockNewlyEligiblePromiseStoryBeat()
    {
#if UNITY_EDITOR
        var kept = UnityEditor.AssetDatabase.LoadAssetAtPath<MiniEventData>("Assets/Data/Events/Alice_PromiseKept_Event.asset");
#else
        MiniEventData kept = null;
#endif
        Assert.That(kept, Is.Not.Null); SetField("events", new List<MiniEventData> { kept, ambient });
        Queue(ambient); yield return null; yield return null;
        var state = GameState.Instance; state.SetFlag("alice_evidence_arc.promised"); state.SetFlag("inspected_painting");
        EvidenceShareResult result;
        Assert.That(EvidenceSharing.TryShare(state, "Alice", "painting_arrow", out result), Is.True);
        SetField("nextStoryCheckTime", 0f); yield return null; yield return null;
        AssertPending(kept);
    }

    [UnityTest]
    public IEnumerator ReplacementRequestRejectsOldAndDuplicateCallbacks()
    {
        provider.Mode = Behaviour.Hang; Queue(ambient);
        Action<GeneratedDialogueContent> old = provider.Callback;
        Queue(ambient); Action<GeneratedDialogueContent> current = provider.Callback;
        old(Content("คำตอบเก่า"));
        Assert.That(controller.HasPendingEvent, Is.False);
        current(Content("คำตอบใหม่")); AssertPending(ambient, "คำตอบใหม่");
        current(Content("คำตอบซ้ำ")); old(Content("คำตอบเก่า"));
        AssertPending(ambient, "คำตอบใหม่"); yield return null;
        Assert.That(provider.Disposals, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator SynchronousCallbackDoesNotLeaveRequestLocked()
    {
        provider.Mode = Behaviour.Immediate; Queue(ambient); yield return null;
        AssertPending(ambient, "บทจาก AI"); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator DisabledControllerCancelsRequestAndRejectsCallbacksAfterReenable()
    {
        provider.Mode = Behaviour.Hang; Queue(ambient);
        Action<GeneratedDialogueContent> old = provider.Callback;
        controller.enabled = false;
        Assert.That(provider.Disposals, Is.EqualTo(1));
        old(Content("คำตอบเก่า")); Assert.That(controller.HasPendingEvent, Is.False);
        controller.enabled = true; old(Content("คำตอบเก่า"));
        Assert.That(controller.HasPendingEvent, Is.False);
        provider.Mode = Behaviour.Immediate; Queue(ambient); yield return null;
        AssertPending(ambient, "บทจาก AI");
    }

    [UnityTest]
    public IEnumerator ChangedConditionsDiscardResponseWithoutCompletingOrRewardingEvent()
    {
        ambient.conditions.Add(new ConditionRule { type = ConditionType.MissingFlag, targetId = "moved_on" });
        provider.Mode = Behaviour.Hang; Queue(ambient);
        GameState.Instance.SetFlag("moved_on");
        provider.Callback(Content("คำตอบเก่า")); yield return null;
        Assert.That(controller.HasPendingEvent, Is.False);
        Assert.That((bool)Field("generationInProgress"), Is.False);
        Assert.That(GameState.Instance.HasFlag(ambient.CompletedFlag), Is.False);
    }

    [UnityTest]
    public IEnumerator GeneratingStoryBeatDoesNotPreemptItselfEveryCheck()
    {
        provider.Mode = Behaviour.Hang; ambient.storyBeat = true;
        SetField("events", new List<MiniEventData> { ambient }); Queue(ambient);
        yield return new WaitForSeconds(1.1f);
        Assert.That(provider.Calls, Is.EqualTo(1));
        Assert.That((bool)Field("generationInProgress"), Is.True);
    }
}
