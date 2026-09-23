using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Knowledge
{
    /// <summary>
    /// Turns GameState plus the authored room/NPC assets into the slice of
    /// knowledge one NPC is allowed to use for one reply.
    /// </summary>
    public static class NpcKnowledgeContextBuilder
    {
        /// <summary>Hint level bands used when a profile authors no tones.</summary>
        public const int NormalHintRelationship = 45;
        public const int ExplicitHintRelationship = 70;

        public static NpcKnowledgeContext Build(
            string npcId,
            string roomId,
            GameState state,
            bool playerAskedForHint)
        {
            NpcKnowledgeContext context = new NpcKnowledgeContext
            {
                NpcId = npcId ?? string.Empty,
                Room = KnowledgeLibrary.GetRoom(roomId),
                Npc = KnowledgeLibrary.GetNpc(npcId),
                PlayerAskedForHint = playerAskedForHint,
            };

            int relationship = state != null && !string.IsNullOrWhiteSpace(npcId)
                ? state.GetRelationship(npcId)
                : 50;
            context.Relationship = relationship;
            RelationshipTone tone = context.Npc != null
                ? context.Npc.ToneFor(relationship)
                : null;
            context.Tone = tone != null ? tone.tone : string.Empty;

            FillCharacter(context, state);

            if (context.Room == null)
            {
                return context;
            }

            context.TotalSteps = context.Room.steps.Count;
            context.CompletedSteps = context.Room.CompletedStepCount(state);
            context.CurrentStep = context.Room.CurrentStep(state);

            SplitFacts(context, state);

            context.AllowedHintLevel = ResolveHintLevel(context, tone, relationship);
            context.DeterministicHint = ResolveHint(context);

            return context;
        }

        /// <summary>How many memory notes go into one prompt.</summary>
        public const int MemoryLimit = 4;

        /// <summary>
        /// The NPC's own side: backstory, secrets, situational direction,
        /// bonds with other NPCs and what was said before.
        /// </summary>
        private static void FillCharacter(NpcKnowledgeContext context, GameState state)
        {
            NpcProfileData npc = context.Npc;
            if (npc != null)
            {
                npc.SplitPersonalFacts(
                    state, context.ShareablePersonalFacts, context.LockedSecrets);

                if (npc.situationalNotes != null)
                {
                    foreach (SituationalNote note in npc.situationalNotes)
                    {
                        if (note != null && !string.IsNullOrWhiteSpace(note.note) &&
                            note.IsActive(state))
                        {
                            context.ActiveNotes.Add(note.note);
                        }
                    }
                }

                if (npc.bonds != null)
                {
                    foreach (NpcBond bond in npc.bonds)
                    {
                        if (bond == null || string.IsNullOrWhiteSpace(bond.otherNpcId))
                        {
                            continue;
                        }

                        int value = state != null
                            ? state.GetNpcRelationship(npc.npcId, bond.otherNpcId)
                            : bond.initialValue;
                        context.BondLines.Add(
                            bond.otherNpcId + " (" + value + "/100): " + bond.description);
                    }
                }
            }

            if (state == null || string.IsNullOrWhiteSpace(context.NpcId))
            {
                return;
            }

            context.History.AddRange(NpcKnowledgeContext.SelectRecentTurns(
                state.GetConversationLog(context.NpcId),
                NpcKnowledgeContext.HistoryTurnLimit,
                NpcKnowledgeContext.HistoryCharBudget));

            // Typed lines are already in the history above, so only the
            // notes written by choices and events are worth repeating here.
            IReadOnlyList<string> memories = state.GetNpcMemory(context.NpcId);
            for (int i = memories.Count - 1;
                 i >= 0 && context.RecentMemories.Count < MemoryLimit;
                 i--)
            {
                string memory = memories[i];
                if (!string.IsNullOrWhiteSpace(memory) &&
                    !memory.StartsWith(PlayerSaidPrefix))
                {
                    context.RecentMemories.Insert(0, memory);
                }
            }
        }

        /// <summary>Prefix DialogueManager uses when it files a typed line as a memory.</summary>
        public const string PlayerSaidPrefix = "ผู้เล่นพูดว่า:";

        private static void SplitFacts(NpcKnowledgeContext context, GameState state)
        {
            foreach (RoomFact fact in context.Room.facts)
            {
                if (fact == null || string.IsNullOrWhiteSpace(fact.factId))
                {
                    continue;
                }

                bool revealed = fact.IsRevealed(state);
                bool npcKnows = context.Npc == null
                    ? revealed
                    : context.Npc.KnowsFact(fact.factId, state);
                bool forbidden = context.Npc != null &&
                                 context.Npc.IsForbidden(fact.factId);

                if (revealed && npcKnows && !forbidden)
                {
                    context.KnownFacts.Add(fact);
                }
                else
                {
                    context.WithheldFacts.Add(fact);
                }
            }
        }

        private static HintLevel ResolveHintLevel(
            NpcKnowledgeContext context,
            RelationshipTone tone,
            int relationship)
        {
            if (!context.PlayerAskedForHint)
            {
                return HintLevel.None;
            }

            if (context.Npc != null && !context.Npc.givesHints)
            {
                return HintLevel.None;
            }

            HintLevel level = HintLevel.Vague;
            if (relationship >= ExplicitHintRelationship)
            {
                level = HintLevel.Explicit;
            }
            else if (relationship >= NormalHintRelationship)
            {
                level = HintLevel.Normal;
            }

            if (tone != null && level > tone.maxHintLevel)
            {
                level = tone.maxHintLevel;
            }

            return level;
        }

        private static string ResolveHint(NpcKnowledgeContext context)
        {
            if (context.AllowedHintLevel == HintLevel.None ||
                context.CurrentStep == null)
            {
                return string.Empty;
            }

            return context.CurrentStep.HintFor(context.AllowedHintLevel);
        }

        /// <summary>
        /// The deterministic answer the offline fallback uses, so a missing API
        /// key still walks the player forward exactly like the AI would.
        /// </summary>
        public static string BuildOfflineReply(NpcKnowledgeContext context)
        {
            if (context == null || !context.HasData)
            {
                return string.Empty;
            }

            if (!context.PlayerAskedForHint)
            {
                return string.Empty;
            }

            if (context.AllowedHintLevel == HintLevel.None)
            {
                return context.Npc != null &&
                       !string.IsNullOrWhiteSpace(context.Npc.refuseHintLine)
                    ? context.Npc.refuseHintLine
                    : string.Empty;
            }

            return context.DeterministicHint;
        }
    }
}
