using System;
using System.Collections.Generic;

namespace MysteryGame.Core
{
    [Serializable]
    public class StateSnapshot
    {
        // =====================================================
        // Session
        // =====================================================

        public string CurrentSceneId;
        public string CurrentChapterId;
        public string CurrentGoalId;

        // =====================================================
        // Runtime State
        // =====================================================

        public List<string> Flags = new List<string>();
        public List<string> Inventory = new List<string>();

        // =====================================================
        // NPC Relationships
        // NPC ID -> Relationship Score
        // =====================================================

        public List<RelationshipSnapshot> Relationships =
            new List<RelationshipSnapshot>();

        // =====================================================
        // NPC Memory
        // NPC ID -> Memory List
        // =====================================================

        public List<NpcMemorySnapshot> NpcMemories =
            new List<NpcMemorySnapshot>();

        public List<NpcNeedSnapshot> NpcNeeds =
            new List<NpcNeedSnapshot>();

        public List<NpcEmotionSnapshot> NpcEmotions =
            new List<NpcEmotionSnapshot>();

        public List<NpcRelationshipSnapshot> NpcRelationships =
            new List<NpcRelationshipSnapshot>();

        // =====================================================
        // Player History
        // =====================================================

        public List<string> PlayerHistory =
            new List<string>();

        // =====================================================
        // Conversations
        // =====================================================

        public List<RelationshipSnapshot> ConversationCounts =
            new List<RelationshipSnapshot>();

        public List<ConversationLogSnapshot> ConversationLogs =
            new List<ConversationLogSnapshot>();

        // =====================================================
        // Journal
        // =====================================================

        public List<JournalEntry> Journal = new List<JournalEntry>();
    }

    /// <summary>
    /// Something the player read and may want to read again: an object's
    /// description or an item card. Id is "interaction.&lt;id&gt;" or "item.&lt;id&gt;".
    /// </summary>
    [Serializable]
    public class JournalEntry
    {
        public string Id;
        public string RoomId;
        public string Title;
        public string Text;
    }

    /// <summary>One line of a conversation. SpeakerId is "player" or an NPC id.</summary>
    [Serializable]
    public class ConversationTurn
    {
        public const string Player = "player";

        public string SpeakerId;
        public string Text;
    }

    [Serializable]
    public class ConversationLogSnapshot
    {
        public string NpcId;
        public List<ConversationTurn> Turns = new List<ConversationTurn>();
    }

    [Serializable]
    public class RelationshipSnapshot
    {
        public string NpcId;
        public int Value;
    }

    [Serializable]
    public class NpcMemorySnapshot
    {
        public string NpcId;
        public List<string> Memories = new List<string>();
    }

    [Serializable]
    public class NpcNeedSnapshot
    {
        public string NpcId;
        public string NeedId;
        public float Value;
    }

    [Serializable]
    public class NpcEmotionSnapshot
    {
        public string NpcId;
        public string EmotionId;
        public float Value;
    }

    [Serializable]
    public class NpcRelationshipSnapshot
    {
        public string FirstNpcId;
        public string SecondNpcId;
        public int Value;
    }
}
