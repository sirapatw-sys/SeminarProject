using System;
using System.Collections.Generic;
using UnityEngine;

namespace MysteryGame.Knowledge
{
    /// <summary>
    /// Which NPCs are standing in which loaded room, kept by the NPC objects
    /// themselves (enabled = present, disabled or unloaded = gone). Lets every
    /// NPC know who else is in its room without the game authoring it twice:
    /// Alice sees only the player in Room01, the gatekeeper in Room02 until
    /// she leaves, and Rina and Stelle in Room03.
    /// </summary>
    public static class RoomPresence
    {
        private static readonly Dictionary<string, Dictionary<string, int>> rooms =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            rooms.Clear();
        }

        public static void Enter(string npcId, string roomId)
        {
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            Dictionary<string, int> present;
            if (!rooms.TryGetValue(roomId, out present))
            {
                present = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                rooms[roomId] = present;
            }

            int count;
            present.TryGetValue(npcId, out count);
            present[npcId] = count + 1;
        }

        public static void Leave(string npcId, string roomId)
        {
            Dictionary<string, int> present;
            int count;
            if (string.IsNullOrWhiteSpace(npcId) || string.IsNullOrWhiteSpace(roomId) ||
                !rooms.TryGetValue(roomId, out present) || !present.TryGetValue(npcId, out count))
            {
                return;
            }

            if (count <= 1) present.Remove(npcId);
            else present[npcId] = count - 1;
        }

        /// <summary>True once any NPC object of this room has been seen (the room is loaded).</summary>
        public static bool HasRoom(string roomId)
        {
            return !string.IsNullOrWhiteSpace(roomId) && rooms.ContainsKey(roomId);
        }

        public static bool IsIn(string roomId, string npcId)
        {
            Dictionary<string, int> present;
            return !string.IsNullOrWhiteSpace(roomId) && !string.IsNullOrWhiteSpace(npcId) &&
                   rooms.TryGetValue(roomId, out present) && present.ContainsKey(npcId);
        }

        /// <summary>The NPC ids in the room, in a stable order.</summary>
        public static List<string> In(string roomId)
        {
            List<string> ids = new List<string>();
            Dictionary<string, int> present;
            if (!string.IsNullOrWhiteSpace(roomId) && rooms.TryGetValue(roomId, out present))
            {
                ids.AddRange(present.Keys);
                ids.Sort(StringComparer.OrdinalIgnoreCase);
            }

            return ids;
        }

        /// <summary>For tests: forget every room.</summary>
        public static void Clear()
        {
            rooms.Clear();
        }
    }
}
