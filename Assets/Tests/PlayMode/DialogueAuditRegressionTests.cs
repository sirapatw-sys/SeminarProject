using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class DialogueAuditRegressionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    NpcCompanionController actor;
    bool persistence, title, intro;
    Provider provider;
    GameState State => GameState.Instance;
    class Provider : IAiDialogueProvider
    {
        public bool Enabled = true;
        public int Calls;
        public string Text = "ฉันฟังอยู่ เล่าต่อได้เลย";
        public GeneratedDialogueContent Event;
        public bool CanGenerate => Enabled;
        public string LastError => "";
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { Calls++; done(Event); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        { Calls++; done(new GeneratedChatReply { reply = Text, referencedFactIds = Array.Empty<string>() }); yield break; }
    }
    static void Title(bool value) => typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, value);
    static void Intro(bool value) => typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, value);
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        provider = new Provider(); DialogueProviders.Override = provider;
        GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState(); actor = NpcCompanionController.Find("Alice", "Room01"); Assert.That(actor, Is.Not.Null);
    }
    [TearDown]
    public void Cleanup()
    {
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = persistence; Title(title); Intro(intro);
    }
    void Position()
    {
        var box = actor.Scenario.stations[0];
        actor.transform.position += (Vector3)(box.companionPoint - actor.Feet);
        GameObject.FindGameObjectWithTag("Player").transform.position += (Vector3)(box.playerPoint - actor.PlayerFeet);
        Physics2D.SyncTransforms();
    }
    void Chat()
    {
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", Private).GetValue(actor.GetComponent<NPCInteraction>());
        DialogueManager.Instance.StartDialogue(data);
    }
    void Send(string text)
    {
        ((TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance)).text = text;
        DialogueManager.Instance.SendTypedMessage();
    }
    TMP_Text Text => (TMP_Text)typeof(DialogueManager).GetField("dialogueText", Private).GetValue(DialogueManager.Instance);
    MiniEventData Chatter => ((List<MiniEventData>)typeof(NpcEventController).GetField("events", Private)
        .GetValue(actor.GetComponent<NpcEventController>())).First(e => e.freeTopic);

    [UnityTest]
    public IEnumerator WithdrawalPunctuationStopsFollowingSearchingAndHelpingWithoutProvider()
    {
        foreach (var mode in new[] { CompanionMode.Following, CompanionMode.Searching, CompanionMode.Helping })
        foreach (var stop in new[] { "ไม่ต้องแล้ว!", "ยกเลิก.", "ไม่เอาแล้ว?", "อลิซ ไม่ต้องแล้วค่ะ…" })
        {
            RoomTransitionManager.CloseRoomOverlays(); State.ResetState(); Position();
            if (mode == CompanionMode.Following) Assert.That(actor.RequestFollow(), Is.True);
            else if (mode == CompanionMode.Searching) { Chat(); Send("ช่วยหาหลักฐานหน่อย"); DialogueManager.Instance.HideDialogue(); }
            else { Assert.That(actor.RequestHelp(actor.Scenario.stations[0]), Is.True); actor.Tick(0.05f); }
            Assert.That(actor.Mode, Is.EqualTo(mode)); Chat(); Send(stop); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), stop);
            Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.ActiveSearchSpot, Is.Null);
            DialogueManager.Instance.HideDialogue();
            Vector2 before = actor.Feet; actor.Tick(0.5f); Assert.That(actor.Feet, Is.EqualTo(before));
        }
        Assert.That(provider.Calls, Is.Zero);
    }

    [UnityTest]
    public IEnumerator IdleWithdrawalAndQuotedCancellationStaySocialButCourtesyStartsRealWork()
    {
        Position(); Chat(); Send("ไม่ต้องแล้ว!"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(provider.Calls, Is.EqualTo(1));
        Send("ตามมาหน่อยนะ ถ้าไม่ลำบากค่ะ"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(provider.Calls, Is.EqualTo(1));
        Send("เขาบอกว่า ไม่ต้องแล้ว!"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following)); Assert.That(provider.Calls, Is.EqualTo(2));
        Send("หยุดเดิน"); yield return null;
        Send("ถ้าประตูเปิดแล้ว ตามมา"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Send("ช่วยจับไว้หน่อย ถ้าสะดวกนะคะ!"); yield return null;
        Assert.That(actor.ActiveStation, Is.EqualTo(actor.Scenario.stations[0]));
    }

    GeneratedDialogueContent SwappedChoices()
    {
        return new GeneratedDialogueContent {
            lines = new[] { "วันนี้อยากคุยเป็นเพื่อนสักหน่อย" }, referencedFactIds = Array.Empty<string>(),
            choices = new[] {
                new GeneratedDialogueChoice { optionText = "ไม่อยากฟัง เลิกคุยเถอะ", responseText = "อืม ไปเถอะ" },
                new GeneratedDialogueChoice { optionText = "ทำไมถึงคิดอย่างนั้น", responseText = "ก็แค่คิดขึ้นมาน่ะ" },
                new GeneratedDialogueChoice { optionText = "ฉันตั้งใจฟังอยู่ เล่าต่อเลย", responseText = "ขอบใจนะที่รับฟัง" }
            }
        };
    }
    [UnityTest]
    public IEnumerator FreeTopicProviderCannotSwapChoicesRepliesOrTheirEffects()
    {
        Position(); provider.Event = SwappedChoices();
        var ev = UnityEngine.Object.Instantiate(Chatter); ev.minimumRoomTimeSeconds = 0f;
        var host = new GameObject("FreeTopicRegression"); var controller = host.AddComponent<NpcEventController>();
        try
        {
            string reason;
            Assert.That(NpcReplyPolicy.ValidateEvent(NpcKnowledgeContextBuilder.Build("Alice", "Room01", State, false),
                provider.Event, out reason), Is.True, "This output passes fact policy; choice safety must be structural: " + reason);
            foreach (int selected in new[] { 0, 2 })
            {
                State.ResetState(); Position();
                typeof(NpcEventController).GetMethod("QueueEvent", Private).Invoke(controller, new object[] { ev });
                yield return null;
                Assert.That(controller.TryStartPendingEvent(), Is.True);
                Assert.That(Text.text, Is.EqualTo(provider.Event.lines[0]), "Keep the valid generated topic.");
                AssertChoiceEffects(ev.dialogue, selected);
                DialogueManager.Instance.HideDialogue();
            }
        }
        finally { UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(ev); }
    }
    void AssertChoiceEffects(DialogueData authored, int selected)
    {
        var choices = (List<DialogueChoiceData>)typeof(DialogueManager).GetField("activeChoices", Private).GetValue(DialogueManager.Instance);
        for (int i = 0; i < choices.Count; i++)
        {
            Assert.That(choices[i].optionText, Is.EqualTo(authored.choices[i].optionText));
            Assert.That(choices[i].responseText, Is.EqualTo(authored.choices[i].responseText));
        }
        int before = State.GetRelationship("Alice");
        int delta = authored.choices[selected].actions.Where(a => a.type == ActionType.ChangeRelationship)
            .Sum(a => RelationshipTuning.ScaleGain(a.amount, RelationshipTuning.ChoiceGainScale));
        DialogueManager.Instance.SelectChoice(selected);
        Assert.That(Text.text, Does.StartWith(authored.choices[selected].responseText));
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(before + delta));
        foreach (var action in authored.choices[selected].actions.Where(a => a.type == ActionType.AddNpcMemory))
            Assert.That(State.GetNpcMemory("Alice"), Does.Contain(action.text));
        DialogueManager.Instance.SelectChoice(selected);
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(before + delta), "Double click cannot award twice.");
    }
    [UnityTest]
    public IEnumerator OfflineTopicsKeepTheirOwnLabelsRepliesAndTopicSpecificMemories()
    {
        Position(); provider.Enabled = false;
        var ev = UnityEngine.Object.Instantiate(Chatter); ev.minimumRoomTimeSeconds = 0f;
        var variant = ev.offlineVariants.First(v => v.choices[0].actions.Any(a =>
            a.type == ActionType.AddNpcMemory && !ev.dialogue.choices[0].actions.Any(b => b.text == a.text)));
        ev.offlineVariants = new List<DialogueData> { variant };
        var host = new GameObject("OfflineTopicRegression"); var controller = host.AddComponent<NpcEventController>();
        try
        {
            typeof(NpcEventController).GetMethod("QueueEvent", Private).Invoke(controller, new object[] { ev });
            Assert.That(controller.TryStartPendingEvent(), Is.True);
            Assert.That(Text.text, Is.EqualTo(variant.lines[0])); AssertChoiceEffects(variant, 0);
            Assert.That(provider.Calls, Is.Zero);
        }
        finally { UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(ev); }
        yield return null;
    }

    [UnityTest]
    public IEnumerator LongDialogueCanScrollToItsEndResizeAndResetForTheNextReply()
    {
        Position(); Chat();
        provider.Text = string.Concat(Enumerable.Repeat("ฉันรู้สึกสบายใจที่ได้คุยกับเธอ เราค่อยคุยกันไปก็ได้ ", 20));
        Send("วันนี้รู้สึกยังไงบ้าง"); yield return null; yield return null;
        Canvas.ForceUpdateCanvases();
        var layout = Text.GetComponentInParent<DialogueTextScroll>();
        Assert.That(layout, Is.Not.Null); layout.RefreshLayout(); Text.ForceMeshUpdate();
        var scroll = layout.GetComponent<ScrollRect>();
        Assert.That(Text.text, Does.Contain(provider.Text)); Assert.That(Text.isTextTruncated, Is.False);
        Assert.That(Text.fontSize, Is.EqualTo(25f)); Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
        Assert.That(scroll.verticalScrollbar.gameObject.activeSelf, Is.True);
        ExecuteEvents.Execute(scroll.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = Vector2.down }, ExecuteEvents.scrollHandler);
        Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1f), "Mouse wheel events must move the message.");
        scroll.verticalScrollbar.value = 0f; Canvas.ForceUpdateCanvases();
        AssertBottomReachable(scroll);
        var root = (RectTransform)layout.transform;
        Vector2 anchorsMin = root.anchorMin, anchorsMax = root.anchorMax, size = root.sizeDelta, position = root.anchoredPosition;
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f); root.sizeDelta = new Vector2(400f, 146f);
        Canvas.ForceUpdateCanvases(); layout.RefreshLayout(); yield return null;
        scroll.verticalNormalizedPosition = 0f; Canvas.ForceUpdateCanvases(); AssertBottomReachable(scroll);
        root.anchorMin = anchorsMin; root.anchorMax = anchorsMax; root.sizeDelta = size; root.anchoredPosition = position;
        Canvas.ForceUpdateCanvases();
        provider.Text = "ฉันฟังอยู่"; Send("สวัสดี"); yield return null; yield return null;
        Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.01f));
        Assert.That(Text.isTextTruncated, Is.False);
        // A typed transcript includes player text and insights, so even a short
        // reply may legitimately need scrolling. A one-line object page fits.
        DialogueManager.Instance.HideDialogue();
        DialogueManager.Instance.StartDialogue("บันทึก", new[] { "ข้อความสั้น" }, false);
        yield return null; yield return null;
        Assert.That(scroll.content.rect.height, Is.EqualTo(scroll.viewport.rect.height).Within(0.1f));
        Assert.That(scroll.verticalScrollbar.gameObject.activeSelf, Is.False);
        DialogueManager.Instance.HideDialogue(); Assert.That(DialogueManager.IsDialogueOpen, Is.False);
    }
    static void AssertBottomReachable(ScrollRect scroll)
    {
        var content = new Vector3[4]; var viewport = new Vector3[4];
        scroll.content.GetWorldCorners(content); scroll.viewport.GetWorldCorners(viewport);
        Assert.That(content[0].y, Is.EqualTo(viewport[0].y).Within(1f), "The end of the message must reach the viewport.");
    }
}
