using System;
using System.Collections;
using System.Reflection;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RuntimeSafetyPlayModeTests
{
    private class OfflineProvider : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return string.Empty; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> complete)
        { complete(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> complete, string personality = null)
        { complete(null); yield break; }
    }
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private RoomTransitionManager transition;
    private float originalMessageDuration;
    private bool originalPersistence;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        originalPersistence = SaveSystem.PersistenceEnabled;
        SaveSystem.PersistenceEnabled = false;
        GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        DialogueProviders.Override = new OfflineProvider();
        RoomTransitionManager.CloseRoomOverlays();
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        RoomTransitionManager.CloseRoomOverlays();
        GameState.Instance.ResetState();
        transition = RoomTransitionManager.Instance;
        originalMessageDuration = (float)typeof(RoomTransitionManager).GetField("messageDisplayDuration", Private).GetValue(transition);
        typeof(RoomTransitionManager).GetField("messageDisplayDuration", Private).SetValue(transition, 0f);
    }

    [TearDown]
    public void Cleanup()
    {
        RoomTransitionManager.CloseRoomOverlays();
        if (transition != null)
        {
            typeof(RoomTransitionManager).GetField("messageDisplayDuration", Private).SetValue(transition, originalMessageDuration);
            if (RoomTransitionManager.IsBusy) UnityEngine.Object.DestroyImmediate(transition.gameObject);
        }
        SaveSystem.PersistenceEnabled = originalPersistence;
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
    }

    private static IEnumerator WaitForTransition()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (RoomTransitionManager.IsBusy && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(RoomTransitionManager.IsBusy, Is.False, "Room transition must finish.");
    }
    private static TMP_Text DialogueText()
    { return (TMP_Text)typeof(DialogueManager).GetField("dialogueText", Private).GetValue(DialogueManager.Instance); }

    [UnityTest]
    public IEnumerator TransitionCancelsAllRoomOverlaysAndOldKeypadCallback()
    {
        bool oldCallbackCalled = false;
        KeypadLockUI.Show("1234", "Old room", null, () => oldCallbackCalled = true);
        ItemPopupUI.ShowItem("review_note", "Old room note", "Old note");
        typeof(JournalUI).GetProperty("IsOpen").SetValue(null, true);
        Assert.That(transition.TryTransitionToRoom("Room02", "Test", 0f), Is.True);
        Assert.That(KeypadLockUI.IsOpen, Is.False);
        Assert.That(ItemPopupUI.IsBusy, Is.False);
        Assert.That(JournalUI.IsOpen, Is.False);
        Assert.That(typeof(KeypadLockUI).GetField("onUnlockSuccess", Private).GetValue(KeypadLockUI.Instance), Is.Null);
        yield return WaitForTransition();
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room02"));
        Assert.That(GameState.Instance.GetCurrentScene(), Is.EqualTo("Room02"));
        Assert.That(InputGate.IsBlocked, Is.False);
        Assert.That(oldCallbackCalled, Is.False);
    }

    [UnityTest]
    public IEnumerator DirectSceneLoadAlsoClearsPersistentKeypad()
    {
        KeypadLockUI.Show("1234", onSuccess: () => Assert.Fail("Old callback must not run."));
        yield return SceneManager.LoadSceneAsync("Room02"); yield return null;
        Assert.That(KeypadLockUI.IsOpen, Is.False);
        Assert.That(typeof(KeypadLockUI).GetField("onUnlockSuccess", Private).GetValue(KeypadLockUI.Instance), Is.Null);
    }

    [UnityTest]
    public IEnumerator MissingSceneCannotChangeStateOrStartCheckpointTransition()
    {
        var state = GameState.Instance; state.SetFlag("keep_flag");
        string before = JsonUtility.ToJson(state.CreateSnapshot());
        Assert.That(transition.TryTransitionToRoom("Missing_Test_Scene", "Test", 0f), Is.False);
        Assert.That(JsonUtility.ToJson(state.CreateSnapshot()), Is.EqualTo(before));
        // Passive needs legitimately tick on subsequent frames, even after a rejected load.
        yield return null;
        Assert.That(state.HasFlag("keep_flag"), Is.True);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room01"));
        Assert.That(RoomTransitionManager.IsBusy, Is.False);
        Assert.That(transition.LastError, Is.Not.Empty);
    }

    [UnityTest]
    public IEnumerator InvalidSaveSceneIsRejectedWithoutChangingLiveGame()
    {
        var state = GameState.Instance; state.SetFlag("keep_flag");
        string before = JsonUtility.ToJson(state.CreateSnapshot());
        var snapshot = new StateSnapshot { CurrentSceneId = "Missing_Test_Scene" };
        Assert.That(SaveSystem.TryLoadSnapshot(snapshot), Is.False);
        Assert.That(JsonUtility.ToJson(state.CreateSnapshot()), Is.EqualTo(before));
        yield return null;
        Assert.That(state.HasFlag("keep_flag"), Is.True);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room01"));
        Assert.That(RoomTransitionManager.IsBusy, Is.False);
        Assert.That(SaveSystem.LastError, Is.Not.Empty);
    }

    [UnityTest]
    public IEnumerator MalformedSaveIsRejectedBeforeClosingCurrentModalOrChangingState()
    {
        var state = GameState.Instance; state.SetFlag("keep_flag"); state.SetRelationship("Alice", 75);
        string before = JsonUtility.ToJson(state.CreateSnapshot());
        KeypadLockUI.Show("1234");
        var snapshot = new StateSnapshot { CurrentSceneId = "Room02" };
        snapshot.Relationships.Add(new RelationshipSnapshot { NpcId = null, Value = 60 });
        Assert.That(SaveSystem.TryLoadSnapshot(snapshot), Is.False);
        Assert.That(JsonUtility.ToJson(state.CreateSnapshot()), Is.EqualTo(before));
        yield return null;
        Assert.That(state.HasFlag("keep_flag"), Is.True);
        Assert.That(KeypadLockUI.IsOpen, Is.True, "A rejected load leaves the old room intact.");
        Assert.That(RoomTransitionManager.IsBusy, Is.False);
    }

    [UnityTest]
    public IEnumerator ValidSaveRestoresBeforeSceneInitializationAndClosesKeypad()
    {
        KeypadLockUI.Show("1234", onSuccess: () => Assert.Fail("Old callback must not run."));
        var snapshot = new StateSnapshot { CurrentSceneId = "Room02" };
        snapshot.Flags.Add("sena_offering_given"); snapshot.Flags.Add("room02_door_unlocked");
        snapshot.Relationships.Add(new RelationshipSnapshot { NpcId = "Alice", Value = 81 });
        snapshot.NpcNeeds.Add(new NpcNeedSnapshot { NpcId = "Alice", NeedId = "thirst", Value = 90f });
        Assert.That(SaveSystem.TryLoadSnapshot(snapshot), Is.True);
        Assert.That(KeypadLockUI.IsOpen, Is.False);
        Assert.That(GameState.Instance.HasFlag("room02_door_unlocked"), Is.True);
        yield return WaitForTransition();
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room02"));
        Assert.That(GameState.Instance.GetRelationship("Alice"), Is.EqualTo(81));
        Assert.That(GameState.Instance.HasFlag("room02_door_unlocked"), Is.True);
        Assert.That(GameState.Instance.GetNpcNeed("Alice", "thirst"), Is.GreaterThanOrEqualTo(90f));
    }

    [UnityTest]
    public IEnumerator SecondLoadWhileTransitioningDoesNotReplaceState()
    {
        Assert.That(transition.TryTransitionToRoom("Room02", "Test", 0.05f), Is.True);
        string before = JsonUtility.ToJson(GameState.Instance.CreateSnapshot());
        Assert.That(SaveSystem.TryLoadSnapshot(new StateSnapshot { CurrentSceneId = "Room03" }), Is.False);
        Assert.That(JsonUtility.ToJson(GameState.Instance.CreateSnapshot()), Is.EqualTo(before));
        yield return WaitForTransition();
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Room02"));
    }

    [UnityTest]
    public IEnumerator FriendlyTypedReplyDoesNotInventProgressButExplorationStillDoes()
    {
        DialogueData intro = null;
#if UNITY_EDITOR
        intro = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset");
#endif
        Assert.That(intro, Is.Not.Null);
        var state = GameState.Instance; state.SetRelationship("Alice", 70);
        DialogueManager.Instance.StartDialogue(intro);
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        input.text = "ขอบคุณนะ"; DialogueManager.Instance.SendTypedMessage(); yield return null;
        Assert.That(state.GetRelationship("Alice"), Is.GreaterThan(70));
        Assert.That(KnowledgeLibrary.GetRoom("Room01").CompletedStepCount(state), Is.Zero);
        DialogueManager.Instance.HideDialogue(); DialogueManager.Instance.StartDialogue(intro);
        Assert.That(DialogueText().text, Does.Not.Contain("จัดการบางอย่างในห้องไปแล้ว"));
        state.SetFlag("inspected_painting");
        DialogueManager.Instance.HideDialogue(); DialogueManager.Instance.StartDialogue(intro);
        Assert.That(DialogueText().text, Does.Contain("จัดการบางอย่างในห้องไปแล้ว"));
    }
}
