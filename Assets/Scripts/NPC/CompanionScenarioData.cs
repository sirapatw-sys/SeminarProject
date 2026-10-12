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
        public List<ConditionRule> conditions = new List<ConditionRule>();
    }

    [Serializable]
    public class CompanionStationData
    {
        public string id;
        public string title;
        [Tooltip("Names/aliases that AI must only mention through verified companion fact tokens (Thai and English).")]
        public List<string> gameplayTerms = new List<string>();
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
    }

    // Content, positions and dialogue belong to the asset, never NPC-specific scripts.
    [CreateAssetMenu(menuName = "Game/Companion Scenario", fileName = "CompanionScenario")]
    public class CompanionScenarioData : ScriptableObject
    {
        public string scenarioId;
        public string npcId;
        public string displayName;
        public Rect walkBounds = new Rect(-7.8f, -4.3f, 15.6f, 6f);
        [Min(0.1f)] public float cellSize = 0.4f;
        [Min(0.05f)] public float clearance = 0.2f;
        [Min(0.5f)] public float moveSpeed = 4.4f;
        [Min(0.5f)] public float followDistance = 1.6f;
        [Min(1f)] public float commandDistance = 4f;
        [Min(1f)] public float witnessDistance = 5f;
        [Min(1f)] public float taskTimeoutSeconds = 35f;
        [TextArea] public string welcomeLine;
        [TextArea] public string followLine;
        [TextArea] public string waitLine;
        [TextArea] public string blockedLine;
        [TextArea] public string comfortLine;
        [TextArea] public string comfortRepeatLine;
        [TextArea] public string comfortMemory;
        public string comfortEmotion = "homesickness";
        public List<CompanionStationData> stations = new List<CompanionStationData>();
        public List<CompanionObservation> observations = new List<CompanionObservation>();

        public string Flag(string suffix) { return "companion." + scenarioId + "." + suffix; }
        public string CompletedFlag(CompanionStationData station) { return Flag(station.id + ".completed"); }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public List<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(npcId)) errors.Add("Companion identity is empty.");
            if (!Finite(cellSize) || !Finite(clearance) || !Finite(moveSpeed) || !Finite(followDistance) ||
                !Finite(commandDistance) || !Finite(witnessDistance) || !Finite(taskTimeoutSeconds) ||
                !Finite(walkBounds.x) || !Finite(walkBounds.y) || !Finite(walkBounds.width) || !Finite(walkBounds.height) ||
                cellSize < 0.15f || clearance <= 0f || moveSpeed <= 0f || followDistance <= clearance ||
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
                if (station.relationshipReward < 0 || station.relationshipReward > 6)
                    errors.Add("Companion reward must be between 0 and 6: " + station.id);
            }
            ids.Clear();
            if (observations != null) foreach (var observation in observations)
                if (observation == null || string.IsNullOrWhiteSpace(observation.id) ||
                    !ids.Add(observation.id) || string.IsNullOrWhiteSpace(observation.interactionId))
                    errors.Add("Companion observation identity is empty/duplicate.");
            return errors;
        }
    }
}
