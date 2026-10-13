using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MysteryGame.Companions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CompanionUsabilityPlayTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private NpcCompanionController actor;
    private bool persistence, title, intro;
    private Offline provider;
    private GameState State { get { return GameState.Instance; } }
    private CompanionStationData Box { get { return actor.Scenario.stations[0]; } }
    private class Offline : IAiDialogueProvider
    {
        public int Calls;
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { Calls++; done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        { Calls++; done(new GeneratedChatReply { reply = "หยุดแล้ว ฉันเจอรหัส 4592" }); yield break; }
    }
    private static void Title(bool value) { typeof(TitleMenu).GetProperty("IsOpen").SetValue(null, value); }
    private static void Intro(bool value) { typeof(IntroSequence).GetProperty("IsPlaying").SetValue(null, value); }
    [UnitySetUp]
    public IEnumerator Setup()
    {
        persistence = SaveSystem.PersistenceEnabled; SaveSystem.PersistenceEnabled = false;
        title = TitleMenu.IsOpen; intro = IntroSequence.IsPlaying;
        provider = new Offline(); DialogueProviders.Override = provider; GameDefinition.Override = null; KnowledgeLibrary.ClearCache();
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
    private void PlayerAt(Vector2 point) { GameObject.FindGameObjectWithTag("Player").transform.position += (Vector3)(point - actor.PlayerFeet); Physics2D.SyncTransforms(); }
    private void ActorAt(Vector2 point) { actor.transform.position += (Vector3)(point - actor.Feet); Physics2D.SyncTransforms(); }
    private void Tick(float seconds) { for (int i = 0; i < seconds / 0.05f; i++) { actor.Tick(0.05f); Physics2D.SyncTransforms(); } }
    private void Chat() { DialogueManager.Instance.StartDialogue(UnityEditor.AssetDatabase.LoadAssetAtPath<DialogueData>("Assets/Data/Dialogue/Alice_Intro.asset")); }
    private void Send(string text)
    { ((TMPro.TMP_InputField)typeof(DialogueManager).GetField("chatInput", Private).GetValue(DialogueManager.Instance)).text = text; DialogueManager.Instance.SendTypedMessage(); }
    private IEnumerator Capture(string name)
    {
        if (!Environment.GetCommandLineArgs().Contains("-npc-work-screenshots")) yield break;
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/NpcGameplayFix_20261013/" + name + ".png"));
        var camera = Camera.main;
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active; float previousAspect = camera.aspect;
        var target = new RenderTexture(1280, 720, 24); var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = 1280f / 720f; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply(); System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; camera.aspect = previousAspect; RenderTexture.active = previousActive;
            UnityEngine.Object.Destroy(texture); target.Release(); UnityEngine.Object.Destroy(target);
        }
        Assert.That(System.IO.File.Exists(path), Is.True, "Rendered screenshot must actually be saved.");
    }

    [UnityTest]
    public IEnumerator NaturalStopRequestsActuallyStopMovementAfterClosingChat()
    {
        foreach (string stop in new[] { "หยุดเดิน", "ช่วยหยุดเดินหน่อย", "หยุดเดินก่อนนะ", "รอก่อน", "หยุด", "stop walking", "อย่าเดินตามเรา", "หยุดหาหลักฐานก่อน" })
        {
            State.ResetState(); ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint); Chat();
            Send("ตามเรามาหน่อย"); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
            Send(stop); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), stop);
            DialogueManager.Instance.HideDialogue(); Vector2 before = actor.Feet;
            PlayerAt(new Vector2(2, -3.4f)); Tick(3f);
            Assert.That(actor.Feet, Is.EqualTo(before), stop); Assert.That(actor.MovementSpeed, Is.Zero);
        }
        Assert.That(provider.Calls, Is.Zero, "Replies must represent engine actions, not model promises.");
    }
    [UnityTest]
    public IEnumerator SearchWalksInspectsAndFindsOnlyPhysicalAuthoredEvidence()
    {
        ActorAt(new Vector2(0, -3.4f)); PlayerAt(new Vector2(1, -3.4f)); Chat();
        Send("ช่วยไปหาหลักฐานในห้องนี้หน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Searching));
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("companion_box_marks")), Is.False);
        var before = actor.Feet; Tick(20); Assert.That(actor.Feet, Is.EqualTo(before), "Movement waits for dialogue to close.");
        DialogueManager.Instance.HideDialogue(); bool moved = false, inspecting = false;
        for (int i = 0; i < 600 && actor.Mode == CompanionMode.Searching; i++)
        {
            actor.Tick(0.05f); Physics2D.SyncTransforms();
            moved |= Vector2.Distance(before, actor.Feet) > 0.2f; inspecting |= actor.IsInspecting;
            Assert.That(actor.IsFree(actor.Feet), Is.True);
        }
        Assert.That(moved && inspecting, Is.True);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("companion_box_marks")), Is.True);
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("companion_box_marks", State), Is.True);
        Assert.That(State.GetJournal().Any(j => j.Text.Contains("รอยมือ")), Is.True);
        Assert.That(State.HasFlag("inspected_painting"), Is.False); Assert.That(State.HasFlag("found_note"), Is.False); Assert.That(State.HasItem("key"), Is.False);
        Assert.That(actor.TaskPhase, Is.EqualTo(CompanionTaskPhase.Completed)); Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator SearchCanBeStoppedBeforeItGrantsEvidenceAndRestoredSavesCancelIt()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint); Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null;
        Send("หยุดเดิน"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(10);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.GetJournal(), Is.Empty);
        Chat(); Send("ช่วยค้นหาเบาะแส"); yield return null; DialogueManager.Instance.HideDialogue();
        State.RestoreSnapshot(State.CreateSnapshot()); Tick(10);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting)); Assert.That(State.GetJournal(), Is.Empty);
    }
    [UnityTest]
    public IEnumerator BoxIsInActualFocusAndCanBeInspectedOpenedAndReadAgain()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint);
        yield return new WaitForFixedUpdate(); yield return null;
        var prop = UnityEngine.Object.FindObjectsOfType<CompanionStationInteraction>().Single(p => p.Station == Box);
        Assert.That(InteractionFocus.Current, Is.SameAs(prop));
        yield return Capture("BoxFocused");
        InteractionFocus.Current.Interact(); Assert.That(DialogueManager.IsDialogueOpen, Is.True);
        Assert.That(actor.ActiveStation, Is.Null, "Inspecting a prop must not command the NPC.");
        DialogueManager.Instance.HideDialogue(); Chat(); Send("ช่วยจับหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        DialogueManager.Instance.HideDialogue(); Tick(3.3f);
        Assert.That(actor.IsCompleted(Box), Is.True);
        yield return null; yield return Capture("BoxOpened");
        prop.Interact(); Assert.That(DialogueManager.IsDialogueOpen, Is.True);
        Assert.That(((TMPro.TMP_Text)typeof(DialogueManager).GetField("dialogueText", Private).GetValue(DialogueManager.Instance)).text, Does.Contain("ไม่มีใครต้องผ่านความมืด"));
        Assert.That(provider.Calls, Is.Zero);
    }
    [UnityTest]
    public IEnumerator RealGripHasMovingHandsAndStopsWhenCancelled()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint); Chat(); Send("ช่วยเปิดกล่องหน่อย"); yield return null;
        DialogueManager.Instance.HideDialogue(); actor.Tick(0.1f); yield return null;
        var visuals = actor.GetComponent<CompanionWorkVisuals>(); Assert.That(visuals.IsWorking, Is.True);
        Vector2 hand = visuals.RightHand; Vector2 feet = actor.Feet;
        float travel = 0f;
        for (int i = 0; i < 4; i++)
        { actor.Tick(0.2f); yield return null; travel = Mathf.Max(travel, Vector2.Distance(visuals.RightHand, hand)); }
        Assert.That(travel, Is.GreaterThan(0.01f), "Hands must move over the working cycle."); Assert.That(actor.Feet, Is.EqualTo(feet));
        yield return Capture("HelpingGrip");
        Chat(); Send("หยุดช่วยก่อน"); yield return null;
        Assert.That(visuals.IsWorking, Is.False); Assert.That(actor.ActiveStation, Is.Null);
    }
    [UnityTest]
    public IEnumerator SearchCanLearnPreviouslyDiscoveredMainEvidenceWithoutSolvingOrFarmingRewards()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint);
        State.SetFlag("inspected_painting"); State.SetFlag("found_note");
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("desk_note", State), Is.False);
        Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(actor.Mode, Is.Not.EqualTo(CompanionMode.Searching));
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("painting_arrow", State), Is.True);
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("desk_note", State), Is.True);
        Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
        int entries = State.GetJournal().Count, memories = State.GetNpcMemory("Alice").Count, relationship = State.GetRelationship("Alice");
        PlayerAt(actor.Feet + Vector2.right); Chat(); Send("ช่วยหาหลักฐานอีกครั้ง"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(State.GetJournal().Count, Is.EqualTo(entries)); Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(memories));
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship));
    }
}
