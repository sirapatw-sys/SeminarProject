using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using MysteryGame.Knowledge;

namespace MysteryGame.Companions
{
    // Authored social actions: called only from the trusted player-send path.
    public static class CompanionConversation
    {
        private static Regex R(string pattern) { return new Regex(pattern.Normalize(NormalizationForm.FormKC), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }
        private static string Key(string value) { return (value ?? "").Normalize(NormalizationForm.FormKC); }
        private static readonly Regex Share = R(@"^(?:ขอ)?(?:เอา|ส่ง|แบ่งปัน)\s*(.+?)\s*(?:ให้(?:เธอ)?ดู|ให้เธอ)(?:หน่อย|นะ|ครับ|ค่ะ|คะ)*[.!?]*$|^share\s+(.+?)(?:\s+please)?[.!?]*$");
        private static readonly Regex Discuss = R(@"^(?:ช่วยคิดด้วยกัน|มาคิดด้วยกัน|ทบทวนหลักฐาน|หลักฐานเกี่ยวกันไหม|let's think together|review the evidence)(?:หน่อย|นะ|ครับ|ค่ะ|คะ)*[.!?]*$");
        private static readonly Regex Invite = R(@"^(?:มีอะไร(?:อยากบอก)?ไหม|อยากคุยอะไร|what is it)(?:นะ|ครับ|ค่ะ|คะ)*[.!?]*$");
        private static readonly Regex Recall = R(@"^(?:ยัง)?จำ(.+?)ได้ไหม(?:นะ|ครับ|ค่ะ|คะ)*[.!?]*$|^remember\s+(.+?)\??$");

        public static bool TryReply(NpcCompanionController actor, string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (actor == null || !actor.CanConverse(DialogueManager.Instance)) return false;
            var state = GameState.Instance;
            if (CompanionReflection.TryReply(actor.Scenario, state, message, out reply)) return true;
            if (CompanionReasoning.TryReply(actor.Scenario, state, actor.RoomId, message, out reply)) return true;
            var share = Share.Match(message.Trim());
            string line = null;
            if (share.Success)
            {
                string target = (share.Groups[1].Success ? share.Groups[1].Value : share.Groups[2].Value).Trim();
                var candidates = EvidenceSharing.Available(state, actor.Scenario.npcId)
                    .Where(f => string.Equals(Key(f.EvidenceTitle), target, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.factId, target, StringComparison.OrdinalIgnoreCase) ||
                        (f.evidenceAliases != null && f.evidenceAliases.Any(a => !string.IsNullOrWhiteSpace(a) &&
                            string.Equals(Key(a), target, StringComparison.OrdinalIgnoreCase)))).ToList();
                EvidenceShareResult result;
                if (candidates.Count == 1 && EvidenceSharing.TryShare(state, actor.Scenario.npcId, candidates[0].factId, out result, false))
                    line = result.Reply;
                else line = "ฉันยังไม่แน่ใจว่าหมายถึงหลักฐานชิ้นไหน ใช้ชื่อหลักฐานที่ค้นพบในสมุดแล้วบอกว่าจะเอาให้ดูนะ";
            }
            else if (Discuss.IsMatch(message.Trim()))
            {
                var context = NpcKnowledgeContextBuilder.Build(actor.Scenario.npcId, actor.RoomId, state, false);
                var known = context.KnownFacts.Select(f => f.factId).ToList();
                if (actor.Scenario.discussions != null)
                    foreach (var discussion in actor.Scenario.discussions)
                        if (discussion != null && discussion.requiredFacts != null && discussion.requiredFacts.Count > 0 &&
                            discussion.requiredFacts.All(known.Contains) && ConditionRule.AllHold(discussion.conditions, state))
                        { line = discussion.line; break; }
                if (string.IsNullOrWhiteSpace(line))
                    line = "...ฉันยังมีข้อมูลไม่พอจะเชื่อมเรื่องนี้ เอาหลักฐานที่เธอค้นพบมาให้ดูได้ แล้วเราค่อยแยกสิ่งที่รู้จริงออกจากสิ่งที่เดากัน";
            }
            else if (Recall.IsMatch(message.Trim()))
            {
                var match = Recall.Match(message.Trim());
                string topic = (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).Trim();
                var memories = state.GetNpcMemory(actor.Scenario.npcId);
                foreach (var station in actor.Scenario.stations)
                    if ((topic == Key(station.title) || (station.gameplayTerms != null && station.gameplayTerms.Any(t => Key(t) == topic))) &&
                        (state.HasFlag(actor.Scenario.CompletedFlag(station)) || memories.Contains(station.memory)))
                    { line = "จำได้... " + station.memory; break; }
                if (line == null && actor.Scenario.observations != null) foreach (var observation in actor.Scenario.observations)
                    if (observation != null && observation.recallTerms != null && observation.recallTerms.Any(t => Key(t) == topic) &&
                        (state.HasFlag(actor.Scenario.Flag("observed." + observation.id)) || memories.Contains(observation.memory)))
                    { line = "จำได้... " + observation.memory; break; }
                if (line == null) line = "ฉันยังไม่มีความทรงจำที่ยืนยันเรื่องนั้น ไม่อยากทำเป็นจำได้ทั้งที่ไม่แน่ใจ";
            }
            else if (Invite.IsMatch(message.Trim())) line = actor.ReadInvitation();
            if (string.IsNullOrWhiteSpace(line)) return false;
            reply = new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }
    }
}
