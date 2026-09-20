using System;
using System.Collections.Generic;
using UnityEngine;

namespace MysteryGame.Knowledge
{
    /// <summary>
    /// Loads the authored knowledge assets from Resources and caches them.
    /// Rooms live at Resources/Knowledge/Rooms/&lt;roomId&gt;, NPC profiles at
    /// Resources/Knowledge/Npcs/&lt;npcId&gt;.
    /// </summary>
    public static class KnowledgeLibrary
    {
        public const string RoomPath = "Knowledge/Rooms/";
        public const string NpcPath = "Knowledge/Npcs/";

        private static readonly Dictionary<string, RoomKnowledgeData> rooms =
            new Dictionary<string, RoomKnowledgeData>(
                StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, NpcProfileData> npcs =
            new Dictionary<string, NpcProfileData>(
                StringComparer.OrdinalIgnoreCase);

        public static RoomKnowledgeData GetRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            RoomKnowledgeData cached;
            if (rooms.TryGetValue(roomId, out cached))
            {
                return cached;
            }

            RoomKnowledgeData loaded =
                Resources.Load<RoomKnowledgeData>(RoomPath + roomId);
            rooms[roomId] = loaded;
            return loaded;
        }

        public static NpcProfileData GetNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return null;
            }

            NpcProfileData cached;
            if (npcs.TryGetValue(npcId, out cached))
            {
                return cached;
            }

            NpcProfileData loaded =
                Resources.Load<NpcProfileData>(NpcPath + npcId);
            npcs[npcId] = loaded;
            return loaded;
        }

        /// <summary>Lets edit-mode tests inject assets without Resources.</summary>
        public static void Register(RoomKnowledgeData room)
        {
            if (room != null && !string.IsNullOrWhiteSpace(room.roomId))
            {
                rooms[room.roomId] = room;
            }
        }

        public static void Register(NpcProfileData npc)
        {
            if (npc != null && !string.IsNullOrWhiteSpace(npc.npcId))
            {
                npcs[npc.npcId] = npc;
            }
        }

        public static void ClearCache()
        {
            rooms.Clear();
            npcs.Clear();
        }
    }
}
