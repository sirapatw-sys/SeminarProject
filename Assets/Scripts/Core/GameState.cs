using System.Collections.Generic;
using UnityEngine;

namespace MysteryGame.Core
{
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        // =====================================================
        // Session
        // =====================================================

        [SerializeField]
        private GameSession session = new GameSession();

        // =====================================================
        // Runtime Flags
        // =====================================================

        private readonly HashSet<string> flags =
            new HashSet<string>();

        // =====================================================
        // Inventory
        // =====================================================

        private readonly HashSet<string> inventory =
            new HashSet<string>();

        // =====================================================
        // Player History
        // =====================================================

        private readonly List<string> playerHistory =
            new List<string>();

        // =====================================================
        // Relationships
        // NPC ID -> Score
        // =====================================================

        private readonly Dictionary<string, int> relationships =
            new Dictionary<string, int>();

        // =====================================================
        // NPC Memory
        // NPC ID -> Memories
        // =====================================================

        private readonly Dictionary<string, List<string>> npcMemories =
            new Dictionary<string, List<string>>();

        // =====================================================
        // Unity Lifecycle
        // =====================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            DontDestroyOnLoad(gameObject);
        }

        // =====================================================
        // Session
        // =====================================================

        public GameSession GetSession()
        {
            return session;
        }

        public void SetCurrentScene(string sceneId)
        {
            session.CurrentSceneId = sceneId;
        }

        public void SetCurrentChapter(string chapterId)
        {
            session.CurrentChapterId = chapterId;
        }

        public void SetCurrentGoal(string goalId)
        {
            session.CurrentGoalId = goalId;
        }

        public string GetCurrentScene()
        {
            return session.CurrentSceneId;
        }

        public string GetCurrentChapter()
        {
            return session.CurrentChapterId;
        }

        public string GetCurrentGoal()
        {
            return session.CurrentGoalId;
        }

        // =====================================================
        // Flags
        // =====================================================

        public void SetFlag(string flagId, bool value = true)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                Debug.LogWarning("Cannot set an empty flag ID.");
                return;
            }

            if (value)
            {
                flags.Add(flagId);
            }
            else
            {
                flags.Remove(flagId);
            }

            Debug.Log(
            $"[GameState] Flag '{flagId}' = {value}"
        );
        }

        public bool HasFlag(string flagId)
        {
            if (string.IsNullOrWhiteSpace(flagId))
            {
                return false;
            }

            return flags.Contains(flagId);
        }

        public void RemoveFlag(string flagId)
        {
            if (!string.IsNullOrWhiteSpace(flagId))
            {
                flags.Remove(flagId);
            }
        }

        // =====================================================
        // Inventory
        // =====================================================

        public void AddItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogWarning("Cannot add an empty item ID.");
                return;
            }

            if (inventory.Add(itemId))
            {
                AddHistory("Added item: " + itemId);
            }
        }

        public void RemoveItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            inventory.Remove(itemId);
        }

        public bool HasItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            return inventory.Contains(itemId);
        }

        // =====================================================
        // Player History
        // =====================================================

        public void AddHistory(string entry)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                return;
            }

            playerHistory.Add(entry);

            Debug.Log("[History] " + entry);
        }

        public IReadOnlyList<string> GetPlayerHistory()
        {
            return playerHistory;
        }

        // =====================================================
        // NPC Relationship
        // =====================================================

        public int GetRelationship(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return 0;
            }

            if (!relationships.ContainsKey(npcId))
            {
                relationships[npcId] = 50;
            }

            return relationships[npcId];
        }

        public void SetRelationship(string npcId, int value)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return;
            }

            relationships[npcId] = Mathf.Clamp(value, 0, 100);
        }

        public void ChangeRelationship(string npcId, int amount)
        {
            int currentValue = GetRelationship(npcId);

            int newValue = Mathf.Clamp(
                currentValue + amount,
                0,
                100
            );

            relationships[npcId] = newValue;

            AddHistory(
                $"Relationship[{npcId}] changed by {amount}"
            );

            Debug.Log(
                $"[Relationship] {npcId} = {newValue}"
            );
        }

        // =====================================================
        // NPC Memory
        // =====================================================

        public void AddNpcMemory(string npcId, string memory)
        {
            if (string.IsNullOrWhiteSpace(npcId) ||
                string.IsNullOrWhiteSpace(memory))
            {
                return;
            }

            if (!npcMemories.ContainsKey(npcId))
            {
                npcMemories[npcId] =
                    new List<string>();
            }

            npcMemories[npcId].Add(memory);
        }

        public IReadOnlyList<string> GetNpcMemory(string npcId)
        {
            if (!npcMemories.ContainsKey(npcId))
            {
                return new List<string>();
            }

            return npcMemories[npcId];
        }

        // =====================================================
        // Snapshot
        // =====================================================

        public StateSnapshot CreateSnapshot()
        {
            StateSnapshot snapshot =
                new StateSnapshot();

            // Session
            snapshot.CurrentSceneId =
                session.CurrentSceneId;

            snapshot.CurrentChapterId =
                session.CurrentChapterId;

            snapshot.CurrentGoalId =
                session.CurrentGoalId;

            // Flags
            snapshot.Flags.AddRange(flags);

            // Inventory
            snapshot.Inventory.AddRange(inventory);

            // History
            snapshot.PlayerHistory.AddRange(
                playerHistory
            );

            // Relationships
            foreach (KeyValuePair<string, int> pair
                     in relationships)
            {
                snapshot.Relationships[pair.Key] =
                    pair.Value;
            }

            // NPC Memory
            foreach (
                KeyValuePair<string, List<string>> pair
                in npcMemories)
            {
                snapshot.NpcMemories[pair.Key] =
                    new List<string>(pair.Value);
            }

            return snapshot;
        }
    }
}