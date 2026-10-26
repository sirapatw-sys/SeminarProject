using System.Collections.Generic;
using MysteryGame.Core;

namespace MysteryGame.Companions
{
    // One next-turn answer to a question the game actually asked.
    // Not a physical task, save entry, or authorization from model prose.
    internal sealed class PendingCompanionHelp
    {
        internal NpcCompanionController Actor;
        internal GameState State;
        internal int ConversationVersion;
        internal int TaskRevision;
        internal string RoomId;
        internal readonly HashSet<string> CandidateIds = new HashSet<string>();
    }
}
