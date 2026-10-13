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
using UnityEngine.TestTools;

// Real typed-send and mock replies: no API calls or persistent saves.
public class CompanionQualifierPlayTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private NpcCompanionController actor;
    private CompanionStationData station;
    private bool persistence, title, intro;
    private GameState State { get { return GameState.Instance; } }
    private class Offline : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null) { done(null); yield break; }
    }
    private class Mock : IAiDialogueProvider
    {
        public string Text;
        public int Calls;
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message,
            Action<GeneratedChatReply> done, string personality = null)
        {
            Calls++;
            done(new GeneratedChatReply { reply = Text, playerTone = "neutral", relationshipDelta = 0,
                referencedFactIds = Array.Empty<string>() });
            yield break;
        }
    }
    private static void Title(bool value) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, value); }
    private static void Intro(bool value) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, value); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        DialogueProviders.Override = new Offline(); GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        if (State != null) UnityEngine.Object.DestroyImmediate(State.gameObject);
        yield return SceneManager.LoadSceneAsync("Room01"); yield return null;
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        foreach (var events in UnityEngine.Object.FindObjectsOfType<NpcEventController>()) events.enabled = false;
        State.ResetState(); actor = NpcCompanionController.Find("Alice", "Room01");
        Assert.That(actor, Is.Not.Null); station = actor.Scenario.stations[0]; Position();
        Assert.That(InputGate.IsBlocked, Is.False);
    }
    [TearDown]
    public void Cleanup()
    {
        Title(false); Intro(false); RoomTransitionManager.CloseRoomOverlays();
        DialogueProviders.Override = null; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
        SaveSystem.PersistenceEnabled = persistence; Title(title); Intro(intro);
    }
    private void Position()
    {
        actor.transform.position += (Vector3)(station.companionPoint - actor.Feet);
        GameObject.FindGameObjectWithTag("Player").transform.position += (Vector3)(station.playerPoint - actor.PlayerFeet);
        Physics2D.SyncTransforms();
    }
    private void Chat()
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
        ((TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance)).text = message;
        DialogueManager.Instance.SendTypedMessage();
    }
    private string Logged { get { return State.GetConversationLog("Alice").Last().Text; } }
    private string Displayed { get { return ((TMP_Text)typeof(DialogueManager).GetField("dialogueText", Private).GetValue(DialogueManager.Instance)).text; } }

    [UnityTest]
    public IEnumerator TrailingConfirmationOrCancellationMustNotStartAnAction()
    {
        var issues = new List<string>();
        foreach (string message in new[] {
            "เธอตามเรามา, ใช่ไหม",
            "เธอช่วยเปิดกล่องสองมือไปแล้ว, ใช่ไหม",
            "เธอช่วยเปิดกล่องสองมือไปแล้ว, จริงไหม",
            "ตามเรามาหน่อย, ไม่ต้องแล้ว",
            "ช่วยเปิดกล่องสองมือหน่อย, ไม่เอาแล้ว",
            "Follow me, never mind" })
        {
            State.ResetState(); Position(); Chat(); Send(message); yield return null;
            string detail = message + " => mode=" + actor.Mode + "; active=" + (actor.ActiveStation != null) + "; logged=" + Logged;
            Debug.Log("QUALIFIER_REGRESSION trailing send: " + detail);
            if (actor.Mode != CompanionMode.Waiting || actor.ActiveStation != null) issues.Add(detail);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }

    [UnityTest]
    public IEnumerator ResolveRetractionsAndTagsBeforeApplyingTheRemainingRequest()
    {
        var provider = new Mock { Text = "ดีใจที่อยู่ด้วยกันนะ" }; DialogueProviders.Override = provider;
        string[] messages = {
            "ตามเรามาหน่อย, ไม่ต้องแล้ว, รอตรงนี้ก่อน",
            "ตามเรามาหน่อย, รอตรงนี้ก่อน, ไม่เอาแล้ว, ช่วยเปิดกล่องสองมือ",
            "ตามเรามาหน่อย, เธอช่วยเปิดกล่องสองมือไปแล้ว, ใช่ไหม",
            "เธอตามเรามา, ใช่ไหม, รอตรงนี้ก่อน",
            "Can you follow me, please?" };
        CompanionMode[] modes = { CompanionMode.Waiting, CompanionMode.GoingToHelp,
            CompanionMode.Following, CompanionMode.Waiting, CompanionMode.Following };
        for (int i = 0; i < messages.Length; i++)
        {
            State.ResetState(); Position(); Chat(); Send(messages[i]); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(modes[i]), messages[i]);
            Assert.That(actor.ActiveStation == station, Is.EqualTo(i == 1), messages[i]);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(provider.Calls, Is.Zero, "The final bounded player request remains local.");
    }

    [UnityTest]
    public IEnumerator NormalizedQuotedSpeechMustRemainNonExecutable()
    {
        var issues = new List<string>();
        foreach (string message in new[] {
            "＇ช่วยเปิดกล่องสองมือ, ตามเรามาหน่อย＇",
            "'ช่วยเปิดกล่องสองมือ, ตามเรามาหน่อย'\u200b" })
        {
            State.ResetState(); Position(); Chat(); Send(message); yield return null;
            string detail = message + " => mode=" + actor.Mode + "; active=" + (actor.ActiveStation != null) + "; logged=" + Logged;
            Debug.Log("QUALIFIER_REGRESSION quote send: " + detail);
            if (actor.Mode != CompanionMode.Waiting || actor.ActiveStation != null) issues.Add(detail);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }

    [UnityTest]
    public IEnumerator ImplicitTaskCompletionMustNotReachUiOrConversationHistory()
    {
        var provider = new Mock(); DialogueProviders.Override = provider; Chat();
        var issues = new List<string>(); int count = 0;
        foreach (string text in new[] {
            "งานเสร็จแล้วค่ะ", "ตอนนี้เสร็จแล้วค่ะ", "เราสำเร็จแล้ว", "ฉันช่วยแก้ให้เสร็จแล้ว" })
        {
            provider.Text = text; Send("คุยเป็นเพื่อนหน่อย"); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(++count));
            string detail = text + " => logged=" + Logged + "; displayed=" + Displayed;
            Debug.Log("QUALIFIER_REGRESSION implicit completion: " + detail);
            if (Logged.Contains(text) || Displayed.Contains(text)) issues.Add(detail);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }

    [UnityTest]
    public IEnumerator SocialUsesOfDoneAndHoldingMustNotBeReplacedByFallback()
    {
        var provider = new Mock(); DialogueProviders.Override = provider; Chat();
        var issues = new List<string>(); int count = 0;
        foreach (string text in new[] {
            "We're done talking for today.", "I'm holding back tears.",
            "We have finally finished talking for today.", "เธอทำให้ฉันหายกังวลแล้ว" })
        {
            provider.Text = text; Send("คุยเป็นเพื่อนหน่อย"); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(++count));
            string detail = text + " => logged=" + Logged + "; displayed=" + Displayed;
            Debug.Log("QUALIFIER_REGRESSION social mock: " + detail);
            if (!Logged.Contains(text) || !Displayed.Contains(text)) issues.Add(detail);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }
}
