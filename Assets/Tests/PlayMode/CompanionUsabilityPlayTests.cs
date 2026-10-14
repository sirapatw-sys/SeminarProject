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
        public string Text = "หยุดแล้ว ฉันเจอรหัส 4592";
        public string ProposedAction;
        public bool CanGenerate { get { return true; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { Calls++; done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        { Calls++; done(new GeneratedChatReply { reply = Text, referencedFactIds = Array.Empty<string>(), companionAction = ProposedAction }); yield break; }
    }
    private class Unavailable : IAiDialogueProvider
    {
        public bool CanGenerate { get { return false; } }
        public string LastError { get { return ""; } }
        public IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> done) { done(null); yield break; }
        public IEnumerator GenerateReply(string id, string name, string context, string message, Action<GeneratedChatReply> done, string personality = null)
        { done(null); yield break; }
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
    private void Chat()
    {
        var data = (DialogueData)typeof(NPCInteraction).GetField("dialogueData", Private).GetValue(actor.GetComponent<NPCInteraction>());
        DialogueManager.Instance.StartDialogue(data);
    }
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
    public IEnumerator SearchRevisitsDiscoveredMainEvidenceWithoutBypassingSharingOrDuplicatingIt()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint);
        State.SetFlag("inspected_painting"); State.SetFlag("found_note");
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("desk_note", State), Is.False);
        Chat(); Send("ช่วยหาหลักฐานหน่อย"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(actor.Mode, Is.Not.EqualTo(CompanionMode.Searching));
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("painting_arrow", State), Is.False);
        Assert.That(KnowledgeLibrary.GetNpc("Alice").KnowsFact("desk_note", State), Is.False);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("painting_arrow")), Is.False);
        Assert.That(State.HasFlag(actor.Scenario.SearchFlag("desk_note")), Is.False);
        Assert.That(State.GetJournal().Any(e => e.Id == actor.Scenario.SearchFlag("desk_note")), Is.False);
        Assert.That(State.HasFlag("drawer_opened"), Is.False); Assert.That(State.HasItem("key"), Is.False);
        int entries = State.GetJournal().Count, memories = State.GetNpcMemory("Alice").Count, relationship = State.GetRelationship("Alice");
        PlayerAt(actor.Feet + Vector2.right); Chat(); Send("ช่วยหาหลักฐานอีกครั้ง"); yield return null; DialogueManager.Instance.HideDialogue(); Tick(40f);
        Assert.That(State.GetJournal().Count, Is.EqualTo(entries)); Assert.That(State.GetNpcMemory("Alice").Count, Is.EqualTo(memories));
        Assert.That(State.GetRelationship("Alice"), Is.EqualTo(relationship));
    }

    [UnityTest]
    public IEnumerator ThaiAddressedRequestsActuallyChangeWorkWithoutCallingAi()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint);
        Chat(); Send("อลิซ ตามมา"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Send("อลิซ หยุดเดิน"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Send("อลิซ ช่วยหาหลักฐานหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Searching));
        Send("ไม่ต้องแล้ว"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Send("อลิซ ช่วยเปิดกล่องหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Assert.That(provider.Calls, Is.Zero);
        Assert.That(NpcReplyPolicy.MentionsGameControls(State.GetConversationLog("Alice").Last().Text), Is.False);
        Send("หยุดช่วยก่อน"); yield return null;
        Send("ช่วยจับไว้หน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Send("หยุดช่วยก่อน"); yield return null;
        Assert.That(provider.Calls, Is.Zero);
        // Unclear to the rules: the model reads it. A reply without a proposed
        // action never moves the companion or starts work.
        Send("ช่วยเดินอีกแบบหนึ่งหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(provider.Calls, Is.EqualTo(1), "Ambiguous requests go to the model when one is available.");
        // The mock's reply fails the text policy; an unclear request then asks
        // back instead of answering with an unrelated neutral line.
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("อยากให้ฉันทำอะไร"));
        // Offline, the same request asks back instead of guessing.
        DialogueProviders.Override = new Unavailable();
        Send("ช่วยเดินอีกแบบหนึ่งหน่อย"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain("อยากให้ฉันทำอะไร"));
    }

    [UnityTest]
    public IEnumerator ModelProposedActionsAreValidatedBeforeAnythingMoves()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint); Chat();
        // Wording the rules do not know: the model's proposal starts the real task,
        // and the answer is the companion's authored line, not the model's promise.
        provider.Text = "ได้สิ ฉันเดินตามไปแล้ว"; provider.ProposedAction = "follow";
        Send("เดินเป็นเพื่อนกันหน่อยนะ"); yield return null;
        Assert.That(provider.Calls, Is.EqualTo(1));
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Is.EqualTo(actor.Scenario.followLine));
        int relationship = State.GetRelationship("Alice");

        // Already following: no stock line, the model's (policy-checked) reply answers.
        provider.Text = "...อืม ฉันอยู่ข้างหลังเธอนี่แหละ"; provider.ProposedAction = "follow";
        Send("ขอบใจนะที่ไม่ทิ้งกัน"); yield return null;
        Assert.That(State.GetConversationLog("Alice").Last().Text, Is.Not.EqualTo(actor.Scenario.followLine));
        Assert.That(State.GetRelationship("Alice"), Is.GreaterThanOrEqualTo(relationship));

        // Rules veto hypothetical/quoted/confirming wording even if the model proposes.
        provider.ProposedAction = "wait";
        foreach (string vetoed in new[] { "ถ้าฉันบอกให้หยุด เธอจะหยุดไหม", "เขาบอกว่า \"รอตรงนี้\"", "เธอหยุดแล้วใช่ไหม" })
        {
            Send(vetoed); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following), vetoed);
        }

        // Unknown ids and locked stations never start work; a locked one gets the authored refusal.
        provider.ProposedAction = "help:no_such_station";
        Send("ช่วยทำอันนั้นที"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        var desk = actor.Scenario.stations[1];
        Assert.That(ConditionRule.AllHold(desk.conditions, State), Is.False, "Desk needs the painting first.");
        provider.ProposedAction = "help:" + desk.id;
        Send("ไปช่วยตรงโต๊ะหน่อยสิ"); yield return null;
        Assert.That(actor.ActiveStation, Is.Null);
        Assert.That(State.HasFlag("found_note"), Is.False);
        // Naming the desk while the model proposes a search walk: the named object
        // wins, so the locked desk is refused instead of a walk to the box.
        provider.ProposedAction = "search";
        Send("ไปช่วยตรงโต๊ะหน่อยสิ"); yield return null;
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
        Assert.That(actor.ActiveStation, Is.Null);

        // A valid proposal for the nearby box starts it through the normal validation.
        provider.ProposedAction = "help:" + Box.id;
        Send("มาช่วยกันตรงนี้หน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(Box));
        Assert.That(actor.IsCompleted(Box), Is.False, "Starting is not completing; the player still has to hold their side.");
    }

    [UnityTest]
    public IEnumerator DeskHelpCanBeAskedFromAcrossTheRoom()
    {
        State.SetFlag("inspected_painting");
        var desk = actor.Scenario.stations[1];
        // Face to face with Alice by the box, more than commandDistance from the desk.
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint);
        Assert.That(Vector2.Distance(actor.PlayerFeet, desk.playerPoint), Is.GreaterThan(actor.Scenario.commandDistance));
        Chat(); Send("ช่วยจับโต๊ะหน่อย"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(desk));
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("ยังไม่มีอะไรตรงนั้น"));
        Assert.That(provider.Calls, Is.Zero);
    }

    [UnityTest]
    public IEnumerator SocialRequestsWithTheirOwnObjectAreConversationNotAskBack()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint); Chat();
        provider.Text = "...อืม ฉันอยู่ตรงนี้นะ";
        foreach (string social in new[] { "ช่วยจับมือฉันหน่อย", "ช่วยเปิดใจคุยกับฉันหน่อย", "ช่วยหาเรื่องคุยหน่อย" })
        {
            int calls = provider.Calls;
            Send(social); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(calls + 1), social);
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Not.Contain("อยากให้ฉันทำอะไร"), social);
            Assert.That(actor.ActiveStation, Is.Null, social); Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), social);
        }
    }

    [UnityTest]
    public IEnumerator IdleVagueRefusalStaysSocialButActiveVagueRefusalStopsWork()
    {
        ActorAt(Box.companionPoint); PlayerAt(Box.playerPoint); Chat();
        provider.Text = "ได้สิ เราคุยเรื่องอื่นกันก็ได้";
        foreach (string text in new[] { "ไม่ต้องแล้ว", "ไม่เอาแล้ว", "ยกเลิก", "หยุดนะ", "พักก่อน" })
        {
            int calls = provider.Calls;
            Send(text); yield return null;
            Assert.That(provider.Calls, Is.EqualTo(calls + 1), text);
            Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(provider.Text), text);
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting));
            Send("มากับฉันหน่อย"); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Following));
            calls = provider.Calls; Send(text); yield return null;
            Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Waiting), text);
            Assert.That(provider.Calls, Is.EqualTo(calls), text);
        }
    }

    [UnityTest]
    public IEnumerator TargetFirstDeskRequestAndInvitationUseSpeechNotSystemControls()
    {
        var desk = actor.Scenario.stations.Single(s => s.id == "desk_support");
        State.SetFlag("inspected_painting"); ActorAt(desk.companionPoint); PlayerAt(desk.playerPoint);
        actor.RefreshInvitation(); Assert.That(actor.Invitation, Is.SameAs(desk));
        Chat(); yield return null;
        Assert.That(actor.Invitation, Is.Null);
        Assert.That(actor.ActiveStation, Is.Null, "Opening an invitation never starts assistance.");
        Assert.That(State.GetConversationLog("Alice").Last().Text, Does.Contain(desk.invitationLine));
        Send("ขอบโต๊ะมันโยกนะ ช่วยได้ไหม"); yield return null;
        Assert.That(actor.ActiveStation, Is.SameAs(desk));
        Assert.That(provider.Calls, Is.Zero);
        Assert.That(NpcReplyPolicy.MentionsGameControls(State.GetConversationLog("Alice").Last().Text), Is.False);
        DialogueManager.Instance.HideDialogue(); Tick(0.2f);
        Assert.That(actor.Mode, Is.EqualTo(CompanionMode.Helping));
        Assert.That(State.HasFlag("found_note"), Is.False);
    }
}
