using System;

namespace MysteryGame.Core
{
    [Serializable]
    public class GameSession
    {
        public string CurrentSceneId = "Room01";
        public string CurrentChapterId = "Chapter01";
        public string CurrentGoalId = "escape_room";
    }
}