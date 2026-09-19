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

        public Dictionary<string, int> Relationships =
            new Dictionary<string, int>();

        // =====================================================
        // NPC Memory
        // NPC ID -> Memory List
        // =====================================================

        public Dictionary<string, List<string>> NpcMemories =
            new Dictionary<string, List<string>>();

        // =====================================================
        // Player History
        // =====================================================

        public List<string> PlayerHistory =
            new List<string>();
    }
}