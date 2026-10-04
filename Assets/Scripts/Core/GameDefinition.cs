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
        public static GameDefinition Override { get; set; }
        public static GameDefinition Current { get { return Override != null ? Override :
            Resources.Load<GameDefinition>("GameDefinition"); } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeOverride() { Override = null; }
    }
}
