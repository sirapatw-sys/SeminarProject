using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using MysteryGame.Knowledge;

namespace MysteryGame.Companions
{
    // Ephemeral, one-conversation evidence focus. Never a saved fact or AI authority.
    internal sealed class CompanionReadingContext
    {
        internal NpcCompanionController Actor;
        internal GameState State;
        internal int ConversationVersion, TaskRevision, SceneRevision;
        internal string RoomId, FocusId;
        internal bool Pending, Show;
        internal readonly HashSet<string> Candidates = new HashSet<string>();
    }

    /// <summary>Player invitations and literal evidence reading, independent of puzzle-hint rights.</summary>
    public static class CompanionNaturalRequests
    {
        static Regex R(string p) => new Regex(p.Normalize(NormalizationForm.FormKC), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        static string Key(string s) => (s ?? "").Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        static readonly Regex Read = R(@"อ่าน|ดูข้อความ|ถอดข้อความ|เขียนว่าอะไร|\b(?:read|transcribe)\b");
        static readonly Regex Show = R(@"(?:เอา|ส่ง|แบ่งปัน).*(?:ให้.*ดู|ให้เธอ)|ให้.*ดู|ช่วยดู|\b(?:share|show)\b");
        static readonly Regex Puzzle = R(@"รหัส|เฉลย|คำตอบ|ตีความ|แปลความ|หมายความ|ย้อน|ขวา|ซ้าย|วิธี|ยังไง|อย่างไร|\b(?:code|answer|solve|interpret|backwards|reverse|how)\b");
        static readonly Regex ReadFiller = R(@"เขียนว่าอะไร|ดูข้อความ|ถอดข้อความ|ได้(?:หรือ|รึ)เปล่า|ได้ไหม|ได้มั้ย|อันนี้|ชิ้นนี้|นี่|อันนั้น|ชิ้นนั้น|ข้อความ|หลักฐาน|ขอให้|อยากให้|พอจะ|รบกวน|กรุณา|โปรด|ช่วย|อ่าน|เอา|ส่ง|แบ่งปัน|ให้|ฟัง|เธอ|คุณ|เรา|ฉัน|ดู|ด้วย|หน่อย|ที|สิ|นะ|ครับ|ค่ะ|คะ|\b(?:can|could|would|will|you|please|read|transcribe|show|share|me|us|the|this|that|it|evidence|text|out|loud|for|to)\b|[\s.!?…]+");
        static readonly Regex ShortFiller = R(@"อันนี้|ชิ้นนี้|นี่|อันนั้น|ชิ้นนั้น|หน่อย|นะ|ครับ|ค่ะ|คะ|\b(?:the|this|that|one|please)\b|[\s.!?…]+");
        static readonly Regex JoinFast = R(@"^(?:(?:ขอให้|อยากให้|เธอ|คุณ|ช่วย|กรุณา|โปรด|มา|ไป)\s*)*(?:ดู|ตรวจดู|ดูหลักฐาน|สำรวจ)(?:ด้วยกัน|เป็นเพื่อน|กับเรา|กับฉัน)(?:หน่อย|ที|สิ|นะ|ครับ|ค่ะ|คะ|ได้ไหม|ได้มั้ย|ก่อน|\s)*[.!?]*$|^(?:please\s+)?(?:come|look)\s+(?:and\s+)?(?:look\s+)?(?:with\s+me|together)(?:\s+please)?[.!?]*$");
        static readonly Regex JoinMovement = R(@"มา|ไป|เดิน|ตาม|\b(?:come|go|walk|join|accompany)\b");
        static readonly Regex JoinLook = R(@"ดู|สำรวจ|ตรวจ|\b(?:look|inspect|explore)\b");
        static readonly Regex JoinTogether = R(@"ด้วยกัน|เป็นเพื่อน|กับ(?:เรา|ฉัน)|ข้าง\s*ๆ|\b(?:together|with\s+me)\b");
        static readonly Regex JoinFiller = R(@"อยากให้|ขอให้|กรุณา|โปรด|ตอน|อยู่|ข้าง\s*ๆ|ช่วย|มา|ไป|เดิน|ตาม|ตรวจ|สำรวจ|ดู|หลักฐาน|ด้วยกัน|เป็นเพื่อน|กับ|เรา|ฉัน|เธอ|คุณ|หน่อย|ที|สิ|นะ|ครับ|ค่ะ|คะ|ได้ไหม|ได้มั้ย|ก่อน|\b(?:please|can|could|would|will|you|come|go|walk|join|accompany|and|look|inspect|explore|together|with|me|us|evidence)\b|[\s.!?…]+");
        static readonly Regex PoliteTail = R(@"(?:\s*(?:นะ|จ๊ะ|จ้ะ|จ้า|ครับ|ค่ะ|คะ)){1,6}[\s.!?…]*$");
        static readonly Regex Reference = R(@"อันนี้|ชิ้นนี้|ตรงนี้|\b(?:this|here|it)\b");
        const string RequestLead = @"(?:(?:เธอ|คุณ|ช่วย|ขอให้|อยากให้|พอจะ|รบกวน|กรุณา|โปรด|แวะ|please|(?:can|could|would|will)\s+you)\s*){0,6}";
        static readonly Regex DirectRead = R(@"^" + RequestLead + @"(?:อ่าน|ดูข้อความ|ถอดข้อความ|\b(?:read|transcribe)\b)");
        static readonly Regex DirectShow = R(@"^" + RequestLead + @"(?:ส่ง|เอา|แบ่งปัน|ดู|\b(?:show|share)\b)");
        static readonly Regex DirectJoin = R(@"^" + RequestLead + @"(?:มา|ไป|เดิน|ตาม|\b(?:come|go|walk|join|accompany)\b)");
        static readonly Regex JoinLookTarget = R(@"(?:ดู|ตรวจ(?:ดู)?|สำรวจ|\b(?:look|inspect|explore)\b)\s*(?:อันนี้|ชิ้นนี้|ตรงนี้|หลักฐาน|ด้วยกัน|เป็นเพื่อน|กับเรา|กับฉัน|\b(?:this|here|it|evidence|together|with\s+me)\b)");
        // The model may interpret connective prose, never a second action,
        // alternative object, figurative reading, or an unsupported destination.
        static readonly Regex SemanticConflict = R(@"แล้ว|จากนั้น|และ|กับ|หรือ(?!เปล่า)|แทน|อื่น|เปิด|ปิด|หยิบ|จับ|ยก|ค้น|หยุด|ยกเลิก|ตีความ|แปลความ|สรุป|ย่อ|แต่ง|ไม่ออก|ออก(?:ไหม|มั้ย)|ความรู้สึก|อ่านใจ|สีหน้า|อนาคต|\b(?:and|then|or|instead|other|another|different|open|close|hold|lift|search|stop|cancel|interpret|summarize|translate|feelings?|mind|future)\b");
        static readonly Regex ForeignLocation = R(@"ประตู|ลิ้นชัก|โต๊ะ|เก้าอี้|กล่อง|กำแพง|หน้าต่าง|(?:ไป|มา)(?:ที่|ตรง)|(?:ที่|ตรง|แถว)(?!นี้|นี่)|ข้าง(?!\s*ๆ)|\b(?:door|drawer|chair|painting|at|towards|near)\b");
        static readonly Regex OtherEvidenceTarget = R(@"ประตู|ลิ้นชัก|โต๊ะ|เก้าอี้|กล่อง|กำแพง|หน้าต่าง|\b(?:door|drawer|chair|painting)\b");

        static readonly Regex SelfReading = R(@"(?:เรา|ฉัน|ผม)(?:(?:จะ|อยาก)(?!ให้)|กำลัง|เคย|อ่าน)|ให้(?:เรา|ฉัน|ผม)อ่าน|\bi\s+(?:want(?!\s+you)|will|am|was|read)\b");
        static bool Consent(string message) => !SelfReading.IsMatch(Key(message)) && !CompanionDialogueCommands.RulesVeto(message, CompanionCommand.Help);
        static bool IsLocalEvidenceRequest(string message, RoomKnowledgeData room) => room != null && Consent(message) &&
            !Puzzle.IsMatch(Key(message)) && (Read.IsMatch(Key(message)) || Show.IsMatch(Key(message))) &&
            OnlyNames(message, Named(message, room.facts));
        public static bool IsEvidenceRequest(string message, RoomKnowledgeData room)
        {
            if (IsLocalEvidenceRequest(message, room)) return true;
            if (room == null || !Consent(message) || Puzzle.IsMatch(Key(message)) ||
                (!DirectRead.IsMatch(Key(message)) && !DirectShow.IsMatch(Key(message)))) return false;
            var named = Named(message, room.facts);
            return (named.Count > 0 || Reference.IsMatch(Key(message))) &&
                !SemanticConflict.IsMatch(JoinTogether.Replace(WithoutNames(message, named), " ")) && !OtherEvidenceTarget.IsMatch(WithoutNames(message, named));
        }
        public static bool IsJoinRequest(string message) => Consent(message) && JoinMovement.IsMatch(Key(message)) &&
            JoinLook.IsMatch(Key(message)) && (JoinTogether.IsMatch(Key(message)) || Reference.IsMatch(Key(message))) &&
            JoinFiller.Replace(Reference.Replace(PoliteTail.Replace(Key(message), ""), ""), "").Length == 0;
        public static bool CanProposeJoin(string message, RoomKnowledgeData room)
        {
            string text = Key(message);
            if (!Consent(message) || !DirectJoin.IsMatch(text) || !JoinLookTarget.IsMatch(text) ||
                !(JoinTogether.IsMatch(text) || Reference.IsMatch(text)) || Puzzle.IsMatch(text) ||
                SemanticConflict.IsMatch(JoinTogether.Replace(text, " ")) || ForeignLocation.IsMatch(text)) return false;
            // Named scene objects belong to their authored activity, not a
            // generic accompany-player action. Never substitute destinations.
            return room == null || room.gameplayTerms == null || !room.gameplayTerms.Any(term =>
                !string.IsNullOrWhiteSpace(term) && Regex.IsMatch(text, NamePattern(Key(term))));
        }

        static GeneratedChatReply Reply(string line, params string[] facts) => new GeneratedChatReply {
            reply = line, relationshipDelta = 0, referencedFactIds = facts ?? Array.Empty<string>() };
        static bool Valid(DialogueManager d, NpcCompanionController a, CompanionReadingContext c) => c != null &&
            c.Actor == a && c.State == GameState.Instance && c.ConversationVersion == d.ConversationVersion &&
            c.SceneRevision == GameState.Instance.SceneRevision &&
            c.TaskRevision == a.TaskRevision && c.RoomId == a.RoomId && a.RoomId == GameState.Instance.GetCurrentScene();
        static CompanionReadingContext NewContext(DialogueManager d, NpcCompanionController a) => new CompanionReadingContext {
            Actor = a, State = GameState.Instance, ConversationVersion = d.ConversationVersion,
            TaskRevision = a.TaskRevision, SceneRevision = GameState.Instance.SceneRevision, RoomId = a.RoomId };
        static IEnumerable<string> Names(RoomFact f) => new[] { f.factId, f.EvidenceTitle }.Concat(f.evidenceAliases ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n)).Select(Key).Distinct();
        static string NamePattern(string n) =>
            (n.Any(ch => ch >= 'ก' && ch <= '๛') ? "" : @"(?<![\p{L}\p{N}_])") + Regex.Escape(n) +
            (n.Any(ch => ch >= 'ก' && ch <= '๛') ? "" : @"(?![\p{L}\p{N}_])");
        static List<RoomFact> Named(string message, IEnumerable<RoomFact> facts)
        {
            string remaining = Key(message);
            var result = new List<RoomFact>();
            // A complete title wins over a shorter alias embedded inside it.
            foreach (var group in facts.Where(f => f != null).SelectMany(f => Names(f).Select(n => new { Fact = f, Name = n }))
                .GroupBy(p => p.Name).OrderByDescending(g => g.Key.Length))
            {
                string pattern = NamePattern(group.Key);
                if (!Regex.IsMatch(remaining, pattern)) continue;
                foreach (var pair in group)
                    if (!result.Contains(pair.Fact)) result.Add(pair.Fact);
                remaining = Regex.Replace(remaining, pattern, " ");
            }
            return result;
        }

        public static void RememberShared(DialogueManager d, string factId)
        {
            if (d == null) return;
            var a = NpcCompanionController.Find(d.ActiveNpcId, GameState.Instance?.GetCurrentScene());
            if (a == null || !a.CanConverse(d) || !EvidenceSharing.Available(GameState.Instance, d.ActiveNpcId).Any(f => f.factId == factId)) return;
            d.ReadingContext = NewContext(d, a); d.ReadingContext.FocusId = factId;
        }

        public static bool TryReply(DialogueManager d, NpcCompanionController a, string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (d == null) return false;
            if (a == null || !a.CanConverse(d)) { d.ReadingContext = null; return false; }
            var old = Valid(d, a, d.ReadingContext) ? d.ReadingContext : null;
            d.ReadingContext = null; // Next unrelated turn, refusal or task change expires the focus/question.
            if ((Consent(message) && JoinFast.IsMatch(PoliteTail.Replace(Key(message), ""))) || IsJoinRequest(message))
                return Join(d, a, out reply); // Verified requests never depend on provider health.
            var room = KnowledgeLibrary.GetRoom(a.RoomId);
            if (CanProposeJoin(message, room)) return false;
            var available = EvidenceSharing.Available(GameState.Instance, a.Scenario.npcId);
            if (old != null && old.Pending && Consent(message))
            {
                var answers = Named(message, available.Where(f => old.Candidates.Contains(f.factId)));
                if (answers.Count == 1 && OnlyNames(message, answers, true))
                    return ReadEvidence(d, a, answers[0], old.Show, out reply);
            }
            if (IsLocalEvidenceRequest(message, room))
                return Resolve(d, a, message, Show.IsMatch(Key(message)) && !Read.IsMatch(Key(message)), old, null, out reply);
            if (IsEvidenceRequest(message, room))
            {
                // Preserve only a verified previous focus during this request;
                // a pending short-answer authorization is still consumed now.
                if (old != null && old.FocusId != null)
                { d.ReadingContext = NewContext(d, a); d.ReadingContext.FocusId = old.FocusId; }
                return false;
            }
            // A mention sets focus, not knowledge. Only the very next turn may refer to it.
            if (Consent(message))
            {
                var named = Named(message, available);
                if (named.Count == 1) { d.ReadingContext = NewContext(d, a); d.ReadingContext.FocusId = named[0].factId; }
            }
            return false;
        }

        static string WithoutNames(string message, List<RoomFact> facts)
        {
            string remaining = Key(message);
            foreach (string name in facts.SelectMany(Names).Distinct().OrderByDescending(n => n.Length))
                remaining = remaining.Replace(name, " ");
            return remaining;
        }
        static bool OnlyNames(string message, List<RoomFact> facts, bool shortAnswer = false) =>
            (shortAnswer ? ShortFiller : ReadFiller).Replace(PoliteTail.Replace(WithoutNames(message, facts), ""), "").Length == 0;
        static bool Resolve(DialogueManager d, NpcCompanionController a, string message, bool show,
            CompanionReadingContext old, string proposedId, out GeneratedChatReply reply, bool semantic = false)
        {
            var available = EvidenceSharing.Available(GameState.Instance, a.Scenario.npcId);
            var room = KnowledgeLibrary.GetRoom(a.RoomId);
            var named = Named(message, room.facts);
            // Named but unseen/forbidden evidence is never replaced by an available one.
            if (named.Any(f => !available.Contains(f)) || !semantic && !OnlyNames(message, named))
            { reply = Reply("ฉันยังอ่านสิ่งนั้นให้ไม่ได้ ส่งชื่อหลักฐานที่เธอค้นพบแล้วให้ชัดเจนก่อนนะ"); return true; }
            var candidates = named.Count > 0 ? named : old != null && old.FocusId != null
                ? available.Where(f => f.factId == old.FocusId).ToList() : available;
            if (candidates.Count == 1 && proposedId != null && candidates[0].factId != proposedId)
            { reply = null; return false; }
            if (candidates.Count == 1 && (proposedId == null || candidates[0].factId == proposedId))
                return ReadEvidence(d, a, candidates[0], show, out reply);
            var pending = NewContext(d, a); pending.Pending = true; pending.Show = show;
            foreach (var f in candidates) pending.Candidates.Add(f.factId);
            if (pending.Candidates.Count > 0) d.ReadingContext = pending;
            reply = Reply(candidates.Count == 0 ? "ตอนนี้ยังไม่มีหลักฐานที่เธอค้นพบและนำมาให้ฉันอ่านได้ ลองสำรวจด้วยกันก่อนนะ" :
                "หมายถึงหลักฐานชิ้นไหน บอกชื่อให้ฉันหน่อยนะ\n" + string.Join(" / ", candidates.Select(f => f.EvidenceTitle)));
            return true;
        }
        static bool ReadEvidence(DialogueManager d, NpcCompanionController a, RoomFact fact, bool show, out GeneratedChatReply reply)
        {
            reply = null;
            EvidenceShareResult result;
            if (!a.CanConverse(d) || !EvidenceSharing.TryShare(GameState.Instance, a.Scenario.npcId, fact.factId, out result, false)) return false;
            RememberShared(d, fact.factId);
            if (result.JournalEntryAdded) JournalUI.NotifyNewEntry();
            reply = Reply(show ? result.Reply : "ได้ ฉันอ่านตามข้อความที่เห็นนะ ยังไม่สรุปคำตอบของปริศนา\n\n" + result.Fact.statement, fact.factId);
            return true;
        }
        static bool Join(DialogueManager d, NpcCompanionController a, out GeneratedChatReply reply)
        {
            reply = null; string line;
            if (!a.ApplyDialogueCommand(d, CompanionCommand.Follow, null, out line)) return false;
            d.ReadingContext = null;
            reply = Reply("ได้ ฉันจะตามไปดูด้วยกัน พอเราคุยกันเสร็จ เธอนำทางได้เลย ถ้ามีหลักฐานที่อยากให้ฉันอ่านก็บอกชื่อได้");
            return true;
        }
        public static bool TryApplyProposal(DialogueManager d, NpcCompanionController a, string message, string action, out GeneratedChatReply reply)
        {
            reply = null;
            if (string.IsNullOrWhiteSpace(action) || a == null || !a.CanConverse(d)) return false;
            var room = KnowledgeLibrary.GetRoom(a.RoomId);
            if ((action == CompanionActionOption.InspectTogether || action == CompanionActionOption.Follow) &&
                (IsJoinRequest(message) || CanProposeJoin(message, room))) return Join(d, a, out reply);
            bool show = action.StartsWith(CompanionActionOption.ShowPrefix, StringComparison.Ordinal);
            if ((!show && !action.StartsWith(CompanionActionOption.ReadPrefix, StringComparison.Ordinal)) ||
                !IsEvidenceRequest(message, room)) return false;
            string id = action.Substring((show ? CompanionActionOption.ShowPrefix : CompanionActionOption.ReadPrefix).Length);
            // The proposal may not change the player's requested operation or target.
            if (show != (Show.IsMatch(Key(message)) && !Read.IsMatch(Key(message))) ||
                !EvidenceSharing.Available(GameState.Instance, a.Scenario.npcId).Any(f => f.factId == id)) return false;
            return Resolve(d, a, message, show, Valid(d, a, d.ReadingContext) ? d.ReadingContext : null, id, out reply, true);
        }
    }
}
