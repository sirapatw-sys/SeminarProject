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
using UnityEngine.UI;

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
        public string text = "รหัสคือ 4592";
        public string[] facts = new string[0];
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> callback)
        { callback(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> callback, string personality = null)
        {
            if (delayed) yield return new WaitForSeconds(0.5f);
            callback(new GeneratedChatReply { reply = text, referencedFactIds = facts,
                relationshipDelta = 10, playerTone = "friendly" });
        }
    }
    private class SwappedChoiceProvider : IAiDialogueProvider
    {
        public bool malformed;
        public GeneratedDialogueContent LastContent;
        public int Calls;
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return string.Empty; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> callback)
        {
            Calls++;
            int count = data.dialogue.choices.Count;
            var choices = new GeneratedDialogueChoice[malformed ? count - 1 : count];
            for (int i = 0; i < choices.Length; i++)
            {
                var opposite = data.dialogue.choices[count - 1 - i];
                choices[i] = new GeneratedDialogueChoice
                { optionText = opposite.optionText, responseText = opposite.responseText };
            }
            LastContent = new GeneratedDialogueContent
            { lines = new[] { "วันนี้ฉันคิดถึงบ้านนิดหน่อย" }, choices = choices, referencedFactIds = Array.Empty<string>() };
            yield return null;
            callback(LastContent);
        }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> callback, string personality = null)
        { callback(null); yield break; }
    }
    private NpcPuzzleInteraction puzzleNpc;
    private NpcPuzzleData puzzleDefinition;
    private DialogueData afterDialogue;
    [SetUp]
    public void Setup()
    {
        SaveSystem.PersistenceEnabled = false; // Never touch the user's save during tests.
        GameDefinition.Override = null;
        KnowledgeLibrary.ClearCache();
        DialogueProviders.Override = new OfflineProvider();
        if (DialogueManager.Instance != null) DialogueManager.Instance.HideDialogue();
        if (GameState.Instance != null) GameState.Instance.ResetState();
    }
    [TearDown]
    public void Cleanup()
    {
        if (DialogueManager.Instance != null) DialogueManager.Instance.HideDialogue();
        if (puzzleNpc != null) puzzleNpc.StopAllCoroutines();
        if (puzzleDefinition != null) UnityEngine.Object.Destroy(puzzleDefinition);
        if (afterDialogue != null) UnityEngine.Object.Destroy(afterDialogue);
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

    private static GameObject DialoguePanel()
    {
        return (GameObject)typeof(DialogueManager).GetField("dialoguePanel",
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DialogueManager.Instance);
    }

    private static void Queue(NpcEventController controller, MiniEventData data)
    {
        typeof(NpcEventController).GetMethod("SetPendingEvent", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(controller, new object[] { data, null, false });
    }

    [UnityTest]
    public IEnumerator EvidencePickerShowsOnlyFoundEvidenceAndClickTeachesNpc()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState();
        DialogueManager.Instance.HideDialogue();
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        var picker = DialoguePanel().GetComponentInChildren<EvidencePickerUI>(true);
        Assert.That(picker, Is.Not.Null);
        picker.Open();
        var panel = DialoguePanel().transform.Find("EvidencePicker");
        Assert.That(panel.Find("Status").GetComponent<TMP_Text>().text, Does.Contain("ยังไม่มีหลักฐาน"));
        Assert.That(panel.Find("EvidenceViewport/EvidenceRows").childCount, Is.Zero);
        state.SetFlag("found_note");
        picker.Open();
        var row = panel.Find("EvidenceViewport/EvidenceRows/Evidence_desk_note").GetComponent<Button>();
        Assert.That(panel.Find("EvidenceViewport/EvidenceRows/Evidence_drawer_code"), Is.Null);
        row.onClick.Invoke();
        Assert.That(picker.IsOpen, Is.False);
        Assert.That(EvidenceSharing.HasBeenShared(state, "Alice", "Room01", "desk_note"), Is.True);
        Assert.That(NpcKnowledgeContextBuilder.Build("Alice", "Room01", state, false).CanReference("desk_note"), Is.True);
        var text = (TMP_Text)typeof(DialogueManager).GetField("dialogueText", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(DialogueManager.Instance);
        Assert.That(text.text, Does.Contain("กระดาษโน้ตพับซ่อนอยู่"));
        Assert.That(text.text, Does.Not.Contain("4592"));
        int score = state.GetRelationship("Alice");
        Assert.That(DialogueManager.Instance.TryShareEvidence("desk_note"), Is.True);
        Assert.That(state.GetRelationship("Alice"), Is.EqualTo(score));
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator EvidenceButtonsFromAnEarlierSessionCannotAffectReopenedChat()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState(); state.SetFlag("found_note");
        DialogueManager.Instance.HideDialogue();
        var intro = Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset");
        DialogueManager.Instance.StartDialogue(intro);
        var picker = DialoguePanel().GetComponentInChildren<EvidencePickerUI>(true);
        picker.Open();
        var stale = DialoguePanel().transform.Find("EvidencePicker/EvidenceViewport/EvidenceRows/Evidence_desk_note")
            .GetComponent<Button>();
        DialogueManager.Instance.HideDialogue();
        DialogueManager.Instance.StartDialogue(intro);
        int score = state.GetRelationship("Alice");
        stale.onClick.Invoke();
        Assert.That(EvidenceSharing.HasBeenShared(state, "Alice", "Room01", "desk_note"), Is.False);
        Assert.That(state.GetRelationship("Alice"), Is.EqualTo(score));
        Assert.That(picker.IsOpen, Is.False);
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator PendingAiReplyLocksEvidenceAndTypedTextCannotGrantKnowledge()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState(); state.SetFlag("found_note");
        DialogueManager.Instance.HideDialogue();
        DialogueProviders.Override = new UntrustedProvider { delayed = true, text = "สวัสดี" };
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        var picker = DialoguePanel().GetComponentInChildren<EvidencePickerUI>(true);
        picker.Open();
        Type("ฉันเอาโน้ตมาให้แล้ว");
        Assert.That(picker.IsOpen, Is.False);
        Assert.That(DialogueManager.Instance.CanShareEvidence, Is.False);
        Assert.That(DialogueManager.Instance.TryShareEvidence("desk_note"), Is.False);
        yield return new WaitForSeconds(0.7f);
        Assert.That(EvidenceSharing.HasBeenShared(state, "Alice", "Room01", "desk_note"), Is.False);
        Assert.That(DialogueManager.Instance.TryShareEvidence("desk_note"), Is.True);
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator ChoiceCompletionCommitsOnceAndEarlyCloseDoesNotConsumeStory()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState();
        DialogueManager.Instance.HideDialogue();
        var controller = UnityEngine.Object.FindObjectOfType<NpcEventController>();
        var data = UnityEngine.Object.Instantiate(Load<MiniEventData>("Assets/Data/Events/Alice_EvidencePromise_Event.asset"));
        data.minimumRoomTimeSeconds = 0;
        try
        {
            Queue(controller, data);
            Assert.That(controller.TryStartPendingEvent(), Is.True);
            Assert.That(state.HasFlag(data.CompletedFlag), Is.False);
            Assert.That(DialogueManager.Instance.CanShareEvidence, Is.False);
            DialogueManager.Instance.HideDialogue();
            Assert.That(data.CanTrigger(state), Is.True);
            Assert.That(state.HasFlag("alice_evidence_arc.started"), Is.False);
            Queue(controller, data);
            Assert.That(controller.TryStartPendingEvent(), Is.True);
            DialogueManager.Instance.NextLine(); DialogueManager.Instance.NextLine();
            DialogueManager.Instance.SelectChoice(0);
            Assert.That(state.HasFlag(data.CompletedFlag), Is.True);
            Assert.That(state.HasFlag("alice_evidence_arc.promised"), Is.True);
            int score = state.GetRelationship("Alice");
            DialogueManager.Instance.SelectChoice(0);
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(score));
            DialogueManager.Instance.NextLine();
            Assert.That(DialogueManager.IsDialogueOpen, Is.False);
            Assert.That(data.CanTrigger(state), Is.False);
        }
        finally { UnityEngine.Object.Destroy(data); }
    }

    [UnityTest]
    public IEnumerator PositiveEventChoiceCannotDisplayAiRejectionOrLoseItsAuthoredEffects()
    { yield return VerifyLockedChoices(0, false, false); }

    [UnityTest]
    public IEnumerator NegativeEventChoiceCannotDisplayAiAcceptanceOrGainRelationship()
    { yield return VerifyLockedChoices(2, false, false); }

    [UnityTest]
    public IEnumerator ChoiceWithoutActionsStillKeepsItsAuthoredTextAndCompletionMeaning()
    { yield return VerifyLockedChoices(2, true, false); }

    [UnityTest]
    public IEnumerator MalformedAiChoicesUseAuthoredDialogueWithoutChangingEffects()
    { yield return VerifyLockedChoices(0, false, true); }

    private IEnumerator VerifyLockedChoices(int selectedIndex, bool noActions, bool malformed)
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState();
        state.SetRelationship("Alice", 30); state.SetNpcNeed("Alice", "homesickness", 50f);
        DialogueManager.Instance.HideDialogue();
        var provider = new SwappedChoiceProvider { malformed = malformed };
        DialogueProviders.Override = provider;
        var controller = UnityEngine.Object.FindObjectOfType<NpcEventController>();
        var source = UnityEngine.Object.Instantiate(Load<DialogueData>("Assets/Data/Dialogue/Alice_Homesick.asset"));
        var ev = ScriptableObject.CreateInstance<MiniEventData>();
        ev.eventId = "choice_lock_test"; ev.npcId = "Alice"; ev.dialogue = source;
        ev.triggerType = MiniEventTriggerType.RandomAmbient; ev.useAiDialogue = true;
        ev.completeOnChoice = true; ev.repeatable = false;
        for (int i = 0; i < source.choices.Count; i++)
        {
            if (noActions) source.choices[i].actions.Clear();
            else
            {
                source.choices[i].actions.Add(new ActionCommand
                { type = ActionType.SetFlag, targetId = "choice_lock.effect." + i });
                source.choices[i].actions.Add(new ActionCommand
                { type = ActionType.AddNpcMemory, targetId = "Alice", text = "choice_lock.memory." + i });
            }
        }
        try
        {
            var queue = typeof(NpcEventController).GetMethod("QueueEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            queue.Invoke(controller, new object[] { ev }); yield return null; yield return null;
            Assert.That(controller.HasPendingEvent, Is.True);
            if (!malformed)
            {
                string reason;
                Assert.That(NpcReplyPolicy.ValidateEvent(BuildEventContext(state), provider.LastContent, out reason),
                    Is.True, "The mock must pass fact validation despite its swapped choice meanings: " + reason);
            }
            Assert.That(controller.TryStartPendingEvent(), Is.True);
            var text = (TMP_Text)typeof(DialogueManager).GetField("dialogueText", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(DialogueManager.Instance);
            Assert.That(text.text, Is.EqualTo(malformed ? source.lines[0] : provider.LastContent.lines[0]));
            // Closing early must not commit even an actionless completion choice.
            DialogueManager.Instance.HideDialogue();
            Assert.That(state.HasFlag(ev.CompletedFlag), Is.False);
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(30));
            Assert.That(state.HasFlag("choice_lock.effect." + selectedIndex), Is.False);
            queue.Invoke(controller, new object[] { ev }); yield return null; yield return null;
            Assert.That(controller.TryStartPendingEvent(), Is.True);
            // Advance through either the generated opening or the authored fallback.
            for (int i = 0; i < (malformed ? source.lines.Count : 1); i++) DialogueManager.Instance.NextLine();
            for (int i = 0; i < source.choices.Count; i++)
            {
                var button = (Button)typeof(DialogueManager).GetField("choiceButton" + (i + 1),
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DialogueManager.Instance);
                Assert.That(button.gameObject.activeSelf, Is.True);
                Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(source.choices[i].optionText));
            }
            int delta = 0; float needDelta = 0f;
            foreach (var action in source.choices[selectedIndex].actions)
            {
                if (action.type == ActionType.ChangeRelationship)
                    delta += RelationshipTuning.ScaleGain(action.amount, RelationshipTuning.ChoiceGainScale);
                if (action.type == ActionType.ChangeNpcNeed && action.secondaryId == "homesickness") needDelta += action.amount;
            }
            DialogueManager.Instance.SelectChoice(selectedIndex);
            // The authored reply, followed by the optional line of numbers.
            Assert.That(text.text, Does.StartWith(source.choices[selectedIndex].responseText));
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(30 + delta));
            Assert.That(state.GetNpcNeed("Alice", "homesickness"), Is.EqualTo(50f + needDelta).Within(0.5f));
            Assert.That(state.HasFlag(ev.CompletedFlag), Is.True);
            var log = state.GetConversationLog("Alice");
            Assert.That(log[log.Count - 2].Text, Does.EndWith(source.choices[selectedIndex].optionText));
            Assert.That(log[log.Count - 1].Text, Is.EqualTo(source.choices[selectedIndex].responseText));
            for (int i = 0; i < source.choices.Count; i++)
            {
                Assert.That(state.HasFlag("choice_lock.effect." + i), Is.EqualTo(!noActions && i == selectedIndex));
                if (!noActions && i == selectedIndex)
                    Assert.That(state.GetNpcMemory("Alice"), Does.Contain("choice_lock.memory." + i));
                else Assert.That(state.GetNpcMemory("Alice"), Does.Not.Contain("choice_lock.memory." + i));
            }
            // Repeated UI clicks and closed-dialogue calls cannot award the effects again.
            DialogueManager.Instance.SelectChoice(selectedIndex);
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(30 + delta));
            Assert.That(state.GetNpcNeed("Alice", "homesickness"), Is.EqualTo(50f + needDelta).Within(0.5f));
            DialogueManager.Instance.HideDialogue(); DialogueManager.Instance.SelectChoice(selectedIndex);
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(30 + delta));
            Assert.That(provider.Calls, Is.EqualTo(2));
            Assert.That(ev.CanTrigger(state), Is.False);
        }
        finally
        {
            DialogueManager.Instance.HideDialogue();
            UnityEngine.Object.Destroy(ev); UnityEngine.Object.Destroy(source);
        }
    }

    private static NpcKnowledgeContext BuildEventContext(GameState state)
    { return NpcKnowledgeContextBuilder.Build("Alice", "Room01", state, false); }

    [UnityTest]
    public IEnumerator PendingStoryCannotConsumeOrReplaceAnotherConversation()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState();
        state.SetFlag("alice_evidence_arc.promised"); state.SetFlag("found_note");
        EvidenceShareResult shared;
        Assert.That(EvidenceSharing.TryShare(state, "Alice", "desk_note", out shared), Is.True);
        DialogueManager.Instance.HideDialogue();
        var controller = UnityEngine.Object.FindObjectOfType<NpcEventController>();
        var data = Load<MiniEventData>("Assets/Data/Events/Alice_PromiseKept_Event.asset");
        Queue(controller, data);
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        int version = DialogueManager.Instance.ConversationVersion;
        Assert.That(controller.TryStartPendingEvent(), Is.False);
        Assert.That(controller.HasPendingEvent, Is.True);
        Assert.That(DialogueManager.Instance.ConversationVersion, Is.EqualTo(version));
        Assert.That(state.HasFlag(data.CompletedFlag), Is.False);
        DialogueManager.Instance.HideDialogue();
        Assert.That(controller.TryStartPendingEvent(), Is.True);
        DialogueManager.Instance.SelectChoice(0);
        Assert.That(state.HasFlag(data.CompletedFlag), Is.True);
        Assert.That(state.HasFlag("alice_evidence_arc.fulfilled"), Is.True);
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator SharingEvidenceCancelsQueuedMissedPromiseAndEnablesKeptOutcome()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var state = GameState.Instance; state.ResetState();
        state.SetFlag("alice_evidence_arc.promised"); state.SetFlag("found_note"); state.SetFlag("drawer_opened");
        DialogueManager.Instance.HideDialogue();
        var controller = UnityEngine.Object.FindObjectOfType<NpcEventController>();
        var missed = Load<MiniEventData>("Assets/Data/Events/Alice_PromiseMissed_Event.asset");
        Queue(controller, missed);
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        Assert.That(DialogueManager.Instance.TryShareEvidence("desk_note"), Is.True);
        DialogueManager.Instance.HideDialogue();
        Assert.That(controller.TryStartPendingEvent(), Is.False);
        Assert.That(controller.HasPendingEvent, Is.False);
        Assert.That(state.HasFlag(missed.CompletedFlag), Is.False);
        Assert.That(state.HasFlag("alice_evidence_arc.neglected"), Is.False);
        var kept = Load<MiniEventData>("Assets/Data/Events/Alice_PromiseKept_Event.asset");
        Assert.That(kept.CanTrigger(state), Is.True);
        Queue(controller, kept);
        Assert.That(controller.TryStartPendingEvent(), Is.True);
        DialogueManager.Instance.SelectChoice(0);
        Assert.That(state.HasFlag("alice_evidence_arc.fulfilled"), Is.True);
        DialogueManager.Instance.HideDialogue();
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

    [UnityTest]
    public IEnumerator LeavingSampleRestoresMainKnowledgeRoomLoadingAndSaveNamespace()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        var main = GameDefinition.Current;
        var alice = KnowledgeLibrary.GetNpc("Alice");
        var mainRoom = KnowledgeLibrary.GetRoom("Room01");
        string mainSave = SaveSystem.SavePath;
        Assert.That(RoomTransitionManager.CanLoadRoom("Room02"), Is.True);
        yield return SceneManager.LoadSceneAsync("ObservatoryA"); yield return null;
        Assert.That(GameDefinition.Current.gameId, Is.EqualTo("observatory_sample"));
        Assert.That(KnowledgeLibrary.GetNpc("Nora"), Is.Not.Null);
        Assert.That(KnowledgeLibrary.GetNpc("Alice"), Is.Null);
        string sampleSave = SaveSystem.SavePath;
        Assert.That(sampleSave, Is.Not.EqualTo(mainSave));
        yield return SceneManager.LoadSceneAsync("ObservatoryB"); yield return null;
        Assert.That(GameDefinition.Current.gameId, Is.EqualTo("observatory_sample"));
        Assert.That(SaveSystem.SavePath, Is.EqualTo(sampleSave));
        Assert.That(RoomTransitionManager.CanLoadRoom("ObservatoryA"), Is.True);
        GameState.Instance.RemoveFlag(mainRoom.enteredFlag);
        // Do not manually clear Override: scene ownership must perform the restoration.
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Assert.That(GameDefinition.Override, Is.Null);
        Assert.That(GameDefinition.Current, Is.SameAs(main));
        Assert.That(KnowledgeLibrary.GetRoom("Room01"), Is.SameAs(mainRoom));
        Assert.That(KnowledgeLibrary.GetNpc("Alice"), Is.SameAs(alice));
        Assert.That(KnowledgeLibrary.GetNpc("Nora"), Is.Null);
        Assert.That(GameState.Instance.HasFlag(mainRoom.enteredFlag), Is.True);
        Assert.That(GameSession.CreateDefault().CurrentSceneId, Is.EqualTo("Room01"));
        Assert.That(SaveSystem.SavePath, Is.EqualTo(mainSave));
        Assert.That(RoomTransitionManager.CanLoadRoom("Room02"), Is.True);
        Assert.That(RoomTransitionManager.CanLoadRoom("ObservatoryA"), Is.False);
    }

    private IEnumerator PreparePuzzle(bool keepNpc = false)
    {
        if (GameState.Instance != null) GameState.Instance.ResetState();
        yield return SceneManager.LoadSceneAsync("Room02"); yield return null;
        GameState.Instance.ResetState(); DialogueManager.Instance.HideDialogue();
        puzzleNpc = UnityEngine.Object.FindObjectOfType<NpcPuzzleInteraction>();
        Assert.That(puzzleNpc, Is.Not.Null);
        var field = typeof(NpcPuzzleInteraction).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic);
        puzzleDefinition = UnityEngine.Object.Instantiate((NpcPuzzleData)field.GetValue(puzzleNpc));
        puzzleDefinition.readReplyDelay = 0.15f;
        puzzleDefinition.hideOnSolved = !keepNpc;
        field.SetValue(puzzleNpc, puzzleDefinition);
    }
    private void SolvePuzzle()
    {
        GameState.Instance.AddItem("tome"); puzzleNpc.Interact(); Type("tomorrow");
        Assert.That(GameState.Instance.HasFlag("sena_passed"), Is.True);
        Assert.That(GameState.Instance.HasFlag("room02_door_unlocked"), Is.True);
    }

    [UnityTest]
    public IEnumerator SolvedFarewellNeverTakesOverAnotherNpcConversation()
    {
        yield return PreparePuzzle(); SolvePuzzle();
        DialogueManager.Instance.HideDialogue();
        DialogueManager.Instance.StartDialogue("Alice", new[] { "คุยเรื่องอื่น" }, false);
        int version = DialogueManager.Instance.ConversationVersion;
        yield return new WaitForSeconds(0.3f);
        Assert.That(DialogueManager.Instance.ActiveNpcId, Is.EqualTo("Alice"));
        Assert.That(DialogueManager.Instance.ConversationVersion, Is.EqualTo(version));
    }

    [UnityTest]
    public IEnumerator ClosingChatNeverReopensTheDelayedFarewell()
    {
        yield return PreparePuzzle(); SolvePuzzle();
        DialogueManager.Instance.HideDialogue();
        yield return new WaitForSeconds(0.3f);
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator FarewellStillPlaysWhenTheOriginalConversationRemainsOpen()
    {
        yield return PreparePuzzle(); SolvePuzzle();
        int version = DialogueManager.Instance.ConversationVersion;
        yield return new WaitForSeconds(0.3f);
        Assert.That(DialogueManager.Instance.ActiveNpcId, Is.EqualTo("Sena"));
        Assert.That(DialogueManager.Instance.ConversationVersion, Is.GreaterThan(version));
    }

    [UnityTest]
    public IEnumerator ReopenedConversationWithSameNpcIsNotTheOldConversation()
    {
        yield return PreparePuzzle(); SolvePuzzle();
        DialogueManager.Instance.HideDialogue();
        DialogueManager.Instance.StartDialogue("Sena", new[] { "คุยรอบใหม่" }, false);
        int version = DialogueManager.Instance.ConversationVersion;
        yield return new WaitForSeconds(0.3f);
        Assert.That(DialogueManager.Instance.ConversationVersion, Is.EqualTo(version));
    }

    [UnityTest]
    public IEnumerator KeptNpcRemainsFocusableAndUsesPostSolvedDialogueWithoutTakingMoreItems()
    {
        yield return PreparePuzzle(true);
        afterDialogue = ScriptableObject.CreateInstance<DialogueData>();
        afterDialogue.dialogueId = "after_solved"; afterDialogue.speakerId = "Sena";
        afterDialogue.speakerName = "Sena"; afterDialogue.lines.Add("หลังจบปริศนาแล้ว");
        puzzleDefinition.postSolvedDialogue = afterDialogue;
        InteractionFocus.Enter(puzzleNpc);
        SolvePuzzle(); yield return new WaitForSeconds(0.3f);
        Assert.That(puzzleNpc.gameObject.activeSelf, Is.True);
        Assert.That(puzzleNpc.CanFocus, Is.True);
        Assert.That(puzzleNpc.GetComponent<Collider2D>().enabled, Is.True);
        Assert.That(InteractionFocus.PickNearest(new IFocusable[] { puzzleNpc }, puzzleNpc.FocusPoint), Is.SameAs(puzzleNpc));
        DialogueManager.Instance.HideDialogue(); GameState.Instance.AddItem("tome");
        puzzleNpc.Interact();
        Assert.That(DialogueManager.IsDialogueOpen, Is.True);
        Assert.That(GameState.Instance.HasItem("tome"), Is.True);
        var log = GameState.Instance.GetConversationLog("Sena");
        Assert.That(log[log.Count - 1].Text, Is.EqualTo("หลังจบปริศนาแล้ว"));
    }

    [UnityTest]
    public IEnumerator AiFactTokensDisplayOnlyTheAllowedAuthoredStatement()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        GameState.Instance.ResetState(); DialogueManager.Instance.HideDialogue();
        DialogueProviders.Override = new UntrustedProvider { text = "{fact:door_locked}", facts = new[] { "door_locked" } };
        DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
        Type("เล่าเรื่องประตูให้ฟัง");
        var context = NpcKnowledgeContextBuilder.Build("Alice", "Room01", GameState.Instance, false);
        var log = GameState.Instance.GetConversationLog("Alice");
        Assert.That(log[log.Count - 1].Text, Is.EqualTo(context.KnownFacts.Find(f => f.factId == "door_locked").statement));
    }

    [UnityTest]
    public IEnumerator ForgedReferencesAndUnsupportedWorldClaimsNeverReachTheConversationLog()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        GameState.Instance.ResetState(); DialogueManager.Instance.HideDialogue();
        foreach (var provider in new[] {
            new UntrustedProvider { text = "มีกุญแจสีม่วงซ่อนอยู่ใต้เตียง" },
            new UntrustedProvider { text = "{fact:door_locked}\nมีกุญแจสีม่วงซ่อนอยู่ใต้เตียง", facts = new[] { "door_locked" } },
            new UntrustedProvider { text = "รหัสคือ 4-5-9-2", facts = new[] { "door_locked" } } })
        {
            DialogueProviders.Override = provider;
            DialogueManager.Instance.StartDialogue(Load<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset"));
            Type("เล่าเรื่องหน่อย");
            var log = GameState.Instance.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Does.Not.Contain("สีม่วง"));
            Assert.That(log[log.Count - 1].Text, Does.Not.Contain("4-5-9-2"));
            Assert.That(log[log.Count - 1].Text, Does.Not.Contain("{fact:"));
            var text = (TMP_Text)typeof(DialogueManager).GetField("dialogueText",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DialogueManager.Instance);
            Assert.That(text.text, Does.Not.Contain("คำตอบ AI ไม่ตรงกับข้อมูล"));
            DialogueManager.Instance.HideDialogue();
        }
    }

    [UnityTest]
    public IEnumerator QueuedAiEventsExpandFactsAndFallBackIfThoseFactsBecomeUnavailable()
    {
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        GameState.Instance.ResetState(); DialogueManager.Instance.HideDialogue();
        var controller = UnityEngine.Object.FindObjectOfType<NpcEventController>();
        Assert.That(controller, Is.Not.Null);
        var dialogue = ScriptableObject.CreateInstance<DialogueData>();
        var data = ScriptableObject.CreateInstance<MiniEventData>();
        try
        {
            dialogue.dialogueId = "regression_event"; dialogue.speakerId = "Alice";
            dialogue.speakerName = "Alice"; dialogue.lines.Add("บทสำรองที่ยืนยันได้");
            data.eventId = "regression_event"; data.npcId = "Alice";
            data.triggerType = MiniEventTriggerType.RandomAmbient; data.dialogue = dialogue;
            var raw = new GeneratedDialogueContent { lines = new[] { "{fact:painting_arrow}" },
                choices = new GeneratedDialogueChoice[0], referencedFactIds = new[] { "painting_arrow" } };
            var queue = typeof(NpcEventController).GetMethod("SetPendingEvent", BindingFlags.Instance | BindingFlags.NonPublic);
            GameState.Instance.SetFlag("inspected_painting");
            EvidenceShareResult shared;
            Assert.That(EvidenceSharing.TryShare(GameState.Instance, "Alice", "painting_arrow", out shared), Is.True);
            queue.Invoke(controller, new object[] { data, raw, true });
            Assert.That(controller.TryStartPendingEvent(), Is.True);
            var log = GameState.Instance.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Does.Contain("ด้านหลังกรอบภาพ"));
            Assert.That(log[log.Count - 1].Text, Does.Not.Contain("{fact:"));
            DialogueManager.Instance.HideDialogue();
            GameState.Instance.RemoveFlag("inspected_painting");
            queue.Invoke(controller, new object[] { data, raw, true });
            Assert.That(controller.TryStartPendingEvent(), Is.True);
            log = GameState.Instance.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Is.EqualTo("บทสำรองที่ยืนยันได้"));
        }
        finally { UnityEngine.Object.Destroy(dialogue); UnityEngine.Object.Destroy(data); }
    }
}
