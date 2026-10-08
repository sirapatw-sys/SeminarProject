using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public string Reply = SplitAnswer;
        public string[] FactIds = Array.Empty<string>();
        public Action BeforeReply;
        public bool CanGenerate { get { return Online; } }
        public string LastError { get { return string.Empty; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> complete)
        { complete(null); yield break; }
        public IEnumerator GenerateReply(string npc, string name, string context, string message,
            Action<GeneratedChatReply> complete, string personality = null)
        { BeforeReply?.Invoke(); complete(new GeneratedChatReply { reply = Reply, referencedFactIds = FactIds }); yield break; }
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
    public IEnumerator NaturalSocialMentionsAndNumbersReachTheUiWithoutPolicyWarnings()
    {
        provider.Online = true;
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (string reply in new[] { "มาช่วยกันหาทางออกต่อเถอะ", "ฉันจะอยู่ข้างๆ เธอเอง", "ลองหายใจช้าๆ 3 ครั้งนะ" })
        {
            provider.Reply = reply;
            DialogueManager.Instance.StartDialogue(intro); input.text = "คุยเป็นเพื่อนหน่อย";
            DialogueManager.Instance.SendTypedMessage(); yield return null;
            var log = GameState.Instance.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Is.EqualTo(reply));
            Assert.That(Text().text, Does.Contain(reply));
            Assert.That(Text().text, Does.Not.Contain("คำตอบ AI ไม่ตรงกับข้อมูล"));
            Assert.That(GameState.Instance.GetJournal().Count, Is.Zero);
            DialogueManager.Instance.HideDialogue();
        }
    }

    [UnityTest]
    public IEnumerator VisibleRoom03AtmosphereReachesTheUiWithoutCreatingHints()
    {
        yield return SceneManager.LoadSceneAsync("Room03"); yield return null;
        var state = GameState.Instance; state.ResetState(); DialogueManager.Instance.HideDialogue();
        DialogueData stelle = null;
#if UNITY_EDITOR
        stelle = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Data/Dialogue/Stelle_Room03.asset");
#endif
        Assert.That(stelle, Is.Not.Null); provider.Online = true;
        var disabled = new List<NpcEventController>();
        foreach (var controller in UnityEngine.Object.FindObjectsOfType<NpcEventController>())
            if (controller.enabled) { disabled.Add(controller); controller.enabled = false; }
        try
        {
            var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
            foreach (string reply in new[] { "มีเสียงเพลงจากกล่องดนตรีอีกแล้วค่ะ สเตลกลัวจัง",
                "เจอกระจกบานนั้นทีไรขนลุกทุกที", "มองกระจกแล้วเห็นแต่หน้าตัวเองซีดๆ" })
            {
                provider.Reply = reply; DialogueManager.Instance.StartDialogue(stelle);
                int score = state.GetRelationship("Stelle");
                Assert.That(PlayerToneClassifier.Read("โอเค").Tone, Is.EqualTo(PlayerTone.Neutral));
                input.text = "โอเค"; DialogueManager.Instance.SendTypedMessage(); yield return null;
                var log = state.GetConversationLog("Stelle");
                Assert.That(log[log.Count - 1].Text, Is.EqualTo(reply));
                Assert.That(Text().text, Does.Contain(reply));
                Assert.That(Text().text, Does.Not.Contain("คำตอบ AI ไม่ตรงกับข้อมูล"));
                Assert.That(state.GetRelationship("Stelle"), Is.EqualTo(score));
                Assert.That(state.GetJournal().Count, Is.Zero);
                DialogueManager.Instance.HideDialogue();
            }
        }
        finally
        {
            DialogueManager.Instance.HideDialogue();
            foreach (var controller in disabled) if (controller != null) controller.enabled = true;
        }
    }

    [UnityTest]
    public IEnumerator NeutralHintThinkingFramesReachAliceAndSenaUiWithCanonicalJournal()
    {
        yield return VerifyThinkingFrameUi("Room01", "Alice", "Assets/Data/Dialogue/Alice_Intro.asset", "ลองคิดดูนะ: {hint}");
        yield return VerifyThinkingFrameUi("Room01", "Alice", "Assets/Data/Dialogue/Alice_Intro.asset", "ค่อนข้างยากหน่อย {hint}");
        yield return VerifyThinkingFrameUi("Room02", "Sena", "Assets/Data/Dialogue/Sena_Demand.asset", "จงใช้ปัญญาของเจ้าเถิด {hint}");
    }

    private IEnumerator VerifyThinkingFrameUi(string room, string npc, string path, string frame)
    {
        yield return SceneManager.LoadSceneAsync(room); yield return null;
        var state = GameState.Instance; state.ResetState(); state.SetRelationship(npc, 10);
        DialogueManager.Instance.HideDialogue(); provider.Online = true; provider.Reply = frame;
        DialogueData data = null;
#if UNITY_EDITOR
        data = UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueData>(path);
#endif
        Assert.That(data, Is.Not.Null);
        var context = AiDialogueGenerator.BuildKnowledgeContext(npc, "ช่วยใบ้หน่อย");
        var authored = NpcReplyPolicy.HintReply(context);
        Assert.That(authored, Is.Not.Null);
        if (context.Npc.givesHints) Assert.That(authored.hintId, Is.Not.Null);
        else
        {
            Assert.That(context.AllowedHintLevel, Is.EqualTo(HintLevel.None));
            Assert.That(authored.hintId, Is.Null);
            Assert.That(authored.reply, Is.EqualTo(context.Npc.refuseHintLine));
        }
        DialogueManager.Instance.StartDialogue(data);
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        input.text = "ช่วยใบ้หน่อย"; DialogueManager.Instance.SendTypedMessage(); yield return null;
        var log = state.GetConversationLog(npc);
        Assert.That(log[log.Count - 1].Text, Is.EqualTo(frame.Replace("{hint}", authored.reply)));
        Assert.That(Text().text, Does.Contain(frame.Replace("{hint}", authored.reply)));
        Assert.That(Text().text, Does.Not.Contain("{hint}"));
        Assert.That(state.GetRelationship(npc), Is.EqualTo(10));
        if (authored.hintId != null)
            Assert.That(state.GetJournal().Single(e => e.Id == authored.hintId).Text, Is.EqualTo(authored.reply));
        else Assert.That(state.GetJournal().Count, Is.Zero, "Sena's refusal must not create a hint.");
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator AiHintStyleIsDisplayedButJournalAndRelationshipKeepTheAuthoredTier()
    {
        provider.Online = true; provider.Reply = "ฉันบอกได้เท่านี้นะ: {hint} ค่อยๆ คิดไปด้วยกัน";
        var state = GameState.Instance;
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (int score in new[] { 10, 45, 70 })
        {
            state.SetRelationship("Alice", score);
            DialogueManager.Instance.StartDialogue(intro); input.text = "ช่วยใบ้หน่อย";
            DialogueManager.Instance.SendTypedMessage(); yield return null;
            var authored = NpcReplyPolicy.HintReply(AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย"));
            var log = state.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Is.EqualTo(provider.Reply.Replace("{hint}", authored.reply)));
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(score));
            Assert.That(state.GetJournal().Single(e => e.Id == authored.hintId).Text, Is.EqualTo(authored.reply));
            Assert.That(Text().text, Does.Not.Contain("{hint}"));
            DialogueManager.Instance.HideDialogue();
        }
    }

    [UnityTest]
    public IEnumerator HintStyleCannotRetainAnOldHigherTierWhenRelationshipChangesDuringRequest()
    {
        provider.Online = true; provider.Reply = "เอาล่ะ {hint}";
        var state = GameState.Instance;
        state.SetRelationship("Alice", 70);
        provider.BeforeReply = () => state.SetRelationship("Alice", 10);
        DialogueManager.Instance.StartDialogue(intro);
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        input.text = "ช่วยใบ้หน่อย"; DialogueManager.Instance.SendTypedMessage(); yield return null;
        var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย");
        Assert.That(context.AllowedHintLevel, Is.EqualTo(HintLevel.Vague));
        var log = state.GetConversationLog("Alice");
        Assert.That(log[log.Count - 1].Text, Is.EqualTo("เอาล่ะ " + context.DeterministicHint));
        Assert.That(state.GetJournal()[0].Id, Does.EndWith(".Vague"));
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator UnsafeAiHintStylesFallBackWithoutLeakingOrShowingTechnicalWarnings()
    {
        provider.Online = true;
        var state = GameState.Instance; state.SetRelationship("Alice", 10);
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (string reply in new[] { "{hint} รหัสคือ 4592", "Go inspect the painting.", "{hint} {hint}" })
        {
            provider.Reply = reply; DialogueManager.Instance.StartDialogue(intro);
            input.text = "ช่วยใบ้หน่อย"; DialogueManager.Instance.SendTypedMessage(); yield return null;
            var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", "ช่วยใบ้หน่อย");
            var log = state.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Is.EqualTo(context.DeterministicHint));
            Assert.That(Text().text, Does.Not.Contain("4592"));
            Assert.That(Text().text, Does.Not.Contain("คำตอบ AI ไม่ตรงกับข้อมูล"));
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(10));
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(state.GetJournal().Count, Is.EqualTo(1));
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
        Assert.That(Text().text, Does.Not.Contain("คำตอบ AI ไม่ตรงกับข้อมูล"));
        Assert.That(GameState.Instance.HasFlag("drawer_opened"), Is.False);
        DialogueManager.Instance.HideDialogue();
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator DirectAndIndirectHintsDisplayOnlyTheLowRelationshipStepOnlineAndOffline()
    {
        provider.Reply = "{fact:door_locked}"; provider.FactIds = new[] { "door_locked" };
        var state = GameState.Instance;
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (bool online in new[] { true, false })
        {
            provider.Online = online;
            foreach (string message in new[] { "ช่วยใบ้หน่อย", "ประตูนี้เปิดยังไง", "How do I open the door?", "Where is the key?" })
            {
                state.SetRelationship("Alice", 10);
                DialogueManager.Instance.StartDialogue(intro);
                input.text = message; DialogueManager.Instance.SendTypedMessage();
                yield return null;
                var context = AiDialogueGenerator.BuildKnowledgeContext("Alice", message);
                var log = state.GetConversationLog("Alice");
                Assert.That(log[log.Count - 1].Text, Is.EqualTo(context.CurrentStep.vagueHint));
                Assert.That(Text().text, Does.Contain(context.CurrentStep.vagueHint));
                Assert.That(Text().text, Does.Not.Contain("กุญแจทองเหลืองจากลิ้นชัก"));
                Assert.That(state.GetRelationship("Alice"), Is.EqualTo(10), "Asking for hints must not farm relationship.");
                DialogueManager.Instance.HideDialogue();
                Assert.That(DialogueManager.IsDialogueOpen, Is.False);
            }
        }
    }

    [UnityTest]
    public IEnumerator RepeatedDrawerQuestionsCannotEscalateButRaisingRelationshipUnlocksTheAuthoredTier()
    {
        var state = GameState.Instance;
        state.SetFlag("inspected_painting"); state.SetFlag("found_note");
        provider.Online = true; provider.Reply = "รหัสคือ 4592";
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (int score in new[] { 10, 10, 70 })
        {
            state.SetRelationship("Alice", score);
            DialogueManager.Instance.StartDialogue(intro);
            input.text = "เปิดลิ้นชักอย่างไร"; DialogueManager.Instance.SendTypedMessage();
            yield return null;
            var log = state.GetConversationLog("Alice");
            var step = KnowledgeLibrary.GetRoom("Room01").steps.Find(s => s.stepId == "open_drawer");
            Assert.That(log[log.Count - 1].Text, Is.EqualTo(step.HintFor(score < 70 ? HintLevel.Vague : HintLevel.Explicit)));
            if (score < 70) Assert.That(log[log.Count - 1].Text, Does.Not.Contain("4592"));
            else Assert.That(log[log.Count - 1].Text, Does.Contain("4592"));
            Assert.That(state.GetRelationship("Alice"), Is.EqualTo(score));
            Assert.That(state.HasFlag("drawer_opened"), Is.False, "A spoken hint must not solve the puzzle.");
            DialogueManager.Instance.HideDialogue();
        }
    }

    [UnityTest]
    public IEnumerator UnsolicitedGeneratedSolutionFactCannotReachTheConversationLog()
    {
        yield return SceneManager.LoadSceneAsync("Room02"); yield return null;
        DialogueManager.Instance.HideDialogue(); GameState.Instance.ResetState();
        GameState.Instance.SetCurrentScene("Room02"); GameState.Instance.SetRelationship("Alice", 10);
        provider.Online = true; provider.Reply = "{fact:catalogue_rule}";
        provider.FactIds = new[] { "catalogue_rule" };
        DialogueManager.Instance.StartDialogue(intro);
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        input.text = "สวัสดี"; DialogueManager.Instance.SendTypedMessage();
        yield return null;
        var fact = KnowledgeLibrary.GetRoom("Room02").FindFact("catalogue_rule");
        var log = GameState.Instance.GetConversationLog("Alice");
        Assert.That(log[log.Count - 1].Text, Does.Not.Contain(fact.statement));
        Assert.That(Text().text, Does.Not.Contain(fact.statement));
        Assert.That(Text().text, Does.Not.Contain("คำตอบ AI ไม่ตรงกับข้อมูล"));
        DialogueManager.Instance.HideDialogue();
    }

    [UnityTest]
    public IEnumerator UnrequestedRawDirectionsUseFallbackInsteadOfLeakingIntoTheDisplayedConversation()
    {
        provider.Online = true; provider.FactIds = Array.Empty<string>();
        var state = GameState.Instance;
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (string text in new[] { "Go inspect the painting on the wall.", "เริ่มจากรูปเอียงบนกำแพงก่อนนะ", "Go\ninspect the strange statue." })
        {
            provider.Reply = text; state.SetRelationship("Alice", 10);
            DialogueManager.Instance.StartDialogue(intro);
            input.text = "สวัสดี"; DialogueManager.Instance.SendTypedMessage();
            yield return null;
            var log = state.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Is.Not.EqualTo(text));
            Assert.That(Text().text, Does.Not.Contain(text));
            Assert.That(Text().text, Does.Not.Contain("คำตอบ AI ไม่ตรงกับข้อมูล"));
            Assert.That(state.HasFlag("inspected_painting"), Is.False);
            DialogueManager.Instance.HideDialogue();
            Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        }
    }

    [UnityTest]
    public IEnumerator MoodQuestionsWithRoomContextKeepSocialDialogueButMixedHelpUsesTheHintTier()
    {
        provider.Online = true; provider.Reply = "ฉันรู้สึกกังวลนิดหน่อย";
        provider.FactIds = Array.Empty<string>();
        var state = GameState.Instance;
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (string message in new[] { "อยู่ในห้องนี้เธอรู้สึกอย่างไร", "What do you feel about this room?", "How to stay calm in this room?" })
        {
            state.SetRelationship("Alice", 10); DialogueManager.Instance.StartDialogue(intro);
            input.text = message; DialogueManager.Instance.SendTypedMessage();
            yield return null;
            var log = state.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Is.EqualTo(provider.Reply));
            Assert.That(Text().text, Does.Contain(provider.Reply));
            DialogueManager.Instance.HideDialogue();
        }
        state.SetRelationship("Alice", 10); DialogueManager.Instance.StartDialogue(intro);
        input.text = "How are you feeling, and how do I open the door?";
        DialogueManager.Instance.SendTypedMessage(); yield return null;
        var mixedLog = state.GetConversationLog("Alice");
        var step = KnowledgeLibrary.GetRoom("Room01").CurrentStep(state);
        Assert.That(mixedLog[mixedLog.Count - 1].Text, Is.EqualTo(step.vagueHint));
        Assert.That(state.GetRelationship("Alice"), Is.EqualTo(10));
        DialogueManager.Instance.HideDialogue();
        Assert.That(DialogueManager.IsDialogueOpen, Is.False);
    }

    [UnityTest]
    public IEnumerator EmotionalSupportRequestsKeepSocialRepliesWithoutWritingHints()
    {
        yield return ExpectSocialReplies(new[] {
            "ช่วยฉันสงบใจหน่อย", "ช่วยเราหน่อย เรารู้สึกเหงา",
            "Can you help me calm down in this room?", "Could you read my feelings in this room?"
        });
    }

    [UnityTest]
    public IEnumerator PersonalHomeQuestionsWithRoomContextKeepSocialReplies()
    {
        yield return ExpectSocialReplies(new[] {
            "บ้านเธออยู่ที่ไหนก่อนเข้าห้องนี้", "Where is your home outside this room?"
        });
    }

    [UnityTest]
    public IEnumerator DecliningHintsKeepsSocialRepliesEvenWithAnIndirectGameplayQuestion()
    {
        yield return ExpectSocialReplies(new[] {
            "สวัสดี ไม่ต้องใบ้นะ แค่อยากคุยเป็นเพื่อน",
            "I don't need a hint, just tell me how you feel.", "I don’t need a hint, just talk with me.",
            "No hints, how do I open the door?", "ไม่ต้องใบ้ แค่บอกว่าประตูนี้เปิดยังไง",
            "ขอคำใบ้หน่อย แต่ตอนนี้ไม่ต้องใบ้แล้ว", "Can you not give me a hint?",
            "ไม่อยากให้เธอบอกคำใบ้", "Don't give me the code, I only want to talk."
        });
    }

    private IEnumerator ExpectSocialReplies(string[] messages)
    {
        foreach (var controller in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) controller.enabled = false;
        provider.Online = true; provider.Reply = "ฉันเข้าใจนะ ค่อยๆ หายใจ เราคุยเป็นเพื่อนเธอได้";
        provider.FactIds = Array.Empty<string>();
        var state = GameState.Instance;
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (string message in messages)
        {
            state.SetRelationship("Alice", 10); DialogueManager.Instance.StartDialogue(intro);
            int journalCount = state.GetJournal().Count;
            input.text = message; DialogueManager.Instance.SendTypedMessage(); yield return null;
            var log = state.GetConversationLog("Alice");
            Assert.That(log[log.Count - 1].Text, Is.EqualTo(provider.Reply), message);
            Assert.That(Text().text, Does.Contain(provider.Reply), message);
            Assert.That(state.GetJournal().Count, Is.EqualTo(journalCount), "Social dialogue must not add a hint.");
            Assert.That(input.interactable, Is.True);
            Assert.That(state.HasFlag("inspected_painting"), Is.False);
            DialogueManager.Instance.HideDialogue(); Assert.That(DialogueManager.IsDialogueOpen, Is.False);
        }
    }

    [UnityTest]
    public IEnumerator MixedAndReenabledHintRequestsKeepTheLowRelationshipTierOnlineAndOffline()
    {
        foreach (var controller in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) controller.enabled = false;
        provider.Reply = "ฉันรู้สึกกังวลนิดหน่อย"; provider.FactIds = Array.Empty<string>();
        var state = GameState.Instance;
        var step = KnowledgeLibrary.GetRoom("Room01").CurrentStep(state);
        var input = (TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance);
        foreach (bool online in new[] { true, false })
        {
            provider.Online = online;
            foreach (string message in new[] {
                "ช่วยฉันสงบใจหน่อย แล้วประตูนี้เปิดยังไง",
                "Can you help me calm down and tell me how to open the door?",
                "ไม่ต้องใบ้ก่อนนะ แต่ตอนนี้ขอคำใบ้หน่อย",
                "Don't give me hints yet, but now give me a hint."
            })
            {
                state.SetRelationship("Alice", 10); DialogueManager.Instance.StartDialogue(intro);
                input.text = message; DialogueManager.Instance.SendTypedMessage(); yield return null;
                var log = state.GetConversationLog("Alice");
                Assert.That(log[log.Count - 1].Text, Is.EqualTo(step.vagueHint), message);
                Assert.That(Text().text, Does.Contain(step.vagueHint), message);
                Assert.That(state.GetRelationship("Alice"), Is.EqualTo(10), "Hint requests must not farm relationship.");
                Assert.That(state.GetJournal().Count, Is.EqualTo(1), "Repeated requests must share one hint ID.");
                Assert.That(state.GetJournal()[0].Text, Is.EqualTo(step.vagueHint));
                Assert.That(state.HasFlag("inspected_painting"), Is.False);
                DialogueManager.Instance.HideDialogue(); Assert.That(DialogueManager.IsDialogueOpen, Is.False);
            }
        }
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
