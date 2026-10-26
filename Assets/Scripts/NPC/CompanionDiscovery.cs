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
            var profile = KnowledgeLibrary.GetNpc(scenario.npcId);
            if (room.facts != null) foreach (var fact in room.facts)
                if (CanReport(scenario, state, roomId, profile, fact)) result.Add(fact);
            return result;
        }
        public static bool HasPending(CompanionScenarioData scenario, GameState state, string roomId)
        {
            if (scenario == null || state == null || state.GetCurrentScene() != roomId) return false;
            var room = KnowledgeLibrary.GetRoom(roomId);
            if (room == null || room.companionScenario != scenario || room.facts == null) return false;
            var profile = KnowledgeLibrary.GetNpc(scenario.npcId);
            foreach (var fact in room.facts) if (CanReport(scenario, state, roomId, profile, fact)) return true;
            return false;
        }
        static bool CanReport(CompanionScenarioData scenario, GameState state, string roomId, NpcProfileData profile, RoomFact fact) =>
            fact != null && !string.IsNullOrWhiteSpace(fact.factId) && fact.canShareAsEvidence && !fact.isPuzzleAnswer && !fact.isPuzzleGuidance &&
            IsSource(scenario, fact.factId) && fact.IsRevealed(state) && (profile == null || !profile.IsForbidden(fact.factId)) &&
            NpcObservation.Knows(state, scenario.npcId, roomId, fact.factId) && !state.HasFlag(ReportedFlag(scenario, fact.factId));
        static bool IsSource(CompanionScenarioData scenario, string id)
        {
            if (scenario.worldTargets != null) foreach (var target in scenario.worldTargets)
                if (target?.discoveredFactIds != null && target.discoveredFactIds.Contains(id)) return true;
            if (scenario.searchSpots != null) foreach (var spot in scenario.searchSpots)
                if (spot != null && spot.factId == id) return true;
            if (scenario.stations != null) foreach (var station in scenario.stations)
                if (station?.discoveredFactIds != null && station.discoveredFactIds.Contains(id)) return true;
            return false;
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
            {
                state.SetFlag(ReportedFlag(scenario, id)); count++;
                CompanionPlaytestTrace.Record("report", roomId, scenario.npcId, target: id, outcome: "shown");
            }
            return count;
        }

        public static string ReportText(CompanionScenarioData scenario, IEnumerable<RoomFact> facts)
        {
            return (string.IsNullOrWhiteSpace(scenario.discoveryReportLine) ? "ฉันพบหลักฐานที่ตรวจสอบได้:" : scenario.discoveryReportLine) +
                "\n" + string.Join("\n", facts.Select(f => f.statement));
        }
    }
}
