using System.Collections.Generic;
using MysteryGame.Knowledge;
using UnityEngine;

namespace MysteryGame.Core
{
    [CreateAssetMenu(menuName = "Game/Game Definition")]
    public class GameDefinition : ScriptableObject
    {
        public string gameId = "mystery";
        public string title = "AI Mystery Game";
        public string firstScene = "Room01";
        public string initialChapter = "Chapter01";
        public string initialGoal = "escape_room";
        [Range(0, 100)] public int normalHintRelationship = 45;
        [Range(0, 100)] public int explicitHintRelationship = 70;
        [TextArea(2, 5)] public string[] introLines;
        public List<RoomKnowledgeData> rooms = new List<RoomKnowledgeData>();
        public List<NpcProfileData> npcs = new List<NpcProfileData>();
        public List<ItemData> items = new List<ItemData>();
        private static GameDefinition overrideDefinition;
        private static GameDefinition unscopedOverride;
        private static GameDefinitionSelector sceneOwner;

        /// <summary>An explicit override supersedes any scene-owned selection.</summary>
        public static GameDefinition Override
        {
            get { return overrideDefinition; }
            set
            {
                sceneOwner = null;
                unscopedOverride = null;
                overrideDefinition = value;
            }
        }
        public static GameDefinition Current { get { return Override != null ? Override :
            Resources.Load<GameDefinition>("GameDefinition"); } }

        internal static void SelectForScene(GameDefinition definition, GameDefinitionSelector owner)
        {
            if (definition == null || owner == null) return;
            // A replacement selector inherits the original unscoped value,
            // never the previous scene's temporary definition.
            if (ReferenceEquals(sceneOwner, null)) unscopedOverride = overrideDefinition;
            sceneOwner = owner;
            overrideDefinition = definition;
            KnowledgeLibrary.ClearCache();
        }

        internal static void ReleaseForScene(GameDefinitionSelector owner)
        {
            // Scene destruction can occur after a new selector's Awake.
            // Old scenes must not erase a newer selection or explicit override.
            if (!ReferenceEquals(sceneOwner, owner)) return;
            overrideDefinition = unscopedOverride;
            sceneOwner = null;
            unscopedOverride = null;
            KnowledgeLibrary.ClearCache();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeOverride() { Override = null; }
    }
}
