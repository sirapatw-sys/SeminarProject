using System;
using System.Collections.Generic;
using System.Linq;
using CompanionFramework;
using MysteryGame.Core;
using MysteryGame.Knowledge;

namespace MysteryGame.Companions
{
    // Unity/game adapter. Only CompanionConversation's verified player-send path
    // invokes this in gameplay; evaluating a hypothesis never solves a puzzle.
    public static class CompanionReasoning
    {
        private sealed class World : IReasoningWorld
        {
            private readonly HashSet<string> known;
            private readonly GameState state;
            public int AllowedDetail { get; private set; }
            public World(NpcKnowledgeContext context, GameState state)
            {
                this.state = state;
                known = new HashSet<string>(context.KnownFacts.Select(f => f.factId), StringComparer.Ordinal);
                AllowedDetail = (int)context.AllowedHintLevel;
            }
            public bool KnowsFact(string id) => known.Contains(id);
            public bool HasFlag(string id) => state.HasFlag(id);
        }
        public static bool TryReply(CompanionScenarioData scenario, GameState state, string roomId,
            string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (scenario == null || scenario.reasoningProfile == null || state == null ||
                state.GetCurrentScene() != roomId || string.IsNullOrWhiteSpace(message)) return false;
            var profile = scenario.reasoningProfile;
            var query = ReasoningQueryMatcher.Read(profile, message);
            if (query.Kind == ReasoningQueryKind.None) return false;
            var context = NpcKnowledgeContextBuilder.Build(scenario.npcId, roomId, state, true);
            if (query.Kind == ReasoningQueryKind.Report)
            {
                CompanionPlaytestTrace.Record("evidence_query", roomId, scenario.npcId, outcome: "requested");
                var facts = CompanionDiscovery.PrioritizePending(scenario, state, roomId,
                    context.KnownFacts.Where(f => f.canShareAsEvidence && !f.isPuzzleAnswer && !f.isPuzzleGuidance))
                    .Take(CompanionDiscovery.ReportLimit).ToList();
                reply = Reply(facts.Count == 0 ? scenario.noEvidenceLine :
                    "...สิ่งที่ฉันตรวจสอบได้ตอนนี้:\n" + string.Join("\n", facts.Select(f => f.statement)),
                    facts.Select(f => f.factId).ToArray());
                return true;
            }
            var rules = query.Rules;
            // Ambiguous or unknown hypotheses ask back; never guess the intended rule.
            if (rules.Count != 1) { reply = Reply(profile.clarifyLine); return true; }
            if (context.Npc != null && !context.Npc.givesHints)
            { reply = Reply(context.Npc.refuseHintLine ?? profile.insufficientEvidenceLine); return true; }
            var result = profile.Evaluate(rules[0], new World(context, state));
            CompanionPlaytestTrace.Record("hypothesis", roomId, scenario.npcId, target: rules[0].id, outcome: result.Outcome.ToString());
            reply = Reply(result.Outcome == ReasoningOutcome.Unavailable ? profile.unavailableLine :
                string.IsNullOrWhiteSpace(result.Line) ? profile.insufficientEvidenceLine : result.Line, result.ReferencedFacts);
            return true;
        }
        private static GeneratedChatReply Reply(string line, string[] facts = null) =>
            new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = facts ?? Array.Empty<string>() };
    }
}
