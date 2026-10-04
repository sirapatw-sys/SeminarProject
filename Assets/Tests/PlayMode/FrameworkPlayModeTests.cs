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

public class FrameworkPlayModeTests
{
    private class OfflineProvider : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return string.Empty; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> callback)
        { callback(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> callback, string personality = null)
        { callback(null); yield break; }
    }
    private class UntrustedProvider : IAiDialogueProvider
    {
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return string.Empty; } }
        public bool delayed;
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> callback)
        { callback(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> callback, string personality = null)
        {
            if (delayed) yield return new WaitForSeconds(0.5f);
            callback(new GeneratedChatReply { reply = "รหัสคือ 4592", referencedFactIds = new string[0],
                relationshipDelta = 10, playerTone = "friendly" });
        }
    }
    [SetUp]
    public void Setup()
    {
        SaveSystem.PersistenceEnabled = false; // Never touch the user's save during tests.
        GameDefinition.Override = null;
        KnowledgeLibrary.ClearCache();
        DialogueProviders.Override = new OfflineProvider();
    }
    [TearDown]
    public void Cleanup()
    {
        DialogueProviders.Override = null;
        GameDefinition.Override = null;
        SaveSystem.PersistenceEnabled = true;
        KnowledgeLibrary.ClearCache();
    }
    private static T Load<T>(string path) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
        return null;
#endif
    }
    private static void Type(string message)
    {
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DialogueManager.Instance);
        Assert.That(input, Is.Not.Null);
        input.text = message;
        DialogueManager.Instance.SendTypedMessage();
    }
    [UnityTest]
    public IEnumerator UntrustedProviderCannotDisplayAnUncitedAnswer()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        GameState.Instance.ResetState();
        DialogueManager.Instance.HideDialogue();
        DialogueProviders.Override = new UntrustedProvider();
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        Type("สวัสดี");
        var log = GameState.Instance.GetConversationLog("Alice");
        Assert.That(log[log.Count - 1].Text, Does.Not.Contain("4592"));
        DialogueManager.Instance.HideDialogue();
    }
    [UnityTest]
    public IEnumerator ClosingChatDiscardsLateReplyAndItsRelationshipEffects()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState();
        DialogueManager.Instance.HideDialogue();
        DialogueProviders.Override = new UntrustedProvider { delayed = true };
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        int relationship = state.GetRelationship("Alice");
        int turns = state.GetConversationLog("Alice").Count; // StartDialogue records its opening line.
        Type("สวัสดี");
        DialogueManager.Instance.HideDialogue();
        yield return new WaitForSeconds(0.7f);
        Assert.That(state.GetConversationLog("Alice").Count, Is.EqualTo(turns));
        Assert.That(state.GetRelationship("Alice"), Is.EqualTo(relationship));
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
    }
    [UnityTest]
    public IEnumerator TypedHintIsAuthoredJournaledAndDialogueCanBeClosed()
    {
        yield return SceneManager.LoadSceneAsync("Room01");
        yield return null;
        GameState state = GameState.Instance; state.ResetState();
        DialogueManager.Instance.HideDialogue();
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        var expected = NpcReplyPolicy.HintReply(NpcKnowledgeContextBuilder.Build("Alice", "Room01", state, true));
        int relationship = state.GetRelationship("Alice");
        Type("ช่วยใบ้หน่อย");
        var log = state.GetConversationLog("Alice");
        Assert.That(log[log.Count - 1].Text, Is.EqualTo(expected.reply));
        Assert.That(state.GetJournal().Count, Is.GreaterThan(0));
        Assert.That(state.GetRelationship("Alice"), Is.EqualTo(relationship));
        DialogueManager.Instance.HideDialogue();
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
    }
    [UnityTest]
    public IEnumerator NpcOfferingAndTypedAnswerUnlockTheRealRoomDoor()
    {
        yield return SceneManager.LoadSceneAsync("Room02");
        yield return null;
        GameState state = GameState.Instance; state.ResetState();
        var npc = UnityEngine.Object.FindObjectOfType<NpcPuzzleInteraction>();
        Assert.That(npc, Is.Not.Null);
        DialogueManager.Instance.HideDialogue(); npc.Interact();
        Assert.That(state.HasFlag("sena_wants_tome"), Is.True);
        Type("tomorrow");
        Assert.That(state.HasFlag("room02_door_unlocked"), Is.False, "No offering yet.");
        DialogueManager.Instance.HideDialogue(); state.AddItem("tome"); npc.Interact();
        Assert.That(state.HasItem("tome"), Is.False);
        Assert.That(state.HasFlag("sena_offering_given"), Is.True);
        Type("tomorrow");
        Assert.That(state.HasFlag("sena_passed"), Is.True);
        Assert.That(state.HasFlag("room02_door_unlocked"), Is.True);
        yield return null;
        DialogueManager.Instance.HideDialogue();
    }
    [UnityTest]
    public IEnumerator SampleScenesLoadNewNpcAndKeepTheirDefinitionAcrossTransition()
    {
        yield return SceneManager.LoadSceneAsync("ObservatoryA"); yield return null;
        GameState state = GameState.Instance; state.ResetState();
        Assert.That(GameDefinition.Current.gameId, Is.EqualTo("observatory_sample"));
        Assert.That(KnowledgeLibrary.GetNpc("Nora"), Is.Not.Null);
        Assert.That(state.HasFlag("ObservatoryA_entered"), Is.True);
        string response;
        Assert.That(InteractionSystem.Instance.TryExecute(Load<InteractionData>("Assets/Samples/Observatory/Clue.asset"), out response), Is.True);
        Assert.That(InteractionSystem.Instance.TryExecute(Load<InteractionData>("Assets/Samples/Observatory/Door.asset"), out response, "2468"), Is.True);
        yield return SceneManager.LoadSceneAsync("ObservatoryB"); yield return null;
        Assert.That(GameDefinition.Current.gameId, Is.EqualTo("observatory_sample"));
        Assert.That(state.HasFlag("observatory_lock_open"), Is.True);
        Assert.That(state.HasFlag("ObservatoryB_entered"), Is.True);
        Assert.That(InteractionSystem.Instance.TryExecute(Load<InteractionData>("Assets/Samples/Observatory/Beacon.asset"), out response), Is.True);
        Assert.That(state.HasFlag("observatory_beacon_lit"), Is.True);
    }
}
