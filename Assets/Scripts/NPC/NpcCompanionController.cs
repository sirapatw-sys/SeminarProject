using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysteryGame.Companions
{
    public enum CompanionMode { Waiting, Following, GoingToHelp, Helping }

    public class NpcCompanionController : MonoBehaviour
    {
        public CompanionScenarioData Scenario { get; private set; }
        public CompanionMode Mode { get; private set; }
        public CompanionStationData ActiveStation { get; private set; }
        public float Progress { get; private set; }
        public string Bubble { get; private set; }
        public string RoomId { get { return gameObject.scene.name; } }
        public Vector2 Feet { get { return (Vector2)transform.position + feetOffset; } }
        public Vector2 PlayerFeet { get { return playerArea != null ? (Vector2)playerArea.bounds.center : player != null ? (Vector2)player.position : Vector2.positiveInfinity; } }
        private bool CanAct { get { return isActiveAndEnabled && Scenario != null && state != null && player != null &&
            state.GetCurrentScene() == RoomId && !InputGate.IsBlocked && !ItemPopupUI.IsBusy; } }
        public bool CanCommand { get { return CanAct && Vector2.Distance(Feet, PlayerFeet) <= Scenario.commandDistance; } }
        public bool CanStop { get { return CanAct && (CanCommand || Mode != CompanionMode.Waiting); } }
        public bool HasBubble { get { return !string.IsNullOrEmpty(Bubble) && bubbleSeconds > 0f; } }
        private Transform player;
        private Collider2D playerArea;
        private Vector2 feetOffset;
        private GameState state;
        private readonly List<Vector2> path = new List<Vector2>();
        private float repath, elapsedTask, bubbleSeconds;
        private bool resumeFollowing;
        private bool introduced;
        private readonly Queue<string> reactions = new Queue<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= InstallInScene;
            SceneManager.sceneLoaded += InstallInScene;
            InstallInScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        private static void InstallInScene(Scene scene, LoadSceneMode mode)
        {
            var room = KnowledgeLibrary.GetRoom(scene.name);
            if (room == null || room.companionScenario == null) return;
            foreach (var npc in FindObjectsOfType<NPCInteraction>())
                if (npc.gameObject.scene == scene && npc.NpcId == room.companionScenario.npcId)
                {
                    var actor = npc.GetComponent<NpcCompanionController>() ?? npc.gameObject.AddComponent<NpcCompanionController>();
                    actor.Configure(room.companionScenario);
                    if (npc.GetComponent<CompanionHud>() == null) npc.gameObject.AddComponent<CompanionHud>();
                }
        }
        public static NpcCompanionController Find(string npcId, string roomId)
        {
            foreach (var actor in FindObjectsOfType<NpcCompanionController>())
                if (actor.Scenario != null && actor.Scenario.npcId == npcId && actor.RoomId == roomId) return actor;
            return null;
        }
        public void Configure(CompanionScenarioData scenario)
        {
            Unsubscribe(); Scenario = scenario;
            if (scenario == null || scenario.ValidateConfiguration().Count != 0)
            { Scenario = null; ResetActivity(); return; }
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
            playerArea = playerObject != null ? playerObject.GetComponent<Collider2D>() : null;
            var talkArea = GetComponent<Collider2D>();
            feetOffset = talkArea != null ? (Vector2)talkArea.bounds.center - (Vector2)transform.position : Vector2.zero;
            state = GameState.Instance;
            if (state != null) { state.StateReset += ResetActivity; state.StateRestored += ResetActivity; }
            InteractionSignals.Completed += Observe;
            introduced = false;
            ResetActivity();
        }
        public bool IsCompleted(CompanionStationData station)
        { return station != null && Scenario != null && state != null && state.HasFlag(Scenario.CompletedFlag(station)); }
        public bool RequestFollow()
        {
            if (!CanCommand) return false;
            CancelTask(); Mode = CompanionMode.Following; Say(Scenario.followLine);
            string flag = Scenario.Flag("invited");
            if (!state.HasFlag(flag)) { state.SetFlag(flag); state.AddNpcMemory(Scenario.npcId, "ผู้เล่นชวนเดินสำรวจไปด้วยกัน"); }
            return true;
        }
        public bool RequestWait()
        {
            if (!CanStop) return false;
            CancelTask(); Mode = CompanionMode.Waiting; Say(Scenario.waitLine); return true;
        }
        public bool CanHelp(CompanionStationData station)
        {
            return CanCommand && station != null && Scenario.stations.Contains(station) && !IsCompleted(station) &&
                ConditionRule.AllHold(station.conditions, state) &&
                Vector2.Distance(PlayerFeet, station.playerPoint) <= Scenario.commandDistance;
        }
        public bool RequestHelp(CompanionStationData station)
        {
            if (!CanHelp(station)) return false;
            if (ActiveStation == station) return true;
            bool follow = Mode == CompanionMode.Following || (ActiveStation != null && resumeFollowing);
            CancelTask(); ActiveStation = station; Mode = CompanionMode.GoingToHelp;
            resumeFollowing = follow;
            Say(station.requestLine); return true;
        }
        public bool RequestComfort()
        {
            if (!CanCommand) return false;
            string flag = Scenario.Flag("comforted");
            if (state.HasFlag(flag)) { Say(Scenario.comfortRepeatLine); return true; }
            state.SetFlag(flag);
            state.ChangeNpcEmotion(Scenario.npcId, Scenario.comfortEmotion, -10f);
            state.ChangeRelationship(Scenario.npcId, 1);
            state.AddNpcMemory(Scenario.npcId, Scenario.comfortMemory);
            Say(Scenario.comfortLine); return true;
        }
        private void Update()
        {
            if (!CanAct) return;
            if (!introduced && CanCommand) { introduced = true; Say(Scenario.welcomeLine); }
            bubbleSeconds -= Time.deltaTime;
            if (!HasBubble && reactions.Count > 0) Say(reactions.Dequeue());
            Tick(Time.deltaTime);
        }
        // Also used by deterministic tests; the same modal/scene guards apply.
        public void Tick(float seconds)
        {
            if (!CanAct || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (Mode == CompanionMode.Following)
            {
                Vector2 delta = Feet - PlayerFeet;
                if (delta.magnitude > Scenario.followDistance + 0.2f)
                {
                    MoveTowards(PlayerFeet, seconds, Scenario.followDistance);
                }
                else { path.Clear(); repath = 0f; }
            }
            else if (ActiveStation != null)
            {
                if (IsCompleted(ActiveStation) || !ConditionRule.AllHold(ActiveStation.conditions, state))
                { ResetActivity(); return; }
                elapsedTask += seconds;
                if (elapsedTask > Scenario.taskTimeoutSeconds)
                { ResetActivity(); Say(Scenario.blockedLine); return; }
                if (Vector2.Distance(Feet, ActiveStation.companionPoint) > 0.12f)
                {
                    Progress = 0f; Mode = CompanionMode.GoingToHelp;
                    MoveTowards(ActiveStation.companionPoint, seconds); return;
                }
                if (Mode != CompanionMode.Helping) { Mode = CompanionMode.Helping; Say(ActiveStation.holdingLine); }
                bool together = Vector2.Distance(PlayerFeet, ActiveStation.playerPoint) <= ActiveStation.radius;
                Progress = together ? Mathf.Clamp01(Progress + seconds / ActiveStation.holdSeconds) : 0f;
                if (Progress >= 1f) CompleteStation();
            }
        }
        private void MoveTowards(Vector2 goal, float seconds, float stopDistance = 0f)
        {
            repath -= seconds;
            if (repath <= 0f)
            {
                repath = 0.6f;
                bool found = stopDistance > 0f
                    ? CompanionNavigator.TryFollowPath(Feet, goal, stopDistance, Scenario.walkBounds, Scenario.cellSize, IsFree, IsClear, path)
                    : CompanionNavigator.TryPath(Feet, goal, Scenario.walkBounds, Scenario.cellSize, IsFree, IsClear, path);
                if (!found) return;
            }
            while (path.Count > 0 && Vector2.Distance(Feet, path[0]) < 0.04f) path.RemoveAt(0);
            if (path.Count == 0) return;
            float step = Scenario.moveSpeed * Mathf.Min(seconds, 0.1f);
            // The player can move before the next repath. Never consume the
            // live stopping distance while following an older route.
            if (stopDistance > 0f) step = Mathf.Min(step, Mathf.Max(0f, Vector2.Distance(Feet, PlayerFeet) - stopDistance));
            Vector2 next = Vector2.MoveTowards(Feet, path[0], step);
            if (!IsClear(Feet, next)) { path.Clear(); repath = 0f; return; }
            transform.position += (Vector3)(next - Feet);
        }
        private bool Blocks(Collider2D collider)
        { return collider != null && !collider.isTrigger && collider.transform != transform &&
            !collider.transform.IsChildOf(transform) && (player == null || (collider.transform != player && !collider.transform.IsChildOf(player))); }
        public bool IsFree(Vector2 point)
        {
            if (!Scenario.walkBounds.Contains(point)) return false;
            foreach (var collider in Physics2D.OverlapCircleAll(point, Scenario.clearance)) if (Blocks(collider)) return false;
            return true;
        }
        private bool IsClear(Vector2 from, Vector2 to)
        {
            if (!IsFree(to)) return false;
            Vector2 delta = to - from;
            foreach (var hit in Physics2D.CircleCastAll(from, Scenario.clearance, delta.normalized, delta.magnitude))
                if (Blocks(hit.collider)) return false;
            return true;
        }
        private void CompleteStation()
        {
            var station = ActiveStation;
            if (station == null || IsCompleted(station)) return;
            state.SetFlag(Scenario.CompletedFlag(station));
            state.ChangeRelationship(Scenario.npcId, Mathf.Clamp(station.relationshipReward, 0, 6));
            state.AddNpcMemory(Scenario.npcId, station.memory);
            state.AddJournalEntry(Scenario.CompletedFlag(station), station.title, station.journalText);
            state.AddHistory("Completed companion activity " + Scenario.scenarioId + "/" + station.id);
            Say(station.completedLine);
            bool follow = resumeFollowing; CancelTask(); Mode = follow ? CompanionMode.Following : CompanionMode.Waiting;
            JournalUI.NotifyNewEntry(); SfxPlayer.Play(SfxPlayer.Cue.Interact);
        }
        private void Observe(InteractionData data, bool success)
        {
            if (!isActiveAndEnabled || data == null || Scenario == null || state == null || player == null ||
                state.GetCurrentScene() != RoomId || Vector2.Distance(Feet, PlayerFeet) > Scenario.witnessDistance) return;
            if (Scenario.observations == null) return;
            foreach (var observation in Scenario.observations)
            {
                if (observation == null || observation.interactionId != data.interactionId || observation.success != success ||
                    !ConditionRule.AllHold(observation.conditions, state)) continue;
                string flag = Scenario.Flag("observed." + observation.id);
                if (state.HasFlag(flag)) continue;
                state.SetFlag(flag);
                if (!string.IsNullOrWhiteSpace(observation.memory)) state.AddNpcMemory(Scenario.npcId, observation.memory);
                if (reactions.Count < 3) reactions.Enqueue(observation.line);
            }
        }
        public string PromptState()
        {
            return "สถานะเพื่อนร่วมสำรวจที่เกมยืนยัน: " + Scenario.displayName + " " +
                (Mode == CompanionMode.Following ? "กำลังเดินตามผู้เล่น" :
                 Mode == CompanionMode.GoingToHelp ? "กำลังเดินไปช่วยงานที่ผู้เล่นขอ ยังไม่สำเร็จ" :
                 Mode == CompanionMode.Helping ? "กำลังจับกลไกให้ รอผู้เล่นทำอีกด้าน ยังไม่สำเร็จ" : "กำลังรออยู่") +
                "; การพูดในแชทไม่ใช่การสั่งทำงานหรือยืนยันว่างานสำเร็จ ห้ามแต่งผลการกระทำหรือเพิ่มข้อมูลคำใบ้";
        }
        private void Say(string text) { Bubble = text; bubbleSeconds = 5f; }
        private void CancelTask() { ActiveStation = null; Progress = 0f; elapsedTask = 0f; resumeFollowing = false; path.Clear(); repath = 0f; }
        private void ResetActivity() { CancelTask(); Mode = CompanionMode.Waiting; reactions.Clear(); Bubble = null; bubbleSeconds = 0f; }
        private void Unsubscribe()
        {
            if (state != null) { state.StateReset -= ResetActivity; state.StateRestored -= ResetActivity; }
            InteractionSignals.Completed -= Observe;
        }
        private void OnDisable() { ResetActivity(); Unsubscribe(); }
        private void OnEnable() { if (Scenario != null) Configure(Scenario); }
        private void OnDestroy() { Unsubscribe(); }
    }
}
