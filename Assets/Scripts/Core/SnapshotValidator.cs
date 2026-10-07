using System;
using System.Collections.Generic;
using UnityEngine;

namespace MysteryGame.Core
{
    /// <summary>Validate and detach save data before any live state is changed.</summary>
    public static class SnapshotValidator
    {
        public static bool TryNormalize(StateSnapshot source, out StateSnapshot result, out string error)
        {
            result = null;
            error = "Save state is missing or invalid.";
            // Scene is required; a completely empty/corrupt state must not
            // masquerade as a valid "new game" save and erase live progress.
            if (source == null || !Text(source.CurrentSceneId)) return false;
            if (!StringsValid(source.Flags) || !StringsValid(source.Inventory) ||
                !StringsValid(source.PlayerHistory) ||
                !Valid(source.Relationships, e => e != null && Text(e.NpcId) && Score(e.Value)) ||
                !Valid(source.NpcMemories, e => e != null && Text(e.NpcId) && StringsValid(e.Memories)) ||
                !Valid(source.NpcNeeds, e => e != null && Text(e.NpcId) && Text(e.NeedId) && Metric(e.Value)) ||
                !Valid(source.NpcEmotions, e => e != null && Text(e.NpcId) && Text(e.EmotionId) && Metric(e.Value)) ||
                !Valid(source.NpcRelationships, e => e != null && Text(e.FirstNpcId) && Text(e.SecondNpcId) &&
                    e.FirstNpcId != e.SecondNpcId && Score(e.Value)) ||
                !Valid(source.ConversationCounts, e => e != null && Text(e.NpcId) && e.Value >= 0) ||
                !Valid(source.ConversationLogs, e => e != null && Text(e.NpcId) &&
                    Valid(e.Turns, t => t != null && Text(t.SpeakerId) && Text(t.Text))) ||
                !Valid(source.Journal, e => e != null && Text(e.Id) && Text(e.Text)))
                return false;

            try
            {
                // Copy nested records as well: callers must not retain references
                // into restored GameState. This preserves the existing JSON schema.
                var copy = JsonUtility.FromJson<StateSnapshot>(JsonUtility.ToJson(source));
                var defaults = GameSession.CreateDefault();
                if (!Text(copy.CurrentChapterId)) copy.CurrentChapterId = defaults.CurrentChapterId;
                if (!Text(copy.CurrentGoalId)) copy.CurrentGoalId = defaults.CurrentGoalId;
                copy.Flags = copy.Flags ?? new List<string>();
                copy.Inventory = copy.Inventory ?? new List<string>();
                copy.PlayerHistory = copy.PlayerHistory ?? new List<string>();
                copy.Relationships = copy.Relationships ?? new List<RelationshipSnapshot>();
                copy.NpcMemories = copy.NpcMemories ?? new List<NpcMemorySnapshot>();
                copy.NpcNeeds = copy.NpcNeeds ?? new List<NpcNeedSnapshot>();
                copy.NpcEmotions = copy.NpcEmotions ?? new List<NpcEmotionSnapshot>();
                copy.NpcRelationships = copy.NpcRelationships ?? new List<NpcRelationshipSnapshot>();
                copy.ConversationCounts = copy.ConversationCounts ?? new List<RelationshipSnapshot>();
                copy.ConversationLogs = copy.ConversationLogs ?? new List<ConversationLogSnapshot>();
                copy.Journal = copy.Journal ?? new List<JournalEntry>();
                foreach (var entry in copy.NpcMemories) entry.Memories = entry.Memories ?? new List<string>();
                foreach (var entry in copy.ConversationLogs) entry.Turns = entry.Turns ?? new List<ConversationTurn>();
                result = copy;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = "Save state cannot be restored: " + ex.GetType().Name;
                return false;
            }
        }

        private static bool Text(string value) { return !string.IsNullOrWhiteSpace(value); }
        private static bool Score(int value) { return value >= 0 && value <= 100; }
        private static bool Metric(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 100f; }
        private static bool StringsValid(List<string> values) { return Valid(values, Text); }
        private static bool Valid<T>(List<T> values, Predicate<T> valid)
        {
            // Missing collections are legitimate in older saves.
            if (values == null) return true;
            foreach (T entry in values) if (!valid(entry)) return false;
            return true;
        }
    }
}
