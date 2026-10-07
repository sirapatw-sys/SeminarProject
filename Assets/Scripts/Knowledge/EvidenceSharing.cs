using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Knowledge
{
    public class EvidenceShareResult
    {
        public RoomFact Fact;
        public string Reply;
        public bool AlreadyShared;
        public bool JournalEntryAdded;
        public int RelationshipDelta;
    }

    /// <summary>
    /// Sharing is a verified player action, never inferred from typed or AI text.
    /// Flags persist in existing saves; each NPC learns only their own evidence.
    /// </summary>
    public static class EvidenceSharing
    {
        public static string SharedFlag(string npcId, string roomId, string factId)
        {
            return "evidence." + Encode(npcId) + "." + Encode(roomId) + "." + Encode(factId) + ".shared";
        }

        private static string Encode(string id)
        {
            return Uri.EscapeDataString(id ?? string.Empty).Replace(".", "%2E");
        }

        public static string AnySharedFlag(string npcId, string roomId)
        {
            return "evidence." + Encode(npcId) + "." + Encode(roomId) + ".shared_any";
        }

        public static bool HasBeenShared(GameState state, string npcId, string roomId, string factId)
        {
            return state != null && !string.IsNullOrWhiteSpace(npcId) &&
                !string.IsNullOrWhiteSpace(roomId) && !string.IsNullOrWhiteSpace(factId) &&
                state.HasFlag(SharedFlag(npcId, roomId, factId));
        }

        public static List<RoomFact> Available(GameState state, string npcId)
        {
            var available = new List<RoomFact>();
            if (state == null) return available;
            var room = KnowledgeLibrary.GetRoom(state.GetCurrentScene());
            var npc = KnowledgeLibrary.GetNpc(npcId);
            if (room == null || npc == null || room.facts == null) return available;
            foreach (var fact in room.facts)
                if (CanShare(fact, npc, state)) available.Add(fact);
            return available;
        }

        private static bool CanShare(RoomFact fact, NpcProfileData npc, GameState state)
        {
            return fact != null && !string.IsNullOrWhiteSpace(fact.factId) &&
                !string.IsNullOrWhiteSpace(fact.statement) && fact.canShareAsEvidence &&
                !fact.isPuzzleAnswer && !npc.IsForbidden(fact.factId) && fact.IsRevealed(state);
        }

        public static bool TryShare(GameState state, string npcId, string factId,
            out EvidenceShareResult result)
        {
            result = null;
            if (state == null) return false;
            var room = KnowledgeLibrary.GetRoom(state.GetCurrentScene());
            var npc = KnowledgeLibrary.GetNpc(npcId);
            var fact = room != null ? room.FindFact(factId) : null;
            if (npc == null || !CanShare(fact, npc, state)) return false;

            bool repeated = HasBeenShared(state, npc.npcId, room.roomId, fact.factId);
            EvidenceReaction reaction = null;
            if (npc.evidenceReactions != null)
                foreach (var candidate in npc.evidenceReactions)
                    if (candidate != null && candidate.factId == fact.factId &&
                        ConditionRule.AllHold(candidate.when, state))
                    { reaction = candidate; break; }

            string interpretation = reaction != null
                ? (repeated ? reaction.repeatReply : reaction.reply) : null;
            if (string.IsNullOrWhiteSpace(interpretation))
                interpretation = repeated
                    ? "หลักฐานนี้ฉันดูแล้ว เรายังกลับมาทบทวนด้วยกันได้"
                    : "ขอบคุณที่เอามาให้ดู ตอนนี้ฉันรู้ข้อมูลนี้แล้ว";
            result = new EvidenceShareResult
            {
                Fact = fact,
                AlreadyShared = repeated,
                Reply = fact.statement + "\n\n" + interpretation,
            };

            if (!repeated)
            {
                state.SetFlag(SharedFlag(npc.npcId, room.roomId, fact.factId));
                state.SetFlag(AnySharedFlag(npc.npcId, room.roomId));
                int before = state.GetRelationship(npc.npcId);
                int delta = reaction != null ? Mathf.Clamp(reaction.relationshipDelta, -15, 10) : 0;
                if (delta != 0) state.ChangeRelationship(npc.npcId, delta);
                result.RelationshipDelta = state.GetRelationship(npc.npcId) - before;
                state.AddNpcMemory(npc.npcId, "ผู้เล่นนำหลักฐาน '" + fact.EvidenceTitle + "' มาให้ดู: " + fact.statement);
                state.AddHistory("Shared evidence " + room.roomId + "/" + fact.factId + " with " + npc.npcId);
                result.JournalEntryAdded = state.AddJournalEntry(
                    SharedFlag(npc.npcId, room.roomId, fact.factId),
                    "คุยเรื่อง " + fact.EvidenceTitle + " กับ " + npc.displayName, result.Reply);
            }
            state.AddConversationTurn(npc.npcId, ConversationTurn.Player, "(หลักฐาน) " + fact.EvidenceTitle);
            state.AddConversationTurn(npc.npcId, npc.npcId, result.Reply);
            return true;
        }
    }
}
