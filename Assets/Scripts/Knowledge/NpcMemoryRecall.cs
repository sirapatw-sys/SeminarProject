using System;
using System.Collections.Generic;
using System.Linq;
using MysteryGame.Companions;

namespace MysteryGame.Knowledge
{
    // Retrieves existing verified notes; never creates memories from player prose.
    public static class NpcMemoryRecall
    {
        public static List<string> Select(IReadOnlyList<string> memories, string message,
            CompanionScenarioData scenario, int limit = 4)
        {
            var preferred = new HashSet<string>(StringComparer.Ordinal);
            Func<string, bool> mentions = term => !string.IsNullOrWhiteSpace(term) &&
                (message ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
            if (scenario != null)
            {
                if (scenario.observations != null) foreach (var observation in scenario.observations)
                    if (observation != null && observation.recallTerms != null && observation.recallTerms.Any(mentions))
                        preferred.Add(observation.memory ?? "");
                if (scenario.stations != null) foreach (var station in scenario.stations)
                    if (station != null && (mentions(station.title) ||
                        (station.gameplayTerms != null && station.gameplayTerms.Any(mentions))))
                        preferred.Add(station.memory ?? "");
            }
            var selected = new List<string>();
            if (memories == null || limit <= 0) return selected;
            int budget = 1400;
            for (int pass = 0; pass < 2; pass++)
                for (int i = memories.Count - 1; i >= 0 && selected.Count < limit; i--)
                {
                    string memory = memories[i];
                    if (string.IsNullOrWhiteSpace(memory) || memory.StartsWith(NpcKnowledgeContextBuilder.PlayerSaidPrefix) ||
                        selected.Contains(memory) || preferred.Contains(memory) != (pass == 0) || memory.Length > budget) continue;
                    selected.Add(memory); budget -= memory.Length;
                }
            return selected;
        }
    }
}
