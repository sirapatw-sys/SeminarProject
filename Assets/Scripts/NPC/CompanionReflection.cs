using System;
using System.Linq;
using CompanionFramework;
using MysteryGame.Core;

namespace MysteryGame.Companions
{
    // Only verified observations can back a shared reaction. A player's claim,
    // model text, or the current puzzle flag alone does not imply NPC presence.
    public static class CompanionReflection
    {
        public static bool TryReply(CompanionScenarioData scenario, GameState state,
            string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (scenario == null || state == null || scenario.observations == null) return false;
            string text = ReasoningQueryMatcher.Normalize(message);
            if (text.Length == 0) return false;
            var matching = scenario.observations.Where(o => o != null && o.followUpPhrases != null &&
                o.followUpPhrases.Any(p => !string.IsNullOrWhiteSpace(p) && ReasoningQueryMatcher.Normalize(p) == text)).ToList();
            if (matching.Count == 0) return false;
            var witnessed = matching.Where(o => state.HasFlag(scenario.Flag("observed." + o.id)) &&
                ConditionRule.AllHold(o.conditions, state) && !string.IsNullOrWhiteSpace(o.followUpLine)).ToList();
            reply = new GeneratedChatReply {
                reply = witnessed.Count == 1 ? witnessed[0].followUpLine : scenario.unverifiedOutcomeLine,
                referencedFactIds = Array.Empty<string>(), relationshipDelta = 0 };
            return true;
        }
    }
}
