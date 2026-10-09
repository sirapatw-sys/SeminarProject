using UnityEngine;

namespace MysteryGame.Core
{
    [CreateAssetMenu(menuName = "Game/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Sprite image;
        [Tooltip("Horizontal frames in image. Use 1 for a still image, preserving its aspect ratio.")]
        [Min(1)] public int horizontalFrames = 1;

        public static ItemData Find(string id)
        {
            GameDefinition game = GameDefinition.Current;
            if (game == null || string.IsNullOrWhiteSpace(id)) return null;
            return game.items.Find(item => item != null &&
                string.Equals(item.itemId, id, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}
