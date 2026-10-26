using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;

namespace MysteryGame.Companions
{
    public sealed class CompanionTaskResult
    {
        public string TargetId;
        public CompanionWorldVerb Verb;
        public bool Success;
        public string Message;
        public Vector2 Destination;
        public readonly List<string> FactIds = new List<string>();
        /// <summary>Journal size when the task finished; a later discovery makes this result stale.</summary>
        public int JournalCount = -1;
    }

    /// <summary>One cancellable world task. The controller remains the only movement owner.</summary>
    public sealed class CompanionTaskExecutor
    {
        readonly NpcCompanionController actor;
        readonly CompanionWorldTarget target;
        readonly CompanionWorldTarget reference;
        readonly Vector2 originalPoint, referencePoint;
        readonly Bounds originalBounds, referenceBounds;
        readonly string place;
        readonly GameState state;
        readonly int sceneRevision;
        readonly Vector2 goal;
        readonly string interactionId;
        readonly CompanionWorldVerb verb;
        float elapsed, worked;
        public Vector2 LookPoint => target != null && target.Interaction != null ? target.Interaction.FocusPoint : goal + Vector2.right * 0.4f;
        public CompanionTaskExecutor(NpcCompanionController actor, CompanionWorldTarget target,
            CompanionWorldVerb verb, Vector2 goal, CompanionWorldTarget reference = null, string place = null)
        {
            this.actor = actor; this.target = target; this.verb = verb; this.goal = goal;
            this.reference = reference; this.place = place ?? target.Definition.title;
            originalPoint = target.ApproachPoint; originalBounds = target.SpatialBounds;
            referencePoint = reference != null ? reference.ApproachPoint : Vector2.zero;
            referenceBounds = reference != null ? reference.SpatialBounds : default(Bounds);
            state = GameState.Instance; sceneRevision = state.SceneRevision;
            interactionId = target.Interaction?.ActiveData?.interactionId;
        }

        public void Tick(float seconds)
        {
            if (state != GameState.Instance || sceneRevision != state.SceneRevision || target == null ||
                !target.IsVisible(state) || !target.Allows(verb) || RoomWorldRegistry.Find(actor.RoomId, state, target.Definition.id) != target ||
                Vector2.Distance(originalPoint, target.ApproachPoint) > 0.05f || originalBounds != target.SpatialBounds ||
                !ReferenceEquals(reference, null) && (reference == null || !reference.IsVisible(state) ||
                    RoomWorldRegistry.Find(actor.RoomId, state, reference.Definition.id) != reference ||
                    Vector2.Distance(referencePoint, reference.ApproachPoint) > 0.05f || referenceBounds != reference.SpatialBounds))
            { Finish(false, "ตรงนั้นเปลี่ยนไปแล้ว ลองบอกสิ่งที่อยากให้ตรวจอีกครั้งนะ"); return; }
            elapsed += seconds;
            if (elapsed > actor.Scenario.taskTimeoutSeconds) { Finish(false, "ฉันเข้าไปตรงนั้นไม่ได้ มีทางเดินติดอยู่"); return; }
            if (Vector2.Distance(actor.Feet, goal) > 0.12f)
            { actor.WorldPhase(CompanionTaskPhase.Moving); worked = 0f; actor.MoveToWorld(goal, seconds); return; }
            actor.StopWorldMovement();
            if (verb == CompanionWorldVerb.Move) { Finish(true, "ถึง " + place + " แล้ว ฉันจะรอตรงนี้นะ"); return; }
            actor.WorldPhase(CompanionTaskPhase.Working, seconds);
            worked += seconds;
            var data = target.Interaction?.ActiveData;
            float duration = Mathf.Max(target.Definition.workSeconds, data != null ? data.soloPreparationSeconds : 0f);
            if (worked < duration) return;
            if (data != null && data.interactionId != interactionId)
            { Finish(false, "สิ่งตรงนี้เปลี่ยนไปขณะที่ฉันกำลังตรวจ ลองชวนอีกครั้งนะ"); return; }
            var result = new CompanionTaskResult { TargetId = target.Definition.id, Verb = verb, Destination = goal };
            var operation = verb == CompanionWorldVerb.Open ? CompanionInteractionOperation.Open : CompanionInteractionOperation.Inspect;
            if (data != null && data.allowCompanionExecution && data.companionOperation == operation && InteractionSystem.Instance != null)
            {
                var execution = InteractionSystem.Instance.TryExecuteCompanion(data, actor.Scenario.npcId, operation);
                result.Success = execution.Success; result.Message = execution.Message;
            }
            else
            {
                result.Success = verb == CompanionWorldVerb.Inspect;
                result.Message = result.Success ? target.Definition.inspectLine : "ตรงนี้ต้องให้เธอเป็นคนเปิดเอง ฉันรอข้าง ๆ ได้";
                if (result.Success && data != null && !data.repeatable &&
                    state.HasFlag("interaction." + data.interactionId + ".completed") && !string.IsNullOrWhiteSpace(data.alreadyCompletedMessage))
                    result.Message = data.alreadyCompletedMessage;
            }
            if (string.IsNullOrWhiteSpace(result.Message)) result.Message = "ตรวจตรงนี้แล้ว ยังไม่มีสิ่งใหม่ที่ยืนยันได้";
            if (result.Success)
            {
                var room = KnowledgeLibrary.GetRoom(actor.RoomId);
                var profile = KnowledgeLibrary.GetNpc(actor.Scenario.npcId);
                foreach (string id in target.Definition.discoveredFactIds ?? new List<string>())
                {
                    var fact = room?.FindFact(id);
                    if (fact == null || !fact.IsRevealed(state) || fact.isPuzzleAnswer || fact.isPuzzleGuidance ||
                        !fact.canShareAsEvidence || (profile != null && profile.IsForbidden(id))) continue;
                    state.SetFlag(NpcObservation.Flag(actor.Scenario.npcId, actor.RoomId, id));
                    result.FactIds.Add(id);
                    if (state.AddJournalEntry("companion.world." + actor.RoomId + "." + id,
                        actor.Scenario.displayName + " ตรวจพบ: " + fact.EvidenceTitle, fact.statement))
                    {
                        state.AddNpcMemory(actor.Scenario.npcId, "ตรวจ " + target.Definition.title + " แล้วพบ: " + fact.statement);
                        JournalUI.NotifyNewEntry();
                    }
                }
            }
            actor.FinishWorldTask(result);
        }
        void Finish(bool success, string message) => actor.FinishWorldTask(new CompanionTaskResult {
            TargetId = target != null ? target.Definition?.id : null, Verb = verb, Success = success, Message = message, Destination = goal });
    }
}
