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

// Exercise trusted typed-send, displayed/logged replies and authoritative state with no API calls.
public class CompanionSpeechActPlayTests
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
    public IEnumerator AQuestionFollowedByAnExplicitStopMustStillStopFollowing()
    {
        var issues = new List<string>();
        foreach (string message in new[] {
            "เธอตามเรามาเหรอ, รอตรงนี้ก่อน",
            "เธอช่วยเปิดกล่องสองมือแล้วใช่ไหม, รอตรงนี้ก่อน",
            "เธอกำลังตามเรามาใช่ไหม, ไม่ต้องตามแล้ว" })
        {
            State.ResetState(); Position(); Assert.That(actor.RequestFollow(), Is.True);
            Chat(); Send(message); yield return null;
            string detail = message + " => mode=" + actor.Mode + "; logged=" + Logged;
            Debug.Log("REGRESSION stop send: " + detail);
            if (actor.Mode != CompanionMode.Waiting) issues.Add(detail);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }
    [UnityTest]
    public IEnumerator StatusQuestionVariantsMustNotStartWork()
    {
        var issues = new List<string>();
        foreach (string message in new[] {
            "เธอตามเรามาอยู่หรือเปล่า", "เธอช่วยเปิดกล่องสองมือไปแล้วรึเปล่า", "เธอช่วยเปิดกล่องสองมืออยู่หรือไม่" })
        {
            State.ResetState(); Position(); Chat(); Send(message); yield return null;
            string detail = message + " => mode=" + actor.Mode + "; active=" + (actor.ActiveStation != null) + "; logged=" + Logged;
            Debug.Log("REGRESSION question send: " + detail);
            if (actor.Mode != CompanionMode.Waiting || actor.ActiveStation != null) issues.Add(detail);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }
    [UnityTest]
    public IEnumerator AnInformationClauseCannotHideTheFollowingDirectRequest()
    {
        var provider = new Mock { Text = "ดีใจที่อยู่ด้วยกันนะ" }; DialogueProviders.Override = provider;
        foreach (string message in new[] {
            "กล่องสองมือเปิดยังไง, ตามเรามาหน่อย",
            "How do we open the box? Please follow me.",
            "Alice, please follow me" })
        {
            State.ResetState(); Position(); Chat(); Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), message);
            Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            DialogueManager.Instance.HideDialogue();
        }
        Assert.That(provider.Calls, Is.Zero, "A typed local command is not delegated to AI.");
    }
    [UnityTest]
    public IEnumerator ConflictingRequestsAndReportedSpeechNeverStartWork()
    {
        var provider = new Mock { Text = "ดีใจที่อยู่ด้วยกันนะ" }; DialogueProviders.Override = provider;
        foreach (string message in new[] {
            "ช่วยหน่อย, ตามเรามา แล้วรอตรงนี้",
            "Can you open the box and follow me?",
            "ตามเรามา รอตรงนี้ก่อน",
            "รอตรงนี้ช่วยเปิดกล่องสองมือ",
            "'Don't follow me', wait here",
            "ถ้าฉันบอกว่า, ตามเรามา" })
        {
            State.ResetState(); Position(); Chat(); Send(message); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), message);
            Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
            DialogueManager.Instance.HideDialogue();
        }
    }
    [UnityTest]
    public IEnumerator OrdinaryEmotionalRepliesFromMockShouldNotBeReplaced()
    {
        var provider = new Mock(); DialogueProviders.Override = provider; Chat();
        var issues = new List<string>(); int count = 0;
        foreach (string text in new[] {
            "เธอทำให้ฉันหายกังวลแล้ว", "เธอทำให้ฉันกล้าขึ้นแล้ว", "เธอทำให้ฉันเชื่อใจได้แล้ว",
            "ฉันช่วยให้เธอสงบลงแล้ว", "เธอทำให้ฉันหายเศร้าแล้ว", "เธอทำให้ฉันรู้สึกดีขึ้นแล้ว" })
        {
            provider.Text = text; Send("คุยเป็นเพื่อนหน่อย"); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(++count));
            string detail = text + " => logged=" + Logged + "; displayed=" + Displayed;
            Debug.Log("REGRESSION emotional mock: " + detail);
            if (!Logged.Contains(text) || !Displayed.Contains(text)) issues.Add(detail);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }
    [UnityTest]
    public IEnumerator UnverifiedPassiveAndImplicitCompletionMustNotReachThePlayer()
    {
        var provider = new Mock(); DialogueProviders.Override = provider; Chat();
        var issues = new List<string>(); int count = 0;
        foreach (string text in new[] { "It has already been opened for you.", "We are finally done.", "ฉันจัดการให้เรียบร้อยแล้วค่ะ", "I've been following you." })
        {
            provider.Text = text; Send("คุยเป็นเพื่อนหน่อย"); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(++count));
            string detail = text + " => logged=" + Logged + "; displayed=" + Displayed + "; completed=" + actor.IsCompleted(station);
            Debug.Log("REGRESSION completion mock: " + detail);
            if (Logged.Contains(text) || Displayed.Contains(text)) issues.Add(detail);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(actor.ActiveStation, Is.Null);
            Assert.That(actor.IsCompleted(station), Is.False); Assert.That(State.HasItem("key"), Is.False);
        }
        Assert.That(issues, Is.Empty, string.Join("\n", issues));
    }
}
