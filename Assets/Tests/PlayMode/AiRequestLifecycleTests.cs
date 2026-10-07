using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class AiRequestLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private enum Mode { Hang, Immediate, Abandon, Null, FactoryThrow, NestedThrow, Duplicate }
    private class Provider : IAiDialogueProvider
    {
        public bool Ready;
        public Mode Behaviour = Mode.Hang;
        public bool ReplyOnDispose;
        public bool ThrowOnDispose;
        public int Calls, Disposals, NestedDisposals;
        public readonly List<Action<GeneratedChatReply>> Callbacks = new List<Action<GeneratedChatReply>>();
        public bool CanGenerate { get { return Ready; } }
        public string LastError { get { return string.Empty; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done)
        { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null)
        {
            Calls++; Callbacks.Add(done);
            if (Behaviour == Mode.FactoryThrow) throw new InvalidOperationException();
            if (Behaviour == Mode.Null) return null;
            return Run(done);
        }
        private IEnumerator Run(Action<GeneratedChatReply> done)
        {
            try
            {
                if (Behaviour == Mode.Immediate || Behaviour == Mode.Duplicate)
                {
                    done(Reply("ยินดีที่ได้คุยด้วย"));
                    if (Behaviour == Mode.Duplicate) done(Reply("ข้อความซ้ำที่ไม่ควรแสดง"));
                    yield break;
                }
                if (Behaviour == Mode.NestedThrow) yield return Nested();
                else if (Behaviour == Mode.Abandon) yield return null;
                else yield return new WaitForSecondsRealtime(60f);
            }
            finally
            {
                Disposals++;
                if (ReplyOnDispose) done(Reply("คำตอบที่มาจากการยกเลิก"));
                if (ThrowOnDispose) throw new InvalidOperationException();
            }
        }
        private IEnumerator Nested()
        {
            try { yield return null; throw new InvalidOperationException(); }
            finally { NestedDisposals++; }
        }
    }
    private Provider provider;
    private DialogueData dialogue;
    private AiSettingsPanel panel;
    private AiDialogueGenerator generator;
    private RoomTransitionManager transition;
    private bool originalPersistence, originalTitle, originalIntro;
    private float originalMessageDuration;
    private readonly List<NpcEventController> disabledEvents = new List<NpcEventController>();
    private readonly Dictionary<string, object> panelFields = new Dictionary<string, object>();
    private readonly Dictionary<string, object> generatorFields = new Dictionary<string, object>();
    private readonly Dictionary<string, string> preferences = new Dictionary<string, string>();
    private readonly HashSet<string> missingPreferences = new HashSet<string>();
    private static readonly string[] PreferenceNames = { "ai.provider", "ai.model", "ai.endpoint", "ai.model.movedToTerra" };

    private static GeneratedChatReply Reply(string text)
    {
        return new GeneratedChatReply { reply = text, referencedFactIds = Array.Empty<string>(),
            relationshipDelta = 1, playerTone = "friendly" };
    }
    private static object Field(object target, string name)
    { return target.GetType().GetField(name, Private).GetValue(target); }
    private static void Set(object target, string name, object value)
    { target.GetType().GetField(name, Private).SetValue(target, value); }
    private static void Call(object target, string method, params object[] args)
    { target.GetType().GetMethod(method, Private).Invoke(target, args); }
    private static bool IntegerPreference(string name) { return name == "ai.provider" || name == "ai.model.movedToTerra"; }
    private string Text { get { return ((TMP_Text)Field(DialogueManager.Instance, "dialogueText")).text; } }
    private string Status { get { return (string)Field(panel, "status"); } }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        foreach (string name in PreferenceNames)
        {
            if (!PlayerPrefs.HasKey(name)) missingPreferences.Add(name);
            preferences[name] = IntegerPreference(name) ? PlayerPrefs.GetInt(name).ToString() : PlayerPrefs.GetString(name);
        }
        originalPersistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        originalTitle = TitleMenu.IsOpen; originalIntro = IntroSequence.IsPlaying;
        typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, false);
        typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, false);
        provider = new Provider(); DialogueProviders.Override = provider;
        GameDefinition.Override = null; KnowledgeLibrary.ClearCache(); RoomTransitionManager.CloseRoomOverlays();
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, false);
        typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, false);
        RoomTransitionManager.CloseRoomOverlays(); GameState.Instance.ResetState();
        foreach (var item in UnityEngine.Object.FindObjectsOfType<NpcEventController>())
            if (item.enabled) { disabledEvents.Add(item); item.enabled = false; }
        transition = RoomTransitionManager.Instance;
        originalMessageDuration = (float)Field(transition, "messageDisplayDuration");
        Set(transition, "messageDisplayDuration", 0f);
        panel = UnityEngine.Object.FindObjectOfType<AiSettingsPanel>(); generator = AiDialogueGenerator.Instance;
        foreach (string name in new[] { "providerIndex", "model", "endpoint", "apiKey", "fetchedModels", "typeModelByHand" })
            panelFields[name] = Field(panel, name);
        foreach (string name in new[] { "provider", "model", "apiUrl", "sessionApiKey" })
            generatorFields[name] = Field(generator, name);
        dialogue = ScriptableObject.CreateInstance<DialogueData>();
        dialogue.dialogueId = "request_lifecycle_test"; dialogue.speakerId = "Alice";
        dialogue.speakerName = "Alice"; dialogue.lines.Add("บททดสอบ");
    }

    [TearDown]
    public void Cleanup()
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.enabled = true;
            DialogueManager.Instance.HideDialogue();
        }
        if (panel != null)
        {
            panel.enabled = true; AiSettingsPanel.Close();
            foreach (var entry in panelFields) Set(panel, entry.Key, entry.Value);
        }
        if (generator != null) foreach (var entry in generatorFields) Set(generator, entry.Key, entry.Value);
        panelFields.Clear(); generatorFields.Clear();
        if (transition != null)
        {
            Set(transition, "messageDisplayDuration", originalMessageDuration);
            if (RoomTransitionManager.IsBusy) UnityEngine.Object.DestroyImmediate(transition.gameObject);
        }
        provider.Ready = false;
        foreach (var item in disabledEvents) if (item != null) item.enabled = true;
        disabledEvents.Clear();
        if (dialogue != null) UnityEngine.Object.DestroyImmediate(dialogue);
        foreach (string name in PreferenceNames)
        {
            if (missingPreferences.Contains(name)) PlayerPrefs.DeleteKey(name);
            else if (IntegerPreference(name)) PlayerPrefs.SetInt(name, int.Parse(preferences[name]));
            else PlayerPrefs.SetString(name, preferences[name]);
        }
        PlayerPrefs.Save(); preferences.Clear(); missingPreferences.Clear();
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = originalPersistence;
        typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, originalTitle);
        typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, originalIntro);
    }

    private void Send()
    {
        provider.Ready = true;
        DialogueManager.Instance.StartDialogue(dialogue, null, isEvent: true);
        var input = (TMP_InputField)Field(DialogueManager.Instance, "chatInput");
        input.text = "วันนี้รู้สึกอย่างไร"; DialogueManager.Instance.SendTypedMessage();
    }
    private void AssertChatReleased(int disposals = 1)
    {
        Assert.That((bool)Field(DialogueManager.Instance, "chatRequestInProgress"), Is.False);
        Assert.That(Field(DialogueManager.Instance, "chatOperation"), Is.Null);
        Assert.That(((TMP_InputField)Field(DialogueManager.Instance, "chatInput")).interactable, Is.True);
        Assert.That(provider.Disposals, Is.EqualTo(disposals));
    }
    private void Form()
    {
        Set(panel, "providerIndex", (int)AiProviderType.OpenAiCompatible);
        Set(panel, "endpoint", "http://127.0.0.1:1/v1/chat/completions");
        Set(panel, "model", "probe-model"); Set(panel, "apiKey", "dummy-lifecycle-test");
        AiSettingsPanel.Open(); provider.Ready = true;
    }
    private void TestConnection() { Form(); Call(panel, "TestConnection"); }
    private int PrimeModels()
    {
        AiSettingsPanel.Open();
        Set(panel, "providerIndex", (int)AiProviderType.KkuIntelsphere);
        Set(panel, "endpoint", AiDialogueGenerator.KkuChatCompletionsUrl);
        Set(panel, "apiKey", "dummy-lifecycle-test"); Set(panel, "model", AiDialogueGenerator.DefaultKkuModel);
        Set(panel, "fetchedModels", null);
        int version = (int)Field(panel, "modelFetchVersion") + 1;
        Set(panel, "modelFetchVersion", version); Set(panel, "modelFetchInProgress", true);
        var operation = new AiRequestOperation(panel); Set(panel, "modelFetchOperation", operation);
        operation.Start(() => provider.GenerateReply("model_probe", "", "", "", _ => { }), _ => { }, () => { });
        return version;
    }
    private void ModelResult(int version, string[] models, string error = null)
    {
        Call(panel, "CompleteModelFetch", version, (int)AiProviderType.KkuIntelsphere,
            AiDialogueGenerator.KkuChatCompletionsUrl, "dummy-lifecycle-test", models, error);
    }
    private static IntPtr Pointer(UnityWebRequest request)
    { return (IntPtr)typeof(UnityWebRequest).GetField("m_Ptr", Private).GetValue(request); }
    private void NativeConfig()
    {
        Set(generator, "provider", AiProviderType.OpenAiCompatible);
        Set(generator, "apiUrl", "http://127.0.0.1:1/v1/chat/completions");
        Set(generator, "sessionApiKey", "dummy-lifecycle-test"); Set(generator, "model", "probe-model");
    }

    [UnityTest] public IEnumerator ClosingChatDisposesTheAbandonedTypedRequest()
    { Send(); yield return null; DialogueManager.Instance.HideDialogue(); AssertChatReleased(); Assert.That(DialogueManager.IsDialogueOpen, Is.False); }

    [UnityTest] public IEnumerator TypedTimeoutDisposesTheAbandonedRequest()
    {
        Send(); yield return null;
        LogAssert.Expect(LogType.Warning, "Typed reply timed out; answering with the fallback.");
        Set(DialogueManager.Instance, "chatRequestDeadline", -1f); Call(DialogueManager.Instance, "Update");
        AssertChatReleased();
    }

    [UnityTest] public IEnumerator DisablingDialogueCancelsItsRequest()
    { Send(); yield return null; DialogueManager.Instance.enabled = false; AssertChatReleased(); Assert.That(DialogueManager.IsDialogueOpen, Is.False); }

    [UnityTest] public IEnumerator RoomTransitionCancelsTheTypedRequest()
    {
        Send(); yield return null;
        Assert.That(transition.TryTransitionToRoom("Room02", "Test", 0f), Is.True); AssertChatReleased();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (RoomTransitionManager.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(RoomTransitionManager.IsBusy, Is.False);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room02"));
    }

    [UnityTest] public IEnumerator ImmediateValidReplyCompletesAndDisposesSafely()
    { provider.Behaviour = Mode.Immediate; Send(); yield return null; AssertChatReleased(); Assert.That(Text, Does.Contain("ยินดีที่ได้คุยด้วย")); }

    [UnityTest] public IEnumerator OldCallbackCannotCompleteANewConversation()
    {
        Send(); yield return null; DialogueManager.Instance.HideDialogue(); Send();
        int before = GameState.Instance.GetRelationship("Alice");
        provider.Callbacks[0](Reply("คำตอบเก่า"));
        Assert.That((bool)Field(DialogueManager.Instance, "chatRequestInProgress"), Is.True);
        Assert.That(GameState.Instance.GetRelationship("Alice"), Is.EqualTo(before));
        provider.Callbacks[1](Reply("คำตอบใหม่")); AssertChatReleased(2);
        Assert.That(Text, Does.Contain("คำตอบใหม่")); Assert.That(Text, Does.Not.Contain("คำตอบเก่า"));
    }

    [UnityTest] public IEnumerator NullProviderIteratorUsesFallbackImmediately()
    { provider.Behaviour = Mode.Null; Send(); yield return null; AssertChatReleased(0); }

    [UnityTest] public IEnumerator ProviderEndingWithoutCallbackUsesFallback()
    { provider.Behaviour = Mode.Abandon; Send(); yield return null; yield return null; AssertChatReleased(); }

    [UnityTest] public IEnumerator FactoryExceptionUsesFallbackWithoutWaitingForTimeout()
    {
        provider.Behaviour = Mode.FactoryThrow;
        LogAssert.Expect(LogType.Warning, "Typed reply provider failed: InvalidOperationException");
        Send(); yield return null; AssertChatReleased(0);
    }

    [UnityTest] public IEnumerator NestedExceptionDisposesBothIteratorsAndUsesFallback()
    {
        provider.Behaviour = Mode.NestedThrow;
        LogAssert.Expect(LogType.Warning, "Typed reply provider failed: InvalidOperationException");
        Send(); yield return null; yield return null; AssertChatReleased();
        Assert.That(provider.NestedDisposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator ExternalCompletionReleasesAStillWaitingIterator()
    { Send(); yield return null; provider.Callbacks[0](Reply("คำตอบใหม่")); AssertChatReleased(); }

    [UnityTest] public IEnumerator CallbackFromDisposeCannotMutateTheClosedChat()
    {
        provider.ReplyOnDispose = true; Send(); yield return null;
        int before = GameState.Instance.GetRelationship("Alice");
        DialogueManager.Instance.HideDialogue(); AssertChatReleased();
        Assert.That(GameState.Instance.GetRelationship("Alice"), Is.EqualTo(before));
    }

    [UnityTest] public IEnumerator DisposalExceptionCannotLeaveTheChatLocked()
    {
        provider.ThrowOnDispose = true; Send(); yield return null;
        LogAssert.Expect(LogType.Warning, "AI request cleanup failed: InvalidOperationException");
        DialogueManager.Instance.HideDialogue(); AssertChatReleased();
    }

    [UnityTest] public IEnumerator DuplicateProviderCallbacksDoNotApplyTwice()
    {
        provider.Behaviour = Mode.Duplicate; Send(); yield return null; AssertChatReleased();
        Assert.That(Text, Does.Contain("ยินดีที่ได้คุยด้วย"));
        Assert.That(Text, Does.Not.Contain("ข้อความซ้ำที่ไม่ควรแสดง"));
    }

    [UnityTest] public IEnumerator ConnectionTestUsesTheCurrentFormAndProviderOverride()
    {
        TestConnection(); yield return null;
        Assert.That(generator.Provider, Is.EqualTo(AiProviderType.OpenAiCompatible));
        Assert.That(generator.ApiUrl, Is.EqualTo("http://127.0.0.1:1/v1/chat/completions"));
        Assert.That(generator.Model, Is.EqualTo("probe-model")); Assert.That(provider.Calls, Is.EqualTo(1));
        AiSettingsPanel.Close(); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator ClosingAiPanelCancelsItsConnectionTestAndDropsLateResults()
    {
        TestConnection(); yield return null; AiSettingsPanel.Close();
        string status = Status; provider.Callbacks[0](Reply("พร้อม"));
        Assert.That(Status, Is.EqualTo(status)); Assert.That(provider.Disposals, Is.EqualTo(1));
        Assert.That((bool)Field(panel, "testInProgress"), Is.False);
    }

    [UnityTest] public IEnumerator DisablingAiPanelCancelsItsRequest()
    { TestConnection(); yield return null; panel.enabled = false; Assert.That(provider.Disposals, Is.EqualTo(1)); Assert.That((bool)Field(panel, "testInProgress"), Is.False); }

    [UnityTest] public IEnumerator ChangingProviderCancelsTheConnectionTest()
    {
        TestConnection(); yield return null; Set(panel, "providerIndex", (int)AiProviderType.Gemini);
        Call(panel, "ApplyProviderDefaults");
        Assert.That(provider.Disposals, Is.EqualTo(1)); Assert.That((string)Field(panel, "apiKey"), Is.Empty);
        string status = Status; provider.Callbacks[0](Reply("เก่า")); Assert.That(Status, Is.EqualTo(status));
    }

    [UnityTest] public IEnumerator CurrentConnectionSuccessIsAcceptedAndDisposed()
    {
        TestConnection(); yield return null; provider.Callbacks[0](Reply("พร้อม"));
        Assert.That(Status, Does.Contain("เชื่อมต่อสำเร็จ — probe-model"));
        Assert.That(provider.Disposals, Is.EqualTo(1)); Assert.That((bool)Field(panel, "testInProgress"), Is.False);
    }

    [UnityTest] public IEnumerator ConnectionFactoryFailureReleasesTheBusyFlag()
    {
        provider.Behaviour = Mode.FactoryThrow; TestConnection(); yield return null;
        Assert.That((bool)Field(panel, "testInProgress"), Is.False); Assert.That(Status, Does.Contain("InvalidOperationException"));
    }

    [UnityTest] public IEnumerator ConnectionEndingWithoutCallbackReportsFailure()
    {
        provider.Behaviour = Mode.Abandon; TestConnection(); yield return null; yield return null;
        Assert.That((bool)Field(panel, "testInProgress"), Is.False);
        Assert.That(Status, Does.Contain("โดยไม่ส่งผลกลับ")); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator ChangedModelCannotShowAnOldConnectionSuccess()
    {
        TestConnection(); yield return null; Set(panel, "model", "another-model");
        provider.Callbacks[0](Reply("พร้อม")); Assert.That(Status, Does.Contain("การตั้งค่าเปลี่ยนแล้ว"));
        Assert.That(Status, Does.Not.Contain("เชื่อมต่อสำเร็จ")); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator CurrentModelListIsAcceptedAndDisposed()
    {
        int version = PrimeModels(); yield return null; ModelResult(version, new[] { "fresh-model" });
        Assert.That((string[])Field(panel, "fetchedModels"), Is.EqualTo(new[] { "fresh-model" }));
        Assert.That((bool)Field(panel, "modelFetchInProgress"), Is.False); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator LateModelsResponseCannotOverwriteAnotherProvidersList()
    {
        int version = PrimeModels(); yield return null; Set(panel, "providerIndex", (int)AiProviderType.Gemini);
        Call(panel, "ApplyProviderDefaults"); ModelResult(version, new[] { AiDialogueGenerator.DefaultKkuModel });
        var choices = (string[])typeof(AiSettingsPanel).GetMethod("ModelChoices", Private).Invoke(panel, new object[] { AiProviderType.Gemini });
        Assert.That(choices, Does.Not.Contain(AiDialogueGenerator.DefaultKkuModel));
        Assert.That(Field(panel, "fetchedModels"), Is.Null); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator ChangedKeyRejectsThePreviousAccountsModelList()
    {
        int version = PrimeModels(); yield return null; Set(panel, "apiKey", "another-dummy-key");
        ModelResult(version, new[] { "stale-model" });
        Assert.That(Field(panel, "fetchedModels"), Is.Null); Assert.That(provider.Disposals, Is.EqualTo(1));
        Assert.That((bool)Field(panel, "modelFetchInProgress"), Is.False);
    }

    [UnityTest] public IEnumerator ChangedEndpointRejectsTheOldModelList()
    {
        int version = PrimeModels(); yield return null; Set(panel, "endpoint", "http://127.0.0.1:1/other");
        ModelResult(version, new[] { "stale-model" });
        Assert.That(Field(panel, "fetchedModels"), Is.Null); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator SwitchingAwayAndBackStillInvalidatesTheOldModelList()
    {
        int version = PrimeModels(); yield return null; Set(panel, "providerIndex", (int)AiProviderType.Gemini);
        Call(panel, "ApplyProviderDefaults"); Set(panel, "providerIndex", (int)AiProviderType.KkuIntelsphere);
        Call(panel, "ApplyProviderDefaults"); Set(panel, "apiKey", "dummy-lifecycle-test");
        ModelResult(version, new[] { "stale-model" }); Assert.That(Field(panel, "fetchedModels"), Is.Null);
    }

    [UnityTest] public IEnumerator StaleModelCallbackCannotUnlockOrReplaceANewerRequest()
    {
        int old = PrimeModels(); yield return null; Call(panel, "CancelModelFetch");
        int current = PrimeModels(); ModelResult(old, new[] { "stale-model" });
        Assert.That((bool)Field(panel, "modelFetchInProgress"), Is.True);
        Assert.That(((AiRequestOperation)Field(panel, "modelFetchOperation")).IsRunning, Is.True);
        ModelResult(current, new[] { "fresh-model" });
        Assert.That((string[])Field(panel, "fetchedModels"), Is.EqualTo(new[] { "fresh-model" }));
        Assert.That(provider.Disposals, Is.EqualTo(2));
    }

    [UnityTest] public IEnumerator CurrentModelFailureReleasesItsRequest()
    {
        int version = PrimeModels(); yield return null; ModelResult(version, null, "test failure");
        Assert.That(Status, Does.Contain("test failure")); Assert.That(provider.Disposals, Is.EqualTo(1));
        Assert.That((bool)Field(panel, "modelFetchInProgress"), Is.False);
    }

    [UnityTest] public IEnumerator ClosingAiPanelCancelsItsModelFetchAndDropsLateResults()
    {
        int version = PrimeModels(); yield return null; AiSettingsPanel.Close();
        ModelResult(version, new[] { "stale-model" }); Assert.That(Field(panel, "fetchedModels"), Is.Null);
        Assert.That((bool)Field(panel, "modelFetchInProgress"), Is.False); Assert.That(provider.Disposals, Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator CancellingPostDisposesItsNativeUnityWebRequest()
    {
        NativeConfig(); IEnumerator routine = null; UnityWebRequest request = null;
        try
        {
            routine = (IEnumerator)typeof(AiDialogueGenerator).GetMethod("Post", Private).Invoke(generator,
                new object[] { "{}", new Action<UnityWebRequest>(_ => Assert.Fail("Cancelled request must not complete.")) });
            Assert.That(routine.MoveNext(), Is.True); request = ((UnityWebRequestAsyncOperation)routine.Current).webRequest;
            Assert.That(Pointer(request), Is.Not.EqualTo(IntPtr.Zero));
            ((IDisposable)routine).Dispose(); Assert.That(Pointer(request), Is.EqualTo(IntPtr.Zero));
        }
        finally { request?.Dispose(); (routine as IDisposable)?.Dispose(); }
        yield return null;
    }

    [UnityTest] public IEnumerator CancellingModelDiscoveryDisposesItsNativeRequest()
    {
        NativeConfig(); IEnumerator routine = null; UnityWebRequest request = null;
        try
        {
            routine = generator.FetchAvailableModels((_, __) => Assert.Fail("Cancelled request must not complete."));
            Assert.That(routine.MoveNext(), Is.True); request = ((UnityWebRequestAsyncOperation)routine.Current).webRequest;
            ((IDisposable)routine).Dispose(); Assert.That(Pointer(request), Is.EqualTo(IntPtr.Zero));
        }
        finally { request?.Dispose(); (routine as IDisposable)?.Dispose(); }
        yield return null;
    }

    [UnityTest] public IEnumerator FinishedPostTransfersAReadableResponseToItsCaller()
    {
        NativeConfig(); UnityWebRequest request = null, handedOff = null; IEnumerator routine = null;
        try
        {
            routine = (IEnumerator)typeof(AiDialogueGenerator).GetMethod("Post", Private).Invoke(generator,
                new object[] { "{}", new Action<UnityWebRequest>(sent => handedOff = sent) });
            Assert.That(routine.MoveNext(), Is.True);
            var pending = (UnityWebRequestAsyncOperation)routine.Current; request = pending.webRequest;
            yield return pending;
            Assert.That(routine.MoveNext(), Is.False); Assert.That(handedOff, Is.SameAs(request));
            Assert.That(Pointer(request), Is.Not.EqualTo(IntPtr.Zero)); Assert.That(request.error, Is.Not.Empty);
            request.Dispose(); Assert.That(Pointer(request), Is.EqualTo(IntPtr.Zero));
        }
        finally { request?.Dispose(); (routine as IDisposable)?.Dispose(); }
    }

    [UnityTest] public IEnumerator ReplyTransportFailureDisposesBeforeCallback()
    {
        NativeConfig(); GeneratedChatReply reply = Reply("sentinel"); IEnumerator routine = null, post = null; UnityWebRequest request = null;
        try
        {
            routine = generator.GenerateReply("Alice", "Alice", "", "วันนี้รู้สึกอย่างไร", value => reply = value);
            Assert.That(routine.MoveNext(), Is.True); post = (IEnumerator)routine.Current;
            Assert.That(post.MoveNext(), Is.True); var pending = (UnityWebRequestAsyncOperation)post.Current; request = pending.webRequest;
            yield return pending; Assert.That(post.MoveNext(), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("^AI typed reply failed; using local fallback\\."));
            Assert.That(routine.MoveNext(), Is.False); Assert.That(reply, Is.Null); Assert.That(Pointer(request), Is.EqualTo(IntPtr.Zero));
        }
        finally { request?.Dispose(); (post as IDisposable)?.Dispose(); (routine as IDisposable)?.Dispose(); }
    }
}
