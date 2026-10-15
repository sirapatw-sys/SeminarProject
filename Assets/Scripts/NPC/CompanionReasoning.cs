using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CompanionFramework;
using MysteryGame.Core;
using MysteryGame.Knowledge;

namespace MysteryGame.Companions
{
    // Unity/game adapter. Only CompanionConversation's verified player-send path
    // invokes this in gameplay; evaluating a hypothesis never solves a puzzle.
    public static class CompanionReasoning
    {
        private static readonly Regex Report = new Regex(
            @"^(?:เธอเจออะไรบ้าง|เธอพบอะไรบ้าง|เจออะไรไหม|เธอรู้อะไรบ้าง|what did you find|what do you know)[?.!]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex Hypothesis = new Regex(
            @"^(?:เราคิดว่า|ฉันคิดว่า|คิดว่า|ถ้า|i think\s+|maybe\s+)(.+?)(?:\s*(?:เธอว่าไง|เธอคิดว่าไง|ใช่ไหม|ดีไหม|what do you think))?[?.!]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
            string text = message.Normalize(NormalizationForm.FormKC).Trim();
            var context = NpcKnowledgeContextBuilder.Build(scenario.npcId, roomId, state, true);
            if (Report.IsMatch(text))
            {
                var facts = context.KnownFacts.Where(f => f.canShareAsEvidence && !f.isPuzzleAnswer && !f.isPuzzleGuidance).Take(3).ToList();
                reply = Reply(facts.Count == 0 ? scenario.noEvidenceLine :
                    "...สิ่งที่ฉันตรวจสอบได้ตอนนี้:\n" + string.Join("\n", facts.Select(f => f.statement)),
                    facts.Select(f => f.factId).ToArray());
                return true;
            }
            var match = Hypothesis.Match(text);
            if (!match.Success) return false;
            string topic = match.Groups[1].Value.Trim().TrimEnd('?', '.', '!', ' ');
            var profile = scenario.reasoningProfile;
            if (profile.rules == null) { reply = Reply(profile.insufficientEvidenceLine); return true; }
            var rules = profile.rules.Where(r => r != null && r.phrases != null && r.phrases.Any(p =>
                !string.IsNullOrWhiteSpace(p) && string.Equals(p.Normalize(NormalizationForm.FormKC).Trim(), topic, StringComparison.OrdinalIgnoreCase))).ToList();
            // Ambiguous or unknown hypotheses ask back; never guess the intended rule.
            if (rules.Count != 1) { reply = Reply(profile.clarifyLine); return true; }
            if (context.Npc != null && !context.Npc.givesHints)
            { reply = Reply(context.Npc.refuseHintLine ?? profile.insufficientEvidenceLine); return true; }
            var result = ReasoningEngine.Evaluate(rules[0], new World(context, state));
            reply = Reply(string.IsNullOrWhiteSpace(result.Line) ? profile.insufficientEvidenceLine : result.Line, result.ReferencedFacts);
            return true;
        }
        private static GeneratedChatReply Reply(string line, string[] facts = null) =>
            new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = facts ?? Array.Empty<string>() };
    }
}
