using System.Collections.Generic;
using System.Linq;
using MysteryGame.Knowledge;

namespace MysteryGame.Core
{
    public static class ContentValidator
    {
        public static List<string> Validate(GameDefinition game, IEnumerable<InteractionData> interactions,
            IEnumerable<InputPuzzleData> puzzles, IEnumerable<MiniEventData> events)
        {
            var errors = new List<string>();
            if (game == null) { errors.Add("GameDefinition is missing."); return errors; }
            if (string.IsNullOrWhiteSpace(game.gameId)) errors.Add("gameId is empty.");
            if (!game.rooms.Any(r => r != null && r.roomId == game.firstScene)) errors.Add("firstScene has no room data.");
            if (game.normalHintRelationship > game.explicitHintRelationship) errors.Add("Hint thresholds are reversed.");
            var roomIds = Unique(game.rooms.Where(r => r != null).Select(r => r.roomId), "room", errors);
            var npcIds = Unique(game.npcs.Where(n => n != null).Select(n => n.npcId), "npc", errors);
            var itemIds = Unique(game.items.Where(i => i != null).Select(i => i.itemId), "item", errors);
            var allInteractions = interactions.Where(i => i != null).ToList();
            var interactionIds = Unique(allInteractions.Select(i => i.interactionId), "interaction", errors);
            var facts = new HashSet<string>();
            foreach (var room in game.rooms.Where(r => r != null))
            {
                Unique(room.steps.Where(s => s != null).Select(s => s.stepId), room.roomId + "/step", errors);
                foreach (var fact in room.facts.Where(f => f != null))
                {
                    if (string.IsNullOrWhiteSpace(fact.factId) || !facts.Add(fact.factId))
                        errors.Add(room.roomId + ": empty/duplicate fact " + fact.factId);
                    if (fact.isPuzzleAnswer && (fact.protectedTerms == null || fact.protectedTerms.Count == 0))
                        errors.Add(room.roomId + "/" + fact.factId + ": puzzle answer has no protectedTerms.");
                    if (fact.canShareAsEvidence && fact.isPuzzleAnswer)
                        errors.Add(room.roomId + "/" + fact.factId + ": puzzle answers cannot be shareable evidence.");
                    if (fact.canShareAsEvidence && (string.IsNullOrWhiteSpace(fact.evidenceTitle) ||
                        string.IsNullOrWhiteSpace(fact.statement)))
                        errors.Add(room.roomId + "/" + fact.factId + ": evidence needs a title and statement.");
                    Rules(fact.revealedWhen, itemIds, npcIds, errors, roomIds);
                }
                foreach (var step in room.steps.Where(s => s != null))
                {
                    if (!string.IsNullOrWhiteSpace(step.interactionId) && !interactionIds.Contains(step.interactionId))
                        errors.Add(room.roomId + "/" + step.stepId + ": missing interaction " + step.interactionId);
                    if (step.completedWhen == null || step.completedWhen.Count == 0)
                        errors.Add(room.roomId + "/" + step.stepId + ": no completion conditions.");
                    if (string.IsNullOrWhiteSpace(step.vagueHint))
                        errors.Add(room.roomId + "/" + step.stepId + ": missing vague hint (cannot escalate to explicit).");
                    Rules(step.availableWhen, itemIds, npcIds, errors, roomIds);
                    Rules(step.completedWhen, itemIds, npcIds, errors, roomIds);
                }
            }
            foreach (var npc in game.npcs.Where(n => n != null))
            {
                foreach (var fact in npc.backstory.Concat(npc.secrets))
                {
                    if (fact == null) { errors.Add(npc.npcId + ": null personal fact."); continue; }
                    if (string.IsNullOrWhiteSpace(fact.factId) || !facts.Add(fact.factId))
                        errors.Add("Empty/duplicate personal fact " + fact.factId);
                    if (string.IsNullOrWhiteSpace(fact.statement)) errors.Add(npc.npcId + ": empty personal statement " + fact.factId);
                    if (fact.when != null && fact.when.Count > 0 &&
                        (fact.protectedTerms == null || !fact.protectedTerms.Any(t => !string.IsNullOrWhiteSpace(t))))
                        errors.Add(npc.npcId + "/" + fact.factId + ": gated personal fact has no protectedTerms.");
                    if (fact.protectedTerms != null && fact.protectedTerms.Any(string.IsNullOrWhiteSpace))
                        errors.Add(npc.npcId + "/" + fact.factId + ": empty protected term.");
                    Rules(fact.when, itemIds, npcIds, errors, roomIds);
                }
                foreach (var rule in npc.fallbackReplies.Concat(npc.returnGreetings))
                    if (rule != null) Rules(rule.when, itemIds, npcIds, errors, roomIds);
            }
            foreach (var npc in game.npcs.Where(n => n != null))
            {
                foreach (var id in npc.knownFactIds.Concat(npc.forbiddenFactIds).Concat(npc.learnedFacts.Select(f => f.factId)))
                    if (!facts.Contains(id)) errors.Add(npc.npcId + ": unknown fact " + id);
                if (npc.evidenceReactions != null)
                    foreach (var reaction in npc.evidenceReactions)
                    {
                        var evidence = reaction != null ? game.rooms.Where(r => r != null)
                            .SelectMany(r => r.facts).FirstOrDefault(f => f != null && f.factId == reaction.factId) : null;
                        if (evidence == null || !evidence.canShareAsEvidence || evidence.isPuzzleAnswer || npc.IsForbidden(evidence.factId))
                            errors.Add(npc.npcId + ": invalid evidence reaction " + (reaction != null ? reaction.factId : "(null)"));
                        if (reaction == null) continue;
                        if (string.IsNullOrWhiteSpace(reaction.reply)) errors.Add(npc.npcId + ": empty evidence reply " + reaction.factId);
                        Rules(reaction.when, itemIds, npcIds, errors, roomIds);
                    }
            }
            foreach (var interaction in allInteractions)
            {
                Rules(interaction.conditions, itemIds, npcIds, errors, roomIds);
                Actions(interaction.actions, itemIds, npcIds, errors);
                if (!string.IsNullOrWhiteSpace(interaction.popupItemId) && !itemIds.Contains(interaction.popupItemId))
                    errors.Add(interaction.interactionId + ": unknown popup item " + interaction.popupItemId);
                if (!string.IsNullOrWhiteSpace(interaction.transitionScene) && !roomIds.Contains(interaction.transitionScene))
                    errors.Add(interaction.interactionId + ": missing destination " + interaction.transitionScene);
            }
            var puzzleList = puzzles.Where(p => p != null).ToList();
            Unique(puzzleList.Select(p => p.puzzleId), "puzzle", errors);
            foreach (var puzzle in puzzleList)
            {
                if (puzzle.numeric && puzzle.acceptedAnswers?.Count != 1)
                    errors.Add(puzzle.puzzleId + ": keypad requires exactly one numeric answer.");
                if (string.IsNullOrWhiteSpace(puzzle.solvedFlag)) errors.Add(puzzle.puzzleId + ": missing solvedFlag.");
                if (puzzle.acceptedAnswers == null || puzzle.acceptedAnswers.Count == 0 ||
                    puzzle.acceptedAnswers.Any(string.IsNullOrWhiteSpace)) errors.Add(puzzle.puzzleId + ": empty answer.");
                if (puzzle.numeric && puzzle.acceptedAnswers != null &&
                    puzzle.acceptedAnswers.Any(a => a == null || !System.Text.RegularExpressions.Regex.IsMatch(a, @"^\d{1,8}$")))
                    errors.Add(puzzle.puzzleId + ": numeric answers must be 1-8 digits.");
                Rules(puzzle.conditions, itemIds, npcIds, errors, roomIds); Actions(puzzle.successActions, itemIds, npcIds, errors);
            }
            var eventList = events.Where(e => e != null).ToList();
            Unique(eventList.Select(e => e.eventId), "event", errors);
            foreach (var ev in eventList)
            {
                if (!npcIds.Contains(ev.npcId)) errors.Add(ev.eventId + ": unknown npc " + ev.npcId);
                if (ev.dialogue == null || ev.dialogue.lines.Count == 0) errors.Add(ev.eventId + ": missing offline dialogue.");
                if (ev.completeOnChoice && (ev.dialogue == null || ev.dialogue.choices.Count == 0))
                    errors.Add(ev.eventId + ": completeOnChoice requires choices.");
                if (ev.dialogue != null)
                    foreach (var choice in ev.dialogue.choices)
                        if (choice != null) Actions(choice.actions, itemIds, npcIds, errors);
                Rules(ev.conditions, itemIds, npcIds, errors, roomIds);
                if (ev.offlineVariants != null)
                    foreach (var variant in ev.offlineVariants)
                        if (variant == null || ev.dialogue == null || variant.choices.Count != ev.dialogue.choices.Count)
                            errors.Add(ev.eventId + ": offline variant has mismatched choices.");
            }
            return errors;
        }
        private static HashSet<string> Unique(IEnumerable<string> ids, string type, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (var id in ids)
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) errors.Add(type + ": empty/duplicate id " + id);
            return seen;
        }
        private static void Rules(IEnumerable<ConditionRule> rules, HashSet<string> items, HashSet<string> npcs,
            List<string> errors, HashSet<string> rooms)
        {
            if (rules == null) return;
            foreach (var rule in rules)
            {
                if (rule == null) { errors.Add("Null condition."); continue; }
                if (string.IsNullOrWhiteSpace(rule.targetId)) errors.Add("Empty condition target.");
                if (rule.type == ConditionType.CurrentScene && !rooms.Contains(rule.targetId))
                    errors.Add("Unknown condition scene " + rule.targetId);
                if ((rule.type == ConditionType.HasItem || rule.type == ConditionType.MissingItem) && !items.Contains(rule.targetId))
                    errors.Add("Unknown condition item " + rule.targetId);
                if ((rule.type == ConditionType.RelationshipAtLeast || rule.type == ConditionType.RelationshipAtMost ||
                    rule.type == ConditionType.RoomProgressSinceLastTalk) && !npcs.Contains(rule.targetId))
                    errors.Add("Unknown condition npc " + rule.targetId);
            }
        }
        private static void Actions(IEnumerable<ActionCommand> actions, HashSet<string> items, HashSet<string> npcs, List<string> errors)
        {
            if (actions == null) return;
            foreach (var action in actions)
            {
                if (action == null) { errors.Add("Null action."); continue; }
                if ((action.type == ActionType.AddItem || action.type == ActionType.RemoveItem) && !items.Contains(action.targetId))
                    errors.Add("Unknown action item " + action.targetId);
                if (action.type == ActionType.ChangeRelationship && !npcs.Contains(action.targetId))
                    errors.Add("Unknown action npc " + action.targetId);
            }
        }
    }
}
