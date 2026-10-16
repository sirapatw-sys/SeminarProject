using System;
using System.Collections.Generic;
using System.Linq;
using MysteryGame.Core;
using MysteryGame.Knowledge;

namespace MysteryGame.Companions
{
    // A read receipt is not evidence. Only current, permitted observations can
    // become a discovery invitation; prose and player-only knowledge cannot.
    public static class CompanionDiscovery
    {
        public const int ReportLimit = 3;
        public static string ReportedFlag(CompanionScenarioData scenario, string factId)
        { return scenario.Flag("reported." + Uri.EscapeDataString(factId ?? "").Replace(".", "%2E")); }

        public static List<RoomFact> Pending(CompanionScenarioData scenario, GameState state, string roomId)
        {
            var result = new List<RoomFact>();
            if (scenario == null || state == null || state.GetCurrentScene() != roomId) return result;
            var room = KnowledgeLibrary.GetRoom(roomId);
            if (room == null || room.companionScenario != scenario) return result;
            var sources = new HashSet<string>(StringComparer.Ordinal);
            if (scenario.searchSpots != null) foreach (var spot in scenario.searchSpots)
                if (spot != null && !string.IsNullOrWhiteSpace(spot.factId)) sources.Add(spot.factId);
            if (scenario.stations != null) foreach (var station in scenario.stations)
                if (station?.discoveredFactIds != null) foreach (string id in station.discoveredFactIds)
                    if (!string.IsNullOrWhiteSpace(id)) sources.Add(id);
            var context = NpcKnowledgeContextBuilder.Build(scenario.npcId, roomId, state, false);
            foreach (var fact in context.KnownFacts)
                if (fact != null && sources.Contains(fact.factId) && fact.canShareAsEvidence &&
                    !fact.isPuzzleAnswer && !fact.isPuzzleGuidance &&
                    NpcObservation.Knows(state, scenario.npcId, roomId, fact.factId) &&
                    !state.HasFlag(ReportedFlag(scenario, fact.factId))) result.Add(fact);
            return result;
        }

        public static IEnumerable<RoomFact> PrioritizePending(CompanionScenarioData scenario, GameState state,
            string roomId, IEnumerable<RoomFact> facts)
        {
            var pending = new HashSet<string>(Pending(scenario, state, roomId).Select(f => f.factId), StringComparer.Ordinal);
            return facts.OrderByDescending(f => pending.Contains(f.factId));
        }

        // Call only after a verified player-triggered report, never from a
        // prompt builder, model proposal, timer, or hypothesis evaluation.
        public static int Acknowledge(CompanionScenarioData scenario, GameState state, string roomId, IEnumerable<string> ids)
        {
            if (ids == null) return 0;
            var allowed = new HashSet<string>(Pending(scenario, state, roomId).Select(f => f.factId), StringComparer.Ordinal);
            int count = 0;
            foreach (string id in ids.Distinct()) if (allowed.Contains(id))
            { state.SetFlag(ReportedFlag(scenario, id)); count++; }
            return count;
        }

        public static string ReportText(CompanionScenarioData scenario, IEnumerable<RoomFact> facts)
        {
            return (string.IsNullOrWhiteSpace(scenario.discoveryReportLine) ? "ฉันพบหลักฐานที่ตรวจสอบได้:" : scenario.discoveryReportLine) +
                "\n" + string.Join("\n", facts.Select(f => f.statement));
        }
    }
}
