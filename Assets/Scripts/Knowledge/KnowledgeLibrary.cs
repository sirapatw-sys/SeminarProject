using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Knowledge
{
    /// <summary>
    /// The selected GameDefinition owns its knowledge namespace. Resources
    /// are only a legacy fallback when no definition exists.
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

        private static GameDefinition cachedGame;

        private static GameDefinition EnsureGameCache()
        {
            GameDefinition game = GameDefinition.Current;
            if (cachedGame != game)
            {
                rooms.Clear();
                npcs.Clear();
                cachedGame = game;
            }
            return game;
        }

        public static RoomKnowledgeData GetRoom(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return null;
            }

            GameDefinition game = EnsureGameCache();

            RoomKnowledgeData cached;
            if (rooms.TryGetValue(roomId, out cached))
            {
                return cached;
            }

            RoomKnowledgeData loaded = game != null
                ? game.rooms.Find(room => room != null && string.Equals(
                    room.roomId, roomId, StringComparison.OrdinalIgnoreCase))
                : Resources.Load<RoomKnowledgeData>(RoomPath + roomId);
            rooms[roomId] = loaded;
            return loaded;
        }

        public static NpcProfileData GetNpc(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
            {
                return null;
            }

            GameDefinition game = EnsureGameCache();

            NpcProfileData cached;
            if (npcs.TryGetValue(npcId, out cached))
            {
                return cached;
            }

            NpcProfileData loaded = game != null
                ? game.npcs.Find(npc => npc != null && string.Equals(
                    npc.npcId, npcId, StringComparison.OrdinalIgnoreCase))
                : Resources.Load<NpcProfileData>(NpcPath + npcId);
            npcs[npcId] = loaded;
            return loaded;
        }

        /// <summary>Lets edit-mode tests inject assets without Resources.</summary>
        public static void Register(RoomKnowledgeData room)
        {
            EnsureGameCache();
            if (room != null && !string.IsNullOrWhiteSpace(room.roomId))
            {
                rooms[room.roomId] = room;
            }
        }

        public static void Register(NpcProfileData npc)
        {
            EnsureGameCache();
            if (npc != null && !string.IsNullOrWhiteSpace(npc.npcId))
            {
                npcs[npc.npcId] = npc;
            }
        }

        public static void ClearCache()
        {
            rooms.Clear();
            npcs.Clear();
            cachedGame = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeCache() { ClearCache(); }
    }
}
