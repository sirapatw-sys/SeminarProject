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

        public static NpcKnowledgeContext BuildForMessage(string npcId, string roomId, GameState state, string message)
        {
            RoomKnowledgeData room = KnowledgeLibrary.GetRoom(roomId);
            var scenario = room != null ? room.companionScenario : null;
            bool companion = scenario != null && scenario.npcId == npcId && state != null &&
                state.GetCurrentScene() == roomId && scenario.stations != null;
            var subjects = new List<string>();
            if (companion) subjects.AddRange(MysteryGame.Companions.CompanionDialogueCommands.RequestTopicsFor(message));
            if (companion)
            {
                MysteryGame.Companions.CompanionStationData requested;
                if (MysteryGame.Companions.CompanionDialogueCommands.Parse(message, scenario, out requested) == MysteryGame.Companions.CompanionCommand.Help &&
                    requested != null && requested.requestAliases != null) subjects.AddRange(requested.requestAliases);
            }
            if (companion) foreach (var station in scenario.stations)
            {
                if (station == null) continue;
                subjects.Add(station.title);
                if (station.gameplayTerms != null) subjects.AddRange(station.gameplayTerms);
            }
            var context = Build(npcId, roomId, state,
                PlayerIntentClassifier.IsAskingForHint(message, room != null ? room.gameplayTerms : null, subjects));
            if (companion)
            {
                context.RecentMemories.Clear();
                context.RecentMemories.AddRange(NpcMemoryRecall.Select(state.GetNpcMemory(npcId), message, scenario, MemoryLimit));
            }
            if (!companion || context.PlayerAskedForHint) return context;
            foreach (var station in scenario.stations)
            {
                if (station == null) continue;
                var names = new List<string> { station.title };
                if (station.gameplayTerms != null) names.AddRange(station.gameplayTerms);
                if (!PlayerIntentClassifier.IsCompanionRequest(message, names)) continue;
                string prefix = scenario.Flag(station.id + ".");
                foreach (var fact in context.CompanionFacts)
                    if (fact.factId == prefix + "pending" || fact.factId == prefix + "completed" ||
                        fact.factId == prefix + "instructions" || fact.factId == prefix + "unavailable")
                        context.RequestedCompanionFacts.Add(fact);
            }
            return context;
        }

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

            FillCompanion(context, state);

            context.TotalSteps = context.Room.steps.Count;
            context.CompletedSteps = context.Room.CompletedStepCount(state);
            context.CurrentStep = context.Room.CurrentStep(state);

            SplitFacts(context, state);

            context.AllowedHintLevel = ResolveHintLevel(context, tone, relationship);
            context.DeterministicHint = ResolveHint(context);

            return context;
        }

        private static void FillCompanion(NpcKnowledgeContext context, GameState state)
        {
            var scenario = context.Room.companionScenario;
            if (scenario == null || state == null || state.GetCurrentScene() != context.Room.roomId ||
                scenario.npcId != context.NpcId || scenario.stations == null) return;
            foreach (var station in scenario.stations)
            {
                if (station == null || string.IsNullOrWhiteSpace(station.id) || string.IsNullOrWhiteSpace(station.title)) continue;
                bool completed = state.HasFlag(scenario.CompletedFlag(station));
                context.CompanionFacts.Add(new RoomFact {
                    factId = scenario.Flag(station.id + (completed ? ".completed" : ".pending")),
                    statement = "กิจกรรม «" + station.title + "» " + (completed ? "ทำสำเร็จแล้ว" : "ยังไม่สำเร็จ")
                });
                if (!completed)
                {
                    bool available = ConditionRule.AllHold(station.conditions, state);
                    if (!available || !string.IsNullOrWhiteSpace(station.instructions))
                        context.CompanionFacts.Add(new RoomFact {
                            factId = scenario.Flag(station.id + (available ? ".instructions" : ".unavailable")),
                            statement = available ? station.instructions : "กิจกรรม «" + station.title + "» ยังไม่พร้อมให้ช่วยในตอนนี้"
                        });
                }
                context.CompanionTerms.Add(station.title);
                if (station.gameplayTerms != null) context.CompanionTerms.AddRange(station.gameplayTerms);
            }
            var actor = MysteryGame.Companions.NpcCompanionController.Find(context.NpcId, context.Room.roomId);
            if (actor == null || state != GameState.Instance || actor.Scenario != scenario) return;
            string status = actor.Mode == MysteryGame.Companions.CompanionMode.Following ? "กำลังเดินตามผู้เล่น" :
                actor.Mode == MysteryGame.Companions.CompanionMode.Searching ? "กำลังสำรวจตามคำขอผู้เล่น ยังไม่ยืนยันว่าพบหลักฐานใหม่" :
                actor.Mode == MysteryGame.Companions.CompanionMode.GoingToHelp ? "กำลังเดินไปช่วยกิจกรรมที่ผู้เล่นขอ ยังไม่สำเร็จ" :
                actor.Mode == MysteryGame.Companions.CompanionMode.Helping ? "กำลังช่วยจับกลไก รอผู้เล่นทำอีกด้าน ยังไม่สำเร็จ" : "กำลังรออยู่";
            context.CompanionFacts.Add(new RoomFact {
                factId = scenario.Flag("mode." + actor.Mode), statement = scenario.displayName + " " + status
            });
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

                FillPresence(context, state);
                var companion = MysteryGame.Companions.NpcCompanionController.Find(context.NpcId,
                    context.Room != null ? context.Room.roomId : null);
                if (companion != null && state == GameState.Instance) context.ActiveNotes.Add(companion.PromptState());

                if (npc.bonds != null)
                {
                    foreach (NpcBond bond in npc.bonds)
                    {
                        if (bond == null || string.IsNullOrWhiteSpace(bond.otherNpcId))
                        {
                            continue;
                        }

                        // Someone the player has not met yet (Rina while still
                        // in Room01) is not brought up before their room.
                        if (context.PresentLines != null && !RoomPresence.IsIn(context.Room != null ? context.Room.roomId : null, bond.otherNpcId) &&
                            (state == null || state.GetConversationCount(bond.otherNpcId) == 0))
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

        /// <summary>The other characters standing in the NPC's room, from the live scene.</summary>
        private static void FillPresence(NpcKnowledgeContext context, GameState state)
        {
            string roomId = context.Room != null ? context.Room.roomId : null;
            if (!RoomPresence.HasRoom(roomId))
            {
                return;
            }

            context.PresentLines = new List<string>();
            foreach (string otherId in RoomPresence.In(roomId))
            {
                if (string.Equals(otherId, context.NpcId, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                NpcProfileData other = KnowledgeLibrary.GetNpc(otherId);
                string name = other != null && !string.IsNullOrWhiteSpace(other.displayName) ? other.displayName : otherId;
                string looks = other != null ? other.Appearance : string.Empty;
                context.PresentLines.Add(string.IsNullOrWhiteSpace(looks) ? name : name + ": " + looks);
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

                if (revealed && npcKnows && !forbidden && !fact.isPuzzleAnswer && !fact.isPuzzleGuidance)
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
            GameDefinition game = GameDefinition.Current;
            if (relationship >= (game != null ? game.explicitHintRelationship : ExplicitHintRelationship))
            {
                level = HintLevel.Explicit;
            }
            else if (relationship >= (game != null ? game.normalHintRelationship : NormalHintRelationship))
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
