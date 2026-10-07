using System.Collections.Generic;
using MysteryGame.Knowledge;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysteryGame.Core
{
    public class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }
        public event System.Action<string> ItemAdded;
        /// <summary>New-game reset, not save restore. Scene adapters can reapply authored defaults.</summary>
        public event System.Action StateReset;

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

        private readonly Dictionary<string, int> conversationCounts =
            new Dictionary<string, int>();
        private readonly Dictionary<string, int> npcLastSeenRevision =
            new Dictionary<string, int>();
        private readonly Dictionary<string, string> npcLastSeenRoomProgress =
            new Dictionary<string, string>();
        private bool readingRoomProgress;
        private int worldRevision;

        // =====================================================
        // NPC Memory
        // NPC ID -> Memories
        // =====================================================

        private readonly Dictionary<string, List<string>> npcMemories =
            new Dictionary<string, List<string>>();

        // NPC ID -> Need ID -> Value (0-100)
        private readonly Dictionary<string, Dictionary<string, float>> npcNeeds =
            new Dictionary<string, Dictionary<string, float>>();

        private readonly Dictionary<string, Dictionary<string, float>> npcEmotions =
            new Dictionary<string, Dictionary<string, float>>();

        // Sorted pair key -> relationship score between two NPCs.
        private readonly Dictionary<string, int> npcRelationships =
            new Dictionary<string, int>();

        // NPC ID -> what was actually said, oldest first. Capped so a long
        // session cannot grow the prompt without bound.
        public const int MaxConversationTurns = 40;

        private readonly Dictionary<string, List<ConversationTurn>> conversationLogs =
            new Dictionary<string, List<ConversationTurn>>();

        // Everything the player has read, in the order first read.
        private readonly List<JournalEntry> journal = new List<JournalEntry>();

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
            session = GameSession.CreateDefault();

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SyncSceneState(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        /// <summary>
        /// Edit-mode tests cannot rely on Awake, which Unity only calls in
        /// play mode, so they install (and later clear) the instance here.
        /// </summary>
        public static void UseForTests(GameState state)
        {
            Instance = state;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SyncSceneState(scene.name);
        }

        /// <summary>
        /// Keeps CurrentSceneId honest even when a room scene is opened
        /// directly from the editor instead of being reached through
        /// RoomTransitionManager, and raises the per-room "entered" flag the
        /// AI prompt and the local fallback replies key off.
        /// </summary>
        private void SyncSceneState(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            SetCurrentScene(sceneName);

            var room = KnowledgeLibrary.GetRoom(sceneName);
            if (room != null && !string.IsNullOrWhiteSpace(room.enteredFlag)) SetFlag(room.enteredFlag);
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
            if (session.CurrentGoalId != goalId)
            {
                worldRevision++;
            }
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
                if (flags.Add(flagId)) worldRevision++;
            }
            else
            {
                if (flags.Remove(flagId)) worldRevision++;
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
            if (flags.Remove(flagId)) worldRevision++;
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
                worldRevision++;
                AddHistory("Added item: " + itemId);
                ItemAdded?.Invoke(itemId);
            }
        }

        public void RemoveItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            if (inventory.Remove(itemId)) worldRevision++;
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
        // Journal
        // =====================================================

        /// <summary>
        /// Records text the player read so it can be read again later. The
        /// same id overwrites its text but keeps its place in the list.
        /// Returns true when the entry is new.
        /// </summary>
        public bool AddJournalEntry(string id, string title, string text)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (JournalEntry existing in journal)
            {
                if (existing.Id == id)
                {
                    existing.Title = title ?? string.Empty;
                    existing.Text = text;
                    return false;
                }
            }

            journal.Add(new JournalEntry
            {
                Id = id,
                RoomId = session.CurrentSceneId ?? string.Empty,
                Title = title ?? string.Empty,
                Text = text,
            });
            return true;
        }

        public IReadOnlyList<JournalEntry> GetJournal()
        {
            return journal;
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
                NpcProfileData profile = KnowledgeLibrary.GetNpc(npcId);
                relationships[npcId] = profile != null
                    ? profile.initialRelationship
                    : 50;
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
            worldRevision++;
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
            worldRevision++;

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
            worldRevision++;
        }

        public IReadOnlyList<string> GetNpcMemory(string npcId)
        {
            if (!npcMemories.ContainsKey(npcId))
            {
                return new List<string>();
            }

            return npcMemories[npcId];
        }

        public int GetConversationCount(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId) ||
                !conversationCounts.ContainsKey(npcId))
            {
                return 0;
            }

            return conversationCounts[npcId];
        }

        public int RecordConversation(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return 0;
            }

            int nextCount = GetConversationCount(npcId) + 1;
            conversationCounts[npcId] = nextCount;
            npcLastSeenRevision[npcId] = worldRevision;
            npcLastSeenRoomProgress[npcId] = RoomProgressStamp();
            return nextCount;
        }

        public bool HasWorldChangedSinceConversation(string npcId)
        {
            return !string.IsNullOrWhiteSpace(npcId) &&
                   npcLastSeenRevision.ContainsKey(npcId) &&
                   worldRevision > npcLastSeenRevision[npcId];
        }

        /// <summary>Room facts, puzzle completion and inventory, not social bookkeeping.</summary>
        public bool HasRoomProgressChangedSinceConversation(string npcId)
        {
            string previous;
            return !readingRoomProgress && !string.IsNullOrWhiteSpace(npcId) &&
                npcLastSeenRoomProgress.TryGetValue(npcId, out previous) &&
                previous != RoomProgressStamp();
        }

        private string RoomProgressStamp()
        {
            readingRoomProgress = true;
            try { return BuildRoomProgressStamp(); }
            finally { readingRoomProgress = false; }
        }

        private string BuildRoomProgressStamp()
        {
            var stamp = new System.Text.StringBuilder();
            AppendProgress(stamp, GetCurrentScene());
            var room = KnowledgeLibrary.GetRoom(GetCurrentScene());
            if (room != null)
            {
                if (room.steps != null)
                    foreach (var step in room.steps)
                        if (step != null)
                        {
                            AppendProgress(stamp, step.stepId);
                            stamp.Append(step.IsCompleted(this) ? '1' : '0');
                        }
                if (room.facts != null)
                    foreach (var fact in room.facts)
                        if (fact != null)
                        {
                            AppendProgress(stamp, fact.factId);
                            stamp.Append(fact.IsRevealed(this) ? '1' : '0');
                        }
            }
            var items = new List<string>(inventory);
            items.Sort(System.StringComparer.Ordinal);
            foreach (string item in items) AppendProgress(stamp, item);
            return stamp.ToString();
        }

        private static void AppendProgress(System.Text.StringBuilder stamp, string value)
        {
            value = value ?? string.Empty;
            stamp.Append(value.Length).Append(':').Append(value).Append(';');
        }

        // =====================================================
        // NPC Needs
        // =====================================================

        public bool HasNpcNeed(string npcId, string needId)
        {
            return !string.IsNullOrWhiteSpace(npcId) &&
                   !string.IsNullOrWhiteSpace(needId) &&
                   npcNeeds.ContainsKey(npcId) &&
                   npcNeeds[npcId].ContainsKey(needId);
        }

        public float GetNpcNeed(string npcId, string needId)
        {
            if (!HasNpcNeed(npcId, needId))
            {
                return 0f;
            }

            return npcNeeds[npcId][needId];
        }

        public void SetNpcNeed(string npcId, string needId, float value)
        {
            if (string.IsNullOrWhiteSpace(npcId) ||
                string.IsNullOrWhiteSpace(needId))
            {
                return;
            }

            if (!npcNeeds.ContainsKey(npcId))
            {
                npcNeeds[npcId] = new Dictionary<string, float>();
            }

            npcNeeds[npcId][needId] = Mathf.Clamp(value, 0f, 100f);
            // Passive needs are read directly by mini-event triggers. They are
            // not player room progress and must not invalidate return greetings.
        }

        public void ChangeNpcNeed(string npcId, string needId, float amount)
        {
            SetNpcNeed(npcId, needId, GetNpcNeed(npcId, needId) + amount);
        }

        public bool HasNpcEmotion(string npcId, string emotionId)
        {
            return !string.IsNullOrWhiteSpace(npcId) &&
                   !string.IsNullOrWhiteSpace(emotionId) &&
                   npcEmotions.ContainsKey(npcId) &&
                   npcEmotions[npcId].ContainsKey(emotionId);
        }

        public float GetNpcEmotion(string npcId, string emotionId)
        {
            return HasNpcEmotion(npcId, emotionId)
                ? npcEmotions[npcId][emotionId]
                : 0f;
        }

        public void SetNpcEmotion(string npcId, string emotionId, float value)
        {
            if (string.IsNullOrWhiteSpace(npcId) ||
                string.IsNullOrWhiteSpace(emotionId))
            {
                return;
            }

            if (!npcEmotions.ContainsKey(npcId))
            {
                npcEmotions[npcId] = new Dictionary<string, float>();
            }

            npcEmotions[npcId][emotionId] = Mathf.Clamp(value, 0f, 100f);
            // Keep emotion-triggered events active without inventing room progress.
        }

        public void ChangeNpcEmotion(string npcId, string emotionId, float amount)
        {
            SetNpcEmotion(
                npcId,
                emotionId,
                GetNpcEmotion(npcId, emotionId) + amount
            );
        }

        public int GetNpcRelationship(string firstNpcId, string secondNpcId)
        {
            string key = GetNpcPairKey(firstNpcId, secondNpcId);
            if (string.IsNullOrEmpty(key))
            {
                return 50;
            }

            if (!npcRelationships.ContainsKey(key))
            {
                npcRelationships[key] =
                    InitialNpcRelationship(firstNpcId, secondNpcId);
            }

            return npcRelationships[key];
        }

        public void ChangeNpcRelationship(
            string firstNpcId,
            string secondNpcId,
            int amount)
        {
            string key = GetNpcPairKey(firstNpcId, secondNpcId);
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            npcRelationships[key] = Mathf.Clamp(
                GetNpcRelationship(firstNpcId, secondNpcId) + amount,
                0,
                100
            );
            worldRevision++;
        }

        /// <summary>
        /// Where two NPCs start, authored as a bond on either one's profile.
        /// When both author one, the average wins so neither file silently
        /// overrides the other.
        /// </summary>
        private static int InitialNpcRelationship(string firstNpcId, string secondNpcId)
        {
            NpcProfileData first = KnowledgeLibrary.GetNpc(firstNpcId);
            NpcProfileData second = KnowledgeLibrary.GetNpc(secondNpcId);
            NpcBond a = first != null ? first.BondWith(secondNpcId) : null;
            NpcBond b = second != null ? second.BondWith(firstNpcId) : null;

            if (a != null && b != null)
            {
                return (a.initialValue + b.initialValue) / 2;
            }

            if (a != null)
            {
                return a.initialValue;
            }

            return b != null ? b.initialValue : 50;
        }

        // =====================================================
        // Conversation Log
        // =====================================================

        public void AddConversationTurn(string npcId, string speakerId, string text)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            List<ConversationTurn> log;
            if (!conversationLogs.TryGetValue(npcId, out log))
            {
                log = new List<ConversationTurn>();
                conversationLogs[npcId] = log;
            }

            log.Add(new ConversationTurn { SpeakerId = speakerId, Text = text.Trim() });
            if (log.Count > MaxConversationTurns)
            {
                log.RemoveRange(0, log.Count - MaxConversationTurns);
            }
        }

        public IReadOnlyList<ConversationTurn> GetConversationLog(string npcId)
        {
            List<ConversationTurn> log;
            if (string.IsNullOrWhiteSpace(npcId) ||
                !conversationLogs.TryGetValue(npcId, out log))
            {
                return new List<ConversationTurn>();
            }

            return log;
        }

        private static string GetNpcPairKey(string firstNpcId, string secondNpcId)
        {
            if (string.IsNullOrWhiteSpace(firstNpcId) ||
                string.IsNullOrWhiteSpace(secondNpcId) ||
                firstNpcId == secondNpcId)
            {
                return string.Empty;
            }

            return string.CompareOrdinal(firstNpcId, secondNpcId) < 0
                ? firstNpcId + "|" + secondNpcId
                : secondNpcId + "|" + firstNpcId;
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
                snapshot.Relationships.Add(
                    new RelationshipSnapshot
                    {
                        NpcId = pair.Key,
                        Value = pair.Value
                    }
                );
            }

            // NPC Memory
            foreach (
                KeyValuePair<string, List<string>> pair
                in npcMemories)
            {
                snapshot.NpcMemories.Add(
                    new NpcMemorySnapshot
                    {
                        NpcId = pair.Key,
                        Memories = new List<string>(pair.Value)
                    }
                );
            }

            foreach (KeyValuePair<string, Dictionary<string, float>> npcPair
                     in npcNeeds)
            {
                foreach (KeyValuePair<string, float> needPair in npcPair.Value)
                {
                    snapshot.NpcNeeds.Add(
                        new NpcNeedSnapshot
                        {
                            NpcId = npcPair.Key,
                            NeedId = needPair.Key,
                            Value = needPair.Value
                        }
                    );
                }
            }

            foreach (KeyValuePair<string, Dictionary<string, float>> npcPair
                     in npcEmotions)
            {
                foreach (KeyValuePair<string, float> emotionPair in npcPair.Value)
                {
                    snapshot.NpcEmotions.Add(
                        new NpcEmotionSnapshot
                        {
                            NpcId = npcPair.Key,
                            EmotionId = emotionPair.Key,
                            Value = emotionPair.Value
                        }
                    );
                }
            }

            foreach (KeyValuePair<string, int> pair in npcRelationships)
            {
                string[] npcIds = pair.Key.Split('|');
                snapshot.NpcRelationships.Add(
                    new NpcRelationshipSnapshot
                    {
                        FirstNpcId = npcIds[0],
                        SecondNpcId = npcIds[1],
                        Value = pair.Value
                    }
                );
            }

            foreach (KeyValuePair<string, int> pair in conversationCounts)
            {
                snapshot.ConversationCounts.Add(
                    new RelationshipSnapshot { NpcId = pair.Key, Value = pair.Value }
                );
            }

            foreach (KeyValuePair<string, List<ConversationTurn>> pair
                     in conversationLogs)
            {
                snapshot.ConversationLogs.Add(
                    new ConversationLogSnapshot
                    {
                        NpcId = pair.Key,
                        Turns = new List<ConversationTurn>(pair.Value)
                    }
                );
            }

            foreach (JournalEntry entry in journal)
            {
                snapshot.Journal.Add(new JournalEntry
                {
                    Id = entry.Id,
                    RoomId = entry.RoomId,
                    Title = entry.Title,
                    Text = entry.Text,
                });
            }

            return snapshot;
        }

        /// <summary>
        /// Replaces the whole runtime state with a saved snapshot. The scene
        /// itself is not loaded here; the caller decides when to move there.
        /// </summary>
        public void RestoreSnapshot(StateSnapshot snapshot)
        {
            if (snapshot == null) return;
            string error;
            if (!TryRestoreSnapshot(snapshot, out error)) Debug.LogWarning(error);
        }

        public bool TryRestoreSnapshot(StateSnapshot snapshot, out string error)
        {
            StateSnapshot normalized;
            if (!SnapshotValidator.TryNormalize(snapshot, out normalized, out error)) return false;
            ApplySnapshot(normalized);
            return true;
        }

        private void ApplySnapshot(StateSnapshot snapshot)
        {

            session.CurrentSceneId = snapshot.CurrentSceneId;
            session.CurrentChapterId = snapshot.CurrentChapterId;
            session.CurrentGoalId = snapshot.CurrentGoalId;

            flags.Clear();
            flags.UnionWith(snapshot.Flags);
            inventory.Clear();
            inventory.UnionWith(snapshot.Inventory);
            playerHistory.Clear();
            playerHistory.AddRange(snapshot.PlayerHistory);

            relationships.Clear();
            foreach (RelationshipSnapshot entry in snapshot.Relationships)
            {
                relationships[entry.NpcId] = entry.Value;
            }

            npcMemories.Clear();
            foreach (NpcMemorySnapshot entry in snapshot.NpcMemories)
            {
                npcMemories[entry.NpcId] = new List<string>(entry.Memories);
            }

            npcNeeds.Clear();
            foreach (NpcNeedSnapshot entry in snapshot.NpcNeeds)
            {
                if (!npcNeeds.ContainsKey(entry.NpcId))
                {
                    npcNeeds[entry.NpcId] = new Dictionary<string, float>();
                }
                npcNeeds[entry.NpcId][entry.NeedId] = entry.Value;
            }

            npcEmotions.Clear();
            foreach (NpcEmotionSnapshot entry in snapshot.NpcEmotions)
            {
                if (!npcEmotions.ContainsKey(entry.NpcId))
                {
                    npcEmotions[entry.NpcId] = new Dictionary<string, float>();
                }
                npcEmotions[entry.NpcId][entry.EmotionId] = entry.Value;
            }

            npcRelationships.Clear();
            foreach (NpcRelationshipSnapshot entry in snapshot.NpcRelationships)
            {
                string key = GetNpcPairKey(entry.FirstNpcId, entry.SecondNpcId);
                if (!string.IsNullOrEmpty(key))
                {
                    npcRelationships[key] = entry.Value;
                }
            }

            conversationCounts.Clear();
            npcLastSeenRevision.Clear();
            npcLastSeenRoomProgress.Clear();
            foreach (RelationshipSnapshot entry in snapshot.ConversationCounts)
            {
                conversationCounts[entry.NpcId] = entry.Value;
            }

            conversationLogs.Clear();
            foreach (ConversationLogSnapshot entry in snapshot.ConversationLogs)
            {
                conversationLogs[entry.NpcId] =
                    new List<ConversationTurn>(entry.Turns);
            }

            journal.Clear();
            if (snapshot.Journal != null)   // saves from before the journal
            {
                foreach (JournalEntry entry in snapshot.Journal)
                {
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.Id))
                    {
                        journal.Add(entry);
                    }
                }
            }

            worldRevision++;
        }

        /// <summary>
        /// Resets all runtime state for a completely fresh game session.
        /// </summary>
        public void ResetState()
        {
            session = GameSession.CreateDefault();
            flags.Clear();
            inventory.Clear();
            playerHistory.Clear();
            relationships.Clear();
            conversationCounts.Clear();
            npcLastSeenRevision.Clear();
            worldRevision = 0;
            npcLastSeenRoomProgress.Clear();
            npcMemories.Clear();
            npcNeeds.Clear();
            npcEmotions.Clear();
            npcRelationships.Clear();
            conversationLogs.Clear();
            journal.Clear();

            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(activeScene))
            {
                SyncSceneState(activeScene);
            }
            AddHistory("Started new game");
            StateReset?.Invoke();
        }
    }
}

