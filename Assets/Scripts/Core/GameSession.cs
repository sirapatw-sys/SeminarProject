using System;

namespace MysteryGame.Core
{
    [Serializable]
    public class GameSession
    {
        public string CurrentSceneId = "Room01";
        public string CurrentChapterId = "Chapter01";
        public string CurrentGoalId = "escape_room";
        public static GameSession CreateDefault()
        {
            var game = GameDefinition.Current;
            return game == null ? new GameSession() : new GameSession
            {
                CurrentSceneId = game.firstScene,
                CurrentChapterId = game.initialChapter,
                CurrentGoalId = game.initialGoal
            };
        }
    }
}
