using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class NpcReplyBoundaryPlayModeTests
{
    private const string SplitAnswer = "สี่\nห้า\nเก้า\nสอง";
    private class Provider : IAiDialogueProvider
    {
        public bool Online;
        public bool CanGenerate { get { return Online; } }
        public string LastError { get { return string.Empty; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> complete)
        { complete(null); yield break; }
        public IEnumerator GenerateReply(string npc, string name, string context, string message,
            Action<GeneratedChatReply> complete, string personality = null)
        { complete(new GeneratedChatReply { reply = SplitAnswer, referencedFactIds = Array.Empty<string>() }); yield break; }
    }
    private Provider provider;
    private DialogueData intro;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static TMP_Text Text()
    { return (TMP_Text)typeof(DialogueManager).GetField("dialogueText", Private).GetValue(DialogueManager.Instance); }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SaveSystem.PersistenceEnabled = false; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        provider = new Provider(); DialogueProviders.Override = provider;
        if (DialogueManager.Instance != null) DialogueManager.Instance.HideDialogue();
        if (GameState.Instance != null) GameState.Instance.ResetState();
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        DialogueManager.Instance.HideDialogue(); GameState.Instance.ResetState();
        GameState.Instance.SetCurrentScene("Room01");
#if UNITY_EDITOR
        intro = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset");
#endif
        Assert.That(intro, Is.Not.Null);
    }
    [TearDown]
    public void Cleanup()
    {
        if (DialogueManager.Instance != null) DialogueManager.Instance.HideDialogue();
        DialogueProviders.Override = null; GameDefinition.Override = null;
        SaveSystem.PersistenceEnabled = true; KnowledgeLibrary.ClearCache();
    }

    [UnityTest]
    public IEnumerator LiveNeedTickWithoutExplorationDoesNotClaimRoomProgress()
    {
        var state = GameState.Instance;
        Assert.That(UnityEngine.Object.FindObjectOfType<NpcNeedController>(), Is.Not.Null);
        state.SetRelationship("Alice", 70); state.SetFlag("alice_evidence_arc.fulfilled");
        DialogueManager.Instance.StartDialogue(intro);
        yield return new WaitForSeconds(0.1f);
        Assert.That(state.GetNpcNeed("Alice", "thirst"), Is.GreaterThan(0f));
        Assert.That(state.GetNpcEmotion("Alice", "homesickness"), Is.GreaterThan(0f));
        Assert.That(KnowledgeLibrary.GetRoom("Room01").CompletedStepCount(state), Is.EqualTo(0));
        Assert.That(state.HasWorldChangedSinceConversation("Alice"), Is.False);
        string expected = NpcOfflineReplies.ReturnGreeting(KnowledgeLibrary.GetNpc("Alice"), state);
        DialogueManager.Instance.HideDialogue(); DialogueManager.Instance.StartDialogue(intro);
        Assert.That(Text().text, Is.EqualTo(expected));
        Assert.That(Text().text, Does.Not.Contain("เธอจัดการบางอย่างในห้องไปแล้ว"));
        DialogueManager.Instance.HideDialogue();
        state.SetFlag("inspected_painting");
        DialogueManager.Instance.StartDialogue(intro);
        Assert.That(Text().text, Does.Contain("จัดการบางอย่าง"), "Real room progress must still change the greeting.");
    }

    [UnityTest]
    public IEnumerator TypedGeneratedReplyCannotDisplayProtectedAnswerAcrossLines()
    {
        provider.Online = true;
        DialogueManager.Instance.StartDialogue(intro);
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        input.text = "สวัสดี"; DialogueManager.Instance.SendTypedMessage();
        yield return null;
        Assert.That(Text().text, Does.Not.Contain(SplitAnswer));
        Assert.That(Text().text, Does.Contain("บทสนทนาสำรอง"), "The invalid generated answer must use fallback.");
        Assert.That(GameState.Instance.HasFlag("drawer_opened"), Is.False);
        DialogueManager.Instance.HideDialogue();
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator NeedAndEmotionEventsStillQueueAndOpenWithoutInventingProgress()
    {
        foreach (var existing in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) existing.enabled = false;
        foreach (bool emotion in new[] { false, true })
        {
            var host = new GameObject("PassiveMetricEventTest");
            var ev = ScriptableObject.CreateInstance<MiniEventData>();
            var dialogue = ScriptableObject.CreateInstance<DialogueData>();
            var controller = host.AddComponent<NpcEventController>();
            try
            {
                var state = GameState.Instance;
                string metric = emotion ? "homesickness" : "thirst";
                dialogue.dialogueId = "passive_" + metric; dialogue.speakerId = "Alice"; dialogue.speakerName = "Alice";
                dialogue.lines.Add("คุยเป็นเพื่อนหน่อยได้ไหม");
                ev.eventId = dialogue.dialogueId; ev.npcId = "Alice"; ev.dialogue = dialogue;
                ev.metricId = metric; ev.threshold = 70f; ev.useAiDialogue = false;
                ev.triggerType = emotion ? MiniEventTriggerType.EmotionThreshold : MiniEventTriggerType.NeedThreshold;
                typeof(NpcEventController).GetField("events", Private).SetValue(controller, new List<MiniEventData> { ev });
                if (emotion) state.SetNpcEmotion("Alice", metric, 69f);
                else state.SetNpcNeed("Alice", metric, 69f);
                state.RecordConversation("Alice");
                Assert.That(ev.CanTrigger(state), Is.False);
                if (emotion) state.ChangeNpcEmotion("Alice", metric, 2f);
                else state.ChangeNpcNeed("Alice", metric, 2f);
                Assert.That(state.HasWorldChangedSinceConversation("Alice"), Is.False);
                typeof(NpcEventController).GetMethod("TryQueueRandomEvent", Private).Invoke(controller, new object[0]);
                Assert.That(controller.HasPendingEvent, Is.True);
                Assert.That(controller.TryStartPendingEvent(), Is.True);
                Assert.That(Text().text, Is.EqualTo(dialogue.lines[0]));
                DialogueManager.Instance.HideDialogue();
                yield return null;
            }
            finally
            {
                controller.enabled = false;
                UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(ev); UnityEngine.Object.Destroy(dialogue);
            }
        }
    }
}
