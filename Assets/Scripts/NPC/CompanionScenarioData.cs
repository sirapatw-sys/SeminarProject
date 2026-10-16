using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Companions
{
    [Serializable]
    public class CompanionObservation
    {
        public string id;
        public string interactionId;
        public bool success = true;
        [TextArea] public string line;
        [TextArea] public string memory;
        public List<string> recallTerms = new List<string>();
        public List<ConditionRule> conditions = new List<ConditionRule>();
        [Tooltip("Exact player follow-ups about this outcome, answered only after the NPC witnessed it.")]
        public List<string> followUpPhrases = new List<string>();
        [TextArea] public string followUpLine;
    }

    [Serializable]
    public class CompanionStationData
    {
        public string id;
        public string title;
        [TextArea] public string propDescription;
        [TextArea] public string pendingLine;
        [TextArea] public string completedStatusLine;
        [Tooltip("Short authored recollections allowed only after this station completes. Never include puzzle hints/secrets.")]
        public List<string> completedRecallPhrases = new List<string>();
        [Tooltip("Empty = timed activity. Otherwise hold until this verified player interaction succeeds.")]
        public string holdForInteractionId;
        [Tooltip("Use the existing scene prop instead of drawing a box.")]
        public bool useSceneProp;
        [TextArea] public string invitationLine;
        [TextArea] public string trustedInvitationLine;
        [TextArea] public string cautiousRequestLine;
        [Tooltip("Names/aliases that AI must only mention through verified companion fact tokens (Thai and English).")]
        public List<string> gameplayTerms = new List<string>();
        [Tooltip("Object names recognized only in direct action requests; do not exempt these from puzzle hint checks.")]
        public List<string> requestAliases = new List<string>();
        [Tooltip("Unambiguous prop names accepted in activity questions, separate from direct-request aliases.")]
        public List<string> questionAliases = new List<string>();
        [Tooltip("Authored optional-activity instructions, not main-puzzle hints or a claim that work has started.")]
        [TextArea] public string instructions;
        [Tooltip("How the AI action menu describes helping here (e.g. 'hold the box from the other side'). Empty = generic wording from the title.")]
        public string actionDescription;
        public Vector2 companionPoint;
        public Vector2 playerPoint;
        [Min(0.2f)] public float radius = 0.5f;
        [Min(0.5f)] public float holdSeconds = 3f;
        public List<ConditionRule> conditions = new List<ConditionRule>();
        [TextArea] public string requestLine;
        [TextArea] public string holdingLine;
        [TextArea] public string completedLine;
        [TextArea] public string memory;
        [TextArea] public string journalText;
        [Range(0, 6)] public int relationshipReward = 3;
        [Tooltip("Trusted content effects applied once, only after verified completion.")]
        public List<ActionCommand> completionActions = new List<ActionCommand>();
        [Tooltip("Facts actually inspected during this completed task; reveal conditions still apply.")]
        public List<string> discoveredFactIds = new List<string>();
    }

    // Content, positions and dialogue belong to the asset, never NPC-specific scripts.
    [CreateAssetMenu(menuName = "Game/Companion Scenario", fileName = "CompanionScenario")]
    public class CompanionScenarioData : ScriptableObject
    {
        public string scenarioId;
        public string npcId;
        public string displayName;
        public List<string> addressNames = new List<string>();
        public Rect walkBounds = new Rect(-7.8f, -4.3f, 15.6f, 6f);
        [Min(0.1f)] public float cellSize = 0.4f;
        [Min(0.05f)] public float clearance = 0.2f;
        [Min(0.5f)] public float moveSpeed = 2.6f;
        [Min(0.1f)] public float moveAcceleration = 6.5f;
        [Min(0.1f)] public float moveDeceleration = 8f;
        [Min(0.5f)] public float followDistance = 1.6f;
        [Tooltip("Extra distance before a stopped companion starts following again, to avoid idle shuffling.")]
        [Min(0.15f)] public float followStartBuffer = 0.45f;
        [Tooltip("Show an unfinished activity's label only when the player is this close.")]
        [Min(0.5f)] public float activityLabelDistance = 2.4f;
        [Tooltip("Companion-only walking cadence; does not change the player's animation.")]
        [Range(0.1f, 1.5f)] public float walkAnimationPace = 0.55f;
        [Min(1f)] public float commandDistance = 4f;
        [Min(1f)] public float witnessDistance = 5f;
        [Min(1f)] public float taskTimeoutSeconds = 35f;
        [TextArea] public string welcomeLine;
        [TextArea] public string followLine;
        [TextArea] public string waitLine;
        [TextArea] public string blockedLine;
        [Tooltip("Said when a held station times out after the companion already reached it (blockedLine means 'cannot get there').")]
        [TextArea] public string releaseLine = "ฉันขอปล่อยมือก่อนนะ ถ้าจะลองอีกครั้งก็ชวนฉันได้";
        [TextArea] public string comfortLine;
        [TextArea] public string comfortRepeatLine;
        [TextArea] public string comfortMemory;
        public string comfortEmotion = "homesickness";
        public List<CompanionStationData> stations = new List<CompanionStationData>();
        public List<CompanionObservation> observations = new List<CompanionObservation>();
        public List<CompanionDiscussion> discussions = new List<CompanionDiscussion>();
        public CompanionFramework.ReasoningProfile reasoningProfile;
        [TextArea] public string noEvidenceLine = "ฉันยังไม่มีหลักฐานที่ตรวจสอบได้ เอาสิ่งที่เธอพบมาให้ดู หรือชวนฉันไปสำรวจด้วยกันได้นะ";
        [TextArea] public string discoveryReportLine = "...ฉันมีสิ่งที่เพิ่งตรวจพบอยากเล่าให้ฟัง:";
        [TextArea] public string discoveryFoundLine = "...ฉันมีสิ่งที่ตรวจพบอยากเล่า ไว้คุยกันนะ";
        [TextArea] public string searchNoNewLine = "...ตรวจรอบนี้แล้ว ยังไม่พบหลักฐานใหม่เพิ่ม";
        [TextArea] public string helpClarificationLine = "...อยากให้ฉันช่วยจับตรงไหน บอกชื่อสิ่งนั้นได้เลยนะ";
        [TextArea] public string unverifiedOutcomeLine = "...ฉันยังไม่ได้เห็นว่าเกิดอะไรขึ้น เล่าให้ฟังหน่อย หรือไปลองดูด้วยกันก่อนนะ";
        public List<CompanionSearchSpot> searchSpots = new List<CompanionSearchSpot>();
        [TextArea] public string searchRequestLine = "ฉันจะสำรวจบริเวณที่เดินถึงและตรวจสิ่งที่เห็น ถ้าเจอหลักฐานจะบอกนะ";
        [TextArea] public string searchScopeLine = "...ฉันยังค้นเฉพาะจุดหรือทำหลายอย่างพร้อมกันไม่ได้ ถ้าจะให้ช่วยสำรวจรอบห้อง บอกฉันอีกครั้งนะ";
        [TextArea] public string searchFinishedLine = "สำรวจรอบนี้เสร็จแล้ว หลักฐานที่ยืนยันได้อยู่ในสมุด เราคุยทบทวนกันต่อได้";
        public string SearchFlag(string factId) { return Flag("search." + factId + ".found"); }

        public string Flag(string suffix) { return "companion." + scenarioId + "." + suffix; }
        public string CompletedFlag(CompanionStationData station) { return Flag(station.id + ".completed"); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            foreach (string line in SpokenLines())
                if (MysteryGame.Knowledge.NpcReplyPolicy.MentionsGameControls(line))
                    errors.Add("Companion speech mentions game controls: " + scenarioId);
            if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(npcId)) errors.Add("Companion identity is empty.");
            if (!Finite(cellSize) || !Finite(clearance) || !Finite(moveSpeed) || !Finite(followDistance) ||
                !Finite(moveAcceleration) || !Finite(moveDeceleration) || !Finite(followStartBuffer) ||
                !Finite(activityLabelDistance) || !Finite(walkAnimationPace) ||
                !Finite(commandDistance) || !Finite(witnessDistance) || !Finite(taskTimeoutSeconds) ||
                !Finite(walkBounds.x) || !Finite(walkBounds.y) || !Finite(walkBounds.width) || !Finite(walkBounds.height) ||
                cellSize < 0.15f || clearance <= 0f || moveSpeed <= 0f || followDistance <= clearance ||
                moveAcceleration <= 0f || moveDeceleration <= 0f || followStartBuffer < 0.15f ||
                activityLabelDistance < 0.5f || walkAnimationPace < 0.1f || walkAnimationPace > 1.5f ||
                commandDistance <= 0f || witnessDistance <= 0f || taskTimeoutSeconds <= 0f ||
                walkBounds.width <= 0f || walkBounds.height <= 0f ||
                (walkBounds.width / cellSize + 1f) * (walkBounds.height / cellSize + 1f) > 5000f)
                errors.Add("Companion navigation settings are invalid or too large.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (stations == null || stations.Count == 0) errors.Add("Companion has no stations.");
            else foreach (var station in stations)
            {
                if (station == null || string.IsNullOrWhiteSpace(station.id) || !ids.Add(station.id))
                { errors.Add("Companion station ID is empty/duplicate."); continue; }
                if (string.IsNullOrWhiteSpace(station.title)) errors.Add("Companion station title is empty: " + station.id);
                if (!Finite(station.radius) || !Finite(station.holdSeconds) ||
                    !Finite(station.companionPoint.x) || !Finite(station.companionPoint.y) ||
                    !Finite(station.playerPoint.x) || !Finite(station.playerPoint.y) ||
                    station.radius <= 0f || station.holdSeconds <= 0f || station.holdSeconds >= taskTimeoutSeconds ||
                    Vector2.Distance(station.companionPoint, station.playerPoint) <= station.radius * 2f ||
                    !walkBounds.Contains(station.companionPoint) || !walkBounds.Contains(station.playerPoint))
                    errors.Add("Companion station positions/timing are invalid: " + station.id);
                if (string.IsNullOrWhiteSpace(station.memory) || string.IsNullOrWhiteSpace(station.journalText))
                    errors.Add("Companion station needs a shared memory and journal: " + station.id);
                // The AI action menu shows this text to the model as the NPC's own ability.
                if (MysteryGame.Knowledge.NpcReplyPolicy.MentionsGameControls(station.actionDescription))
                    errors.Add("Companion action description mentions game controls: " + station.id);
                if (station.relationshipReward < 0 || station.relationshipReward > 6)
                    errors.Add("Companion reward must be between 0 and 6: " + station.id);
            }
            ids.Clear();
            if (observations != null) foreach (var observation in observations)
            {
                if (observation == null || string.IsNullOrWhiteSpace(observation.id) ||
                    !ids.Add(observation.id) || string.IsNullOrWhiteSpace(observation.interactionId))
                    errors.Add("Companion observation identity is empty/duplicate.");
                if (observation != null && observation.followUpPhrases != null && observation.followUpPhrases.Count > 0 &&
                    (string.IsNullOrWhiteSpace(observation.followUpLine) || observation.followUpPhrases.Exists(string.IsNullOrWhiteSpace)))
                    errors.Add("Observation follow-up needs phrases and a line.");
            }
            ids.Clear();
            if (searchSpots != null) foreach (var spot in searchSpots)
                if (spot == null || string.IsNullOrWhiteSpace(spot.id) || !ids.Add(spot.id) ||
                    !Finite(spot.point.x) || !Finite(spot.point.y) || !walkBounds.Contains(spot.point) ||
                    !Finite(spot.inspectSeconds) || spot.inspectSeconds < 0.5f || spot.inspectSeconds >= taskTimeoutSeconds)
                    errors.Add("Companion search spot identity, point or timing is invalid.");
            ids.Clear();
            if (discussions != null) foreach (var discussion in discussions)
                if (discussion == null || string.IsNullOrWhiteSpace(discussion.id) || !ids.Add(discussion.id) ||
                    string.IsNullOrWhiteSpace(discussion.line) || discussion.requiredFacts == null || discussion.requiredFacts.Count == 0)
                    errors.Add("Companion discussion needs a unique identity, evidence and authored line.");
            if (reasoningProfile != null)
            {
                if (reasoningProfile.npcId != npcId) errors.Add("Reasoning profile belongs to another NPC.");
                errors.AddRange(reasoningProfile.ValidateConfiguration());
            }
            return errors;
        }

        // UI-only labels/prop descriptions and journal entries are deliberately separate.
        public IEnumerable<string> SpokenLines()
        {
            foreach (string line in new[] { welcomeLine, followLine, waitLine, blockedLine, releaseLine, comfortLine,
                comfortRepeatLine, searchRequestLine, searchScopeLine, searchFinishedLine, noEvidenceLine, discoveryReportLine,
                discoveryFoundLine, searchNoNewLine, helpClarificationLine, unverifiedOutcomeLine }) yield return line;
            if (reasoningProfile != null) foreach (string line in reasoningProfile.SpokenLines()) yield return line;
            if (stations != null) foreach (var station in stations) if (station != null)
                foreach (string line in new[] { station.instructions, station.requestLine, station.holdingLine,
                    station.completedLine, station.invitationLine, station.trustedInvitationLine,
                    station.cautiousRequestLine, station.pendingLine, station.completedStatusLine }) yield return line;
            if (stations != null) foreach (var station in stations) if (station != null && station.completedRecallPhrases != null)
                foreach (string line in station.completedRecallPhrases) yield return line;
            if (observations != null) foreach (var observation in observations) if (observation != null)
            { yield return observation.line; yield return observation.followUpLine; }
            if (searchSpots != null) foreach (var spot in searchSpots) if (spot != null) yield return spot.inspectingLine;
            if (discussions != null) foreach (var discussion in discussions) if (discussion != null) yield return discussion.line;
        }
    }

    [Serializable]
    public class CompanionSearchSpot
    {
        public string id;
        public Vector2 point;
        [Min(0.5f)] public float inspectSeconds = 2f;
        public string factId;
        [TextArea] public string inspectingLine;
        public List<ConditionRule> conditions = new List<ConditionRule>();
    }

    [Serializable]
    public class CompanionDiscussion
    {
        public string id;
        public List<string> requiredFacts = new List<string>();
        public List<ConditionRule> conditions = new List<ConditionRule>();
        [TextArea] public string line;
    }
}
