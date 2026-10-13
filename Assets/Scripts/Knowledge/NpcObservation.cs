using System;
using MysteryGame.Core;

namespace MysteryGame.Knowledge
{
    // Observation is a physical game action. Player/AI prose cannot grant it.
    public static class NpcObservation
    {
        public static string Flag(string npc, string room, string fact)
        {
            return "observed." + Uri.EscapeDataString(npc ?? "").Replace(".", "%2E") + "." +
                Uri.EscapeDataString(room ?? "").Replace(".", "%2E") + "." + Uri.EscapeDataString(fact ?? "").Replace(".", "%2E");
        }
        public static bool Knows(GameState state, string npc, string room, string fact)
        { return state != null && state.HasFlag(Flag(npc, room, fact)); }
    }
}
