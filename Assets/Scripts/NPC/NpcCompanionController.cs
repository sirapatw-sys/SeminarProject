using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysteryGame.Companions
{
    public enum CompanionMode { Waiting, Following, GoingToHelp, Helping, Searching }
    public enum CompanionTaskPhase { None, Moving, Working, Completed, Cancelled, Failed }

    public class NpcCompanionController : MonoBehaviour
    {
        public CompanionScenarioData Scenario { get; private set; }
        public CompanionMode Mode { get; private set; }
        public CompanionStationData ActiveStation { get; private set; }
        public float Progress { get; private set; }
        public float MovementSpeed { get; private set; }
        public string Bubble { get; private set; }
        public CompanionTaskPhase TaskPhase { get; private set; }
        public int TaskRevision { get; private set; }
        public string LastTaskId { get; private set; }
        public CompanionStationData Invitation { get; private set; }
        public CompanionSearchSpot ActiveSearchSpot { get; private set; }
        public float WorkAnimationTime { get; private set; }
        public bool IsInspecting { get { return Mode == CompanionMode.Searching && TaskPhase == CompanionTaskPhase.Working; } }
        public string RoomId { get { return gameObject.scene.name; } }
        public Vector2 Feet { get { return (Vector2)transform.position + feetOffset; } }
        public Vector2 PlayerFeet { get { return playerArea != null ? (Vector2)playerArea.bounds.center : player != null ? (Vector2)player.position : Vector2.positiveInfinity; } }
        private bool CanAct { get { return isActiveAndEnabled && Scenario != null && state != null && player != null &&
            state.GetCurrentScene() == RoomId && !InputGate.IsBlocked && !ItemPopupUI.IsBusy; } }
        public bool CanCommand { get { return CanAct && Vector2.Distance(Feet, PlayerFeet) <= Scenario.commandDistance; } }
        public bool CanStop { get { return CanAct && (CanCommand || Mode != CompanionMode.Waiting); } }
        public bool HasBubble { get { return !string.IsNullOrEmpty(Bubble) && bubbleSeconds > 0f; } }
        public bool HasDiscoveryInvitation { get { return isActiveAndEnabled && state != null && state == GameState.Instance &&
            Scenario != null && Mode != CompanionMode.Searching && ActiveStation == null &&
            CompanionDiscovery.Pending(Scenario, state, RoomId).Count > 0; } }
        private Transform player;
        private Collider2D playerArea;
        private Vector2 feetOffset;
        private GameState state;
        private readonly List<Vector2> path = new List<Vector2>();
        private readonly Collider2D[] overlapBuffer = new Collider2D[64];
        private float repath, elapsedTask, bubbleSeconds;
        private int searchIndex;
        private float searchProgress;
        private bool foundSearchEvidence;
        private string searchReport;
        private bool resumeFollowing;
        private bool followingMoving;
        private Vector2 moveDirection;
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
                    NpcCompanionController actor;
                    if (!npc.TryGetComponent(out actor)) actor = npc.gameObject.AddComponent<NpcCompanionController>();
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
            // Authored NPCs have only static colliders. A static trigger moved by
            // its transform does not wake a sleeping player body, so walking up
            // to (or away from) a player who stands still never raised the
            // talk prompt's Enter/Exit. A never-sleeping kinematic body keeps
            // those contacts live without changing how the companion moves.
            Rigidbody2D body;
            if (!TryGetComponent(out body))
            {
                body = gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                body.interpolation = RigidbodyInterpolation2D.None;
            }
            state = GameState.Instance;
            if (state != null) { state.StateReset += ResetActivity; state.StateRestored += ResetActivity; }
            InteractionSignals.Completed += Observe;
            introduced = false;
            ResetActivity();
            CompanionStationInteraction.Install(this);
            if (GetComponent<CompanionWorkVisuals>() == null) gameObject.AddComponent<CompanionWorkVisuals>();
        }
        public bool IsCompleted(CompanionStationData station)
        { return station != null && Scenario != null && state != null && state.HasFlag(Scenario.CompletedFlag(station)); }
        public bool RequestFollow()
        {
            if (!CanCommand) return false;
            return FollowCore();
        }
        private bool FollowCore()
        {
            if (Mode == CompanionMode.Following) { Say(Scenario.followLine); return true; }
            CancelTask(); Mode = CompanionMode.Following; Say(Scenario.followLine);
            string flag = Scenario.Flag("invited");
            if (!state.HasFlag(flag)) { state.SetFlag(flag); state.AddNpcMemory(Scenario.npcId, "ผู้เล่นชวนเดินสำรวจไปด้วยกัน"); }
            return true;
        }
        public bool RequestWait()
        {
            if (!CanStop) return false;
            return WaitCore();
        }
        private bool WaitCore()
        {
            CancelTask(); Mode = CompanionMode.Waiting; Say(Scenario.waitLine); return true;
        }
        private bool SearchCore()
        {
            if (Mode == CompanionMode.Searching) return true;
            if (Scenario.searchSpots == null || Scenario.searchSpots.Count == 0) return false;
            bool available = false;
            foreach (var spot in Scenario.searchSpots) if (ConditionRule.AllHold(spot.conditions, state)) { available = true; break; }
            if (!available) return false;
            bool follow = Mode == CompanionMode.Following;
            CancelTask(); Mode = CompanionMode.Searching; resumeFollowing = follow;
            TaskRevision++; TaskPhase = CompanionTaskPhase.Moving; LastTaskId = "search";
            searchIndex = -1; foundSearchEvidence = false; searchReport = null; AdvanceSearch();
            Say(Scenario.searchRequestLine); return ActiveSearchSpot != null;
        }
        private void AdvanceSearch()
        {
            StopMoving(); searchProgress = 0f; WorkAnimationTime = 0f;
            ActiveSearchSpot = null;
            while (++searchIndex < Scenario.searchSpots.Count)
                if (ConditionRule.AllHold(Scenario.searchSpots[searchIndex].conditions, state))
                { ActiveSearchSpot = Scenario.searchSpots[searchIndex]; TaskPhase = CompanionTaskPhase.Moving; return; }
            bool follow = resumeFollowing;
            string lastLine = foundSearchEvidence ? searchReport : "ฉันตรวจบริเวณที่ไปถึงแล้ว รอบนี้ยังไม่พบหลักฐานใหม่ที่ยืนยันได้";
            CancelTask(); Mode = follow ? CompanionMode.Following : CompanionMode.Waiting;
            TaskPhase = CompanionTaskPhase.Completed;
            Say(lastLine ?? Scenario.searchFinishedLine);
        }
        private void TickSearch(float seconds)
        {
            if (ActiveSearchSpot == null) { AdvanceSearch(); return; }
            elapsedTask += seconds;
            if (elapsedTask > Scenario.taskTimeoutSeconds)
            { ResetActivity(); LastTaskId = "search"; TaskPhase = CompanionTaskPhase.Failed; Say(Scenario.blockedLine); return; }
            if (!ConditionRule.AllHold(ActiveSearchSpot.conditions, state)) { AdvanceSearch(); return; }
            if (Vector2.Distance(Feet, ActiveSearchSpot.point) > 0.12f)
            { TaskPhase = CompanionTaskPhase.Moving; searchProgress = 0f; MoveTowards(ActiveSearchSpot.point, seconds); return; }
            StopMoving();
            if (TaskPhase != CompanionTaskPhase.Working) Say(ActiveSearchSpot.inspectingLine);
            TaskPhase = CompanionTaskPhase.Working; WorkAnimationTime += seconds; searchProgress += seconds;
            if (searchProgress < ActiveSearchSpot.inspectSeconds) return;
            var room = KnowledgeLibrary.GetRoom(RoomId);
            var fact = room != null ? room.FindFact(ActiveSearchSpot.factId) : null;
            var profile = KnowledgeLibrary.GetNpc(Scenario.npcId);
            if (fact != null && !fact.isPuzzleAnswer && !fact.isPuzzleGuidance &&
                (profile == null || !profile.IsForbidden(fact.factId)))
            {
                string flag = Scenario.SearchFlag(fact.factId);
                bool previous = state.HasFlag(flag);
                state.SetFlag(flag);
                if (fact.IsRevealed(state))
                {
                    state.SetFlag(NpcObservation.Flag(Scenario.npcId, RoomId, fact.factId));
                    foundSearchEvidence = true;
                    searchReport = (previous ? "ตรวจยืนยันได้ว่า... " : "เจอแล้ว... ") + fact.statement;
                    Say(searchReport);
                    if (state.AddJournalEntry(flag, "หลักฐานที่ " + Scenario.displayName + " พบ: " + fact.EvidenceTitle, fact.statement))
                    {
                        state.AddNpcMemory(Scenario.npcId, Scenario.displayName + " สำรวจและพบหลักฐานด้วยตัวเอง: " + fact.statement);
                        JournalUI.NotifyNewEntry();
                    }
                }
                else if (!previous) state.RemoveFlag(flag);
            }
            AdvanceSearch();
        }
        public bool CanHelp(CompanionStationData station)
        {
            return CanCommand && StationAvailable(station, false);
        }
        // A request is made face to face (CanCommand), and the companion walks to
        // the station herself, so starting one no longer needs the player to be
        // standing near it already; that refused "ช่วยจับโต๊ะหน่อย" asked from
        // across the room. The unprompted "?" invitation still needs proximity.
        private bool StationAvailable(CompanionStationData station, bool playerNearby = true)
        {
            return station != null && Scenario.stations.Contains(station) && !IsCompleted(station) &&
                ConditionRule.AllHold(station.conditions, state) &&
                (!playerNearby || Vector2.Distance(PlayerFeet, station.playerPoint) <= Scenario.commandDistance);
        }
        public bool RequestHelp(CompanionStationData station)
        {
            if (!CanHelp(station)) return false;
            return HelpCore(station);
        }
        private string RequestLineFor(CompanionStationData station)
        {
            return state.GetRelationship(Scenario.npcId) < 45 && !string.IsNullOrWhiteSpace(station.cautiousRequestLine)
                ? station.cautiousRequestLine : station.requestLine;
        }
        private bool HelpCore(CompanionStationData station)
        {
            if (ActiveStation == station) return true;
            bool follow = Mode == CompanionMode.Following || (ActiveStation != null && resumeFollowing);
            CancelTask(); ActiveStation = station; Mode = CompanionMode.GoingToHelp;
            TaskRevision++; LastTaskId = station.id; TaskPhase = CompanionTaskPhase.Moving;
            Invitation = null;
            resumeFollowing = follow;
            Say(RequestLineFor(station)); return true;
        }
        public bool RequestComfort()
        {
            if (!CanCommand) return false;
            return ComfortCore();
        }
        private bool ComfortCore()
        {
            string flag = Scenario.Flag("comforted");
            if (state.HasFlag(flag)) { Say(Scenario.comfortRepeatLine); return true; }
            state.SetFlag(flag);
            state.ChangeNpcEmotion(Scenario.npcId, Scenario.comfortEmotion, -10f);
            state.ChangeRelationship(Scenario.npcId, 1);
            state.AddNpcMemory(Scenario.npcId, Scenario.comfortMemory);
            Say(Scenario.comfortLine); return true;
        }
        internal bool CanConverse(DialogueManager dialogue)
        {
            return dialogue != null && dialogue == DialogueManager.Instance && DialogueManager.IsDialogueOpen &&
                isActiveAndEnabled && Scenario != null && state != null && state == GameState.Instance && player != null &&
                dialogue.ActiveNpcId == Scenario.npcId && state.GetCurrentScene() == RoomId &&
                InputGate.IsGameplayActive && !AiSettingsPanel.IsOpen && !KeypadLockUI.IsOpen && !JournalUI.IsOpen && !ItemPopupUI.IsBusy &&
                Vector2.Distance(Feet, PlayerFeet) <= Scenario.commandDistance;
        }
        internal bool ApplyDialogueCommand(DialogueManager dialogue, CompanionCommand command, CompanionStationData station, out string reply)
        {
            // Bypass only the conversation's own input lock. Movement/task Tick
            // remains paused until the player closes dialogue; every other modal
            // and the live NPC/room/range checks still apply.
            bool allowed = CanConverse(dialogue);
            if (!allowed) { reply = "ตอนนี้ยังช่วยตามคำชวนไม่ได้ เข้ามาคุยใกล้ ๆ แล้วลองชวนอีกครั้งนะ"; return false; }
            if (command == CompanionCommand.Clarify)
            { reply = "อยากให้ฉันทำอะไรก่อน ชวนทีละอย่างและบอกสิ่งที่อยากให้ช่วยอีกครั้งนะ"; return false; }
            switch (command)
            {
                case CompanionCommand.Follow: FollowCore(); reply = Scenario.followLine; break;
                case CompanionCommand.Wait: WaitCore(); reply = Scenario.waitLine; break;
                case CompanionCommand.Search:
                    bool started = SearchCore();
                    reply = started ? Scenario.searchRequestLine : "ตอนนี้ยังไม่มีบริเวณที่ฉันไปสำรวจได้ ลองคุยทบทวนหลักฐานที่เราพบกันก่อนได้";
                    return started;
                case CompanionCommand.Comfort:
                    ComfortCore(); reply = Bubble; return true;
                case CompanionCommand.Help:
                    if (station == null || !Scenario.stations.Contains(station))
                    { reply = "บอกสิ่งที่อยากให้ฉันช่วยให้ชัดอีกครั้งนะ"; return false; }
                    if (IsCompleted(station)) { reply = !string.IsNullOrWhiteSpace(station.completedStatusLine) ? station.completedStatusLine :
                        "เราเคยทำตรงนั้นสำเร็จด้วยกันแล้วนะ"; return false; }
                    if (!StationAvailable(station, false))
                    { reply = "ตอนนี้ยังไม่มีอะไรตรงนั้นให้ฉันช่วยนะ ถ้ามีอะไรให้ช่วยค่อยบอกฉันอีกที"; return false; }
                    HelpCore(station);
                    reply = (Mode == CompanionMode.Helping ? station.holdingLine : RequestLineFor(station)) +
                        (string.IsNullOrWhiteSpace(station.instructions) ? "" : "\n" + station.instructions);
                    break;
                default: reply = "บอกสิ่งที่อยากให้ฉันช่วยอีกครั้งนะ"; return false;
            }
            return true;
        }
        private void Update()
        {
            if (!CanAct) { MovementSpeed = 0f; return; }
            if (!introduced && CanCommand) { introduced = true; Say(Scenario.welcomeLine); }
            bubbleSeconds -= Time.deltaTime;
            if (!HasBubble && reactions.Count > 0) Say(reactions.Dequeue());
            Tick(Time.deltaTime);
            RefreshInvitation();
        }
        public void RefreshInvitation()
        {
            Invitation = null;
            if (!CanAct || ActiveStation != null || Scenario.stations == null) return;
            foreach (var station in Scenario.stations)
                if (!string.IsNullOrWhiteSpace(station.invitationLine) && StationAvailable(station) &&
                    !state.HasFlag(Scenario.Flag(station.id + ".invitation_read")))
                { Invitation = station; break; }
        }
        public string ReadInvitation()
        {
            if (Invitation == null || !StationAvailable(Invitation)) return null;
            var station = Invitation; Invitation = null;
            state.SetFlag(Scenario.Flag(station.id + ".invitation_read"));
            return state.GetRelationship(Scenario.npcId) >= 70 && !string.IsNullOrWhiteSpace(station.trustedInvitationLine)
                ? station.trustedInvitationLine : station.invitationLine;
        }
        public string ReadDiscoveryReport()
        {
            if (!CanCommand || !HasDiscoveryInvitation) return null;
            var facts = CompanionDiscovery.Pending(Scenario, state, RoomId);
            var shown = facts.GetRange(0, Mathf.Min(CompanionDiscovery.ReportLimit, facts.Count));
            string line = CompanionDiscovery.ReportText(Scenario, shown);
            CompanionDiscovery.Acknowledge(Scenario, state, RoomId, shown.ConvertAll(f => f.factId));
            return line;
        }
        public bool IsHolding(string interactionId)
        {
            return isActiveAndEnabled && Scenario != null && state == GameState.Instance &&
                state != null && state.GetCurrentScene() == RoomId && Mode == CompanionMode.Helping &&
                ActiveStation != null && ActiveStation.holdForInteractionId == interactionId &&
                !IsCompleted(ActiveStation) && ConditionRule.AllHold(ActiveStation.conditions, state) &&
                Vector2.Distance(PlayerFeet, ActiveStation.playerPoint) <= ActiveStation.radius &&
                Vector2.Distance(Feet, ActiveStation.companionPoint) <= 0.12f;
        }
        // Also used by deterministic tests; the same modal/scene guards apply.
        public void Tick(float seconds)
        {
            if (!CanAct) { MovementSpeed = 0f; return; }
            if (seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (Mode == CompanionMode.Searching) { TickSearch(seconds); return; }
            if (Mode == CompanionMode.Following)
            {
                float distance = Vector2.Distance(Feet, PlayerFeet);
                // Different start/stop thresholds keep a nearby idle companion
                // from taking a tiny step every time the player shifts position.
                if (!followingMoving && distance > Scenario.followDistance + Scenario.followStartBuffer)
                    followingMoving = true;
                if (followingMoving && distance <= Scenario.followDistance + 0.06f)
                    followingMoving = false;
                if (followingMoving)
                {
                    MoveTowards(PlayerFeet, seconds, Scenario.followDistance);
                }
                else StopMoving();
            }
            else if (ActiveStation != null)
            {
                if (IsCompleted(ActiveStation) || !ConditionRule.AllHold(ActiveStation.conditions, state))
                { ResetActivity(); return; }
                elapsedTask += seconds;
                if (elapsedTask > Scenario.taskTimeoutSeconds)
                {
                    // Standing at the station already: "I can't get there" would be untrue.
                    string line = Mode == CompanionMode.Helping && !string.IsNullOrWhiteSpace(Scenario.releaseLine) ? Scenario.releaseLine : Scenario.blockedLine;
                    string failedTask = ActiveStation.id; ResetActivity(); LastTaskId = failedTask; TaskPhase = CompanionTaskPhase.Failed; Say(line); return;
                }
                if (Vector2.Distance(Feet, ActiveStation.companionPoint) > 0.12f)
                {
                    Progress = 0f; Mode = CompanionMode.GoingToHelp; TaskPhase = CompanionTaskPhase.Moving;
                    MoveTowards(ActiveStation.companionPoint, seconds); return;
                }
                StopMoving();
                if (Mode != CompanionMode.Helping) { Mode = CompanionMode.Helping; Say(ActiveStation.holdingLine); }
                TaskPhase = CompanionTaskPhase.Working;
                WorkAnimationTime += seconds;
                if (!string.IsNullOrWhiteSpace(ActiveStation.holdForInteractionId)) return;
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
                if (!found) { MovementSpeed = 0f; return; }
            }
            while (path.Count > 0 && Vector2.Distance(Feet, path[0]) < 0.04f) path.RemoveAt(0);
            if (path.Count == 0) { MovementSpeed = 0f; return; }
            float dt = Mathf.Min(seconds, 0.1f);
            // Keep the original corner route until the actual step is safe;
            // furniture can change after the route was planned.
            for (int target = Mathf.Min(path.Count - 1, 6); target >= 0; target--)
            {
                if (target > 0 && !IsClear(Feet, path[target])) continue;
                float remaining = Vector2.Distance(Feet, path[target]);
                for (int i = target + 1; i < path.Count; i++) remaining += Vector2.Distance(path[i - 1], path[i]);
                float desiredSpeed = Mathf.Min(Scenario.moveSpeed, Mathf.Sqrt(2f * Scenario.moveDeceleration * remaining));
                Vector2 direction = (path[target] - Feet).normalized;
                if (moveDirection.sqrMagnitude > 0f)
                    desiredSpeed *= Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(Vector2.Dot(moveDirection, direction)));
                float speed = Mathf.MoveTowards(MovementSpeed, desiredSpeed,
                    (desiredSpeed > MovementSpeed ? Scenario.moveAcceleration : Scenario.moveDeceleration) * dt);
                float step = speed * dt;
                // Respect the player's live position, even on an older route.
                if (stopDistance > 0f) step = Mathf.Min(step, Mathf.Max(0f, Vector2.Distance(Feet, PlayerFeet) - stopDistance));
                Vector2 next = Vector2.MoveTowards(Feet, path[target], step);
                if (!IsClear(Feet, next)) continue;
                if (target > 0) path.RemoveRange(0, target);
                Vector2 movement = next - Feet;
                MovementSpeed = movement.magnitude / dt;
                if (movement.sqrMagnitude > 0f) moveDirection = movement.normalized;
                transform.position += (Vector3)movement;
                return;
            }
            path.Clear(); MovementSpeed = 0f; repath = Mathf.Max(repath, 0.6f);
        }
        private void StopMoving() { path.Clear(); repath = 0f; MovementSpeed = 0f; moveDirection = Vector2.zero; }
        private bool Blocks(Collider2D collider)
        { return collider != null && !collider.isTrigger && collider.transform != transform &&
            !collider.transform.IsChildOf(transform) && (player == null || (collider.transform != player && !collider.transform.IsChildOf(player))); }
        public bool IsFree(Vector2 point)
        {
            if (!Scenario.walkBounds.Contains(point)) return false;
            int count = Physics2D.OverlapCircleNonAlloc(point, Scenario.clearance, overlapBuffer);
            // A full buffer may omit a blocker. Fall back rather than accepting
            // a truncated query. Ordinary room queries reuse the fixed buffer.
            var hits = count == overlapBuffer.Length ? Physics2D.OverlapCircleAll(point, Scenario.clearance) : overlapBuffer;
            if (hits != overlapBuffer) count = hits.Length;
            for (int i = 0; i < count; i++) if (Blocks(hits[i])) return false;
            return true;
        }
        private bool IsClear(Vector2 from, Vector2 to)
        {
            if (!Scenario.walkBounds.Contains(from) || !IsFree(to)) return false;
            Vector2 delta = to - from;
            if (delta.sqrMagnitude == 0f) return true;
            // A circle swept along a segment fills this capsule. Checking the
            // whole footprint avoids CircleCast missing a shallow furniture
            // corner that OverlapCircle then rejects on the next small step.
            Vector2 size = new Vector2(delta.magnitude + 2f * Scenario.clearance, 2f * Scenario.clearance);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            int count = Physics2D.OverlapCapsuleNonAlloc((from + to) * 0.5f, size, CapsuleDirection2D.Horizontal, angle, overlapBuffer);
            var hits = count == overlapBuffer.Length ? Physics2D.OverlapCapsuleAll((from + to) * 0.5f, size,
                CapsuleDirection2D.Horizontal, angle) : overlapBuffer;
            if (hits != overlapBuffer) count = hits.Length;
            for (int i = 0; i < count; i++) if (Blocks(hits[i])) return false;
            return true;
        }
        private void CompleteStation()
        {
            var station = ActiveStation;
            if (station == null || IsCompleted(station)) return;
            state.SetFlag(Scenario.CompletedFlag(station));
            if (station.completionActions != null)
                foreach (var action in station.completionActions) action?.Execute(state);
            var room = KnowledgeLibrary.GetRoom(RoomId);
            var profile = KnowledgeLibrary.GetNpc(Scenario.npcId);
            if (station.discoveredFactIds != null) foreach (string id in station.discoveredFactIds)
            {
                var fact = room != null ? room.FindFact(id) : null;
                if (fact != null && fact.IsRevealed(state) && !fact.isPuzzleAnswer && !fact.isPuzzleGuidance &&
                    (profile == null || !profile.IsForbidden(id)))
                    state.SetFlag(NpcObservation.Flag(Scenario.npcId, RoomId, id));
            }
            state.ChangeRelationship(Scenario.npcId, Mathf.Clamp(station.relationshipReward, 0, 6));
            state.AddNpcMemory(Scenario.npcId, station.memory);
            state.AddJournalEntry(Scenario.CompletedFlag(station), station.title, station.journalText);
            state.AddHistory("Completed companion activity " + Scenario.scenarioId + "/" + station.id);
            Say(station.completedLine);
            bool follow = resumeFollowing; CancelTask(); Mode = follow ? CompanionMode.Following : CompanionMode.Waiting;
            TaskPhase = CompanionTaskPhase.Completed;
            JournalUI.NotifyNewEntry(); SfxPlayer.Play(SfxPlayer.Cue.Interact);
        }
        private void Observe(InteractionData data, bool success)
        {
            if (!isActiveAndEnabled || data == null || Scenario == null || state == null || player == null ||
                state.GetCurrentScene() != RoomId || Vector2.Distance(Feet, PlayerFeet) > Scenario.witnessDistance) return;
            // Completion is tied to the player's verified interaction, never
            // an AI claim or merely waiting beside the station.
            if (success && ActiveStation != null && Mode == CompanionMode.Helping &&
                ActiveStation.holdForInteractionId == data.interactionId &&
                Vector2.Distance(Feet, ActiveStation.companionPoint) <= 0.12f &&
                Vector2.Distance(PlayerFeet, ActiveStation.playerPoint) <= ActiveStation.radius)
                CompleteStation();
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
                 Mode == CompanionMode.Searching ? "กำลังสำรวจพื้นที่ตามที่ผู้เล่นขอ ยังไม่ยืนยันว่าเจอหลักฐาน" :
                 Mode == CompanionMode.GoingToHelp ? "กำลังเดินไปช่วยงานที่ผู้เล่นขอ ยังไม่สำเร็จ" :
                 Mode == CompanionMode.Helping ? "กำลังจับกลไกให้ รอผู้เล่นทำอีกด้าน ยังไม่สำเร็จ" : "กำลังรออยู่") +
                "; เกมตรวจคำชวนที่ผู้เล่นพิมพ์ก่อนเริ่มงาน ข้อความ AI ไม่ใช่คำสั่งหรือหลักฐานว่างานสำเร็จ ห้ามแต่งผลการกระทำหรือเพิ่มข้อมูลคำใบ้";
        }
        private void Say(string text) { Bubble = text; bubbleSeconds = 5f; }
        private void CancelTask() { if (ActiveStation != null || Mode == CompanionMode.Searching) { TaskRevision++; TaskPhase = CompanionTaskPhase.Cancelled; } ActiveStation = null; ActiveSearchSpot = null; WorkAnimationTime = 0f; Progress = 0f; elapsedTask = 0f; resumeFollowing = false; followingMoving = false; StopMoving(); }
        private void ResetActivity() { CancelTask(); TaskRevision++; TaskPhase = CompanionTaskPhase.None; LastTaskId = null; Invitation = null; Mode = CompanionMode.Waiting; reactions.Clear(); Bubble = null; bubbleSeconds = 0f; }
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
