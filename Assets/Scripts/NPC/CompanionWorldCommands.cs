using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using MysteryGame.Knowledge;

namespace MysteryGame.Companions
{
    public sealed class CompanionWorldSession
    {
        public string FocusId;
        public List<string> Candidates;
        public CompanionWorldVerb PendingVerb;
        public string PendingRelation;
        internal GameState State;
        internal string Room;
        internal int SceneRevision, TaskRevision;
        public void Clear() { FocusId = null; Candidates = null; State = null; }
        internal bool Valid(NpcCompanionController actor) => State == GameState.Instance && State != null &&
            Room == actor.RoomId && SceneRevision == State.SceneRevision && TaskRevision == actor.TaskRevision;
        internal void Remember(NpcCompanionController actor)
        { State = GameState.Instance; Room = actor.RoomId; SceneRevision = State.SceneRevision; TaskRevision = actor.TaskRevision; }
    }

    public static partial class CompanionWorldCommands
    {
        static readonly Regex Move = new Regex(@"เดิน|ยืน|ไปที่|ไปตรง|ไปข้าง|ไปอยู่|ขยับ|move|walk|go\s+to|stand", RegexOptions.IgnoreCase);
        static readonly Regex Inspect = new Regex(@"ตรวจ|สำรวจ|เช็ค|เช็ก|ค้น|ดู|สังเกต|inspect|check|look|examine|search", RegexOptions.IgnoreCase);
        static readonly Regex Open = new Regex(@"เปิด|open|unlock", RegexOptions.IgnoreCase);
        static readonly Regex Request = new Regex(@"^(?:ช่วย|ลอง|ขอ|อยากให้|รบกวน|เธอ|คุณ|เดิน|ไป|มา|ตรวจ|สำรวจ|เช็ค|เช็ก|ดู|เปิด|ขยับ|ยืน|please\b|can\s+you\b|could\s+you\b|would\s+you\b|go\b|walk\b|move\b|inspect\b|check\b|look\b|open\b)", RegexOptions.IgnoreCase);
        static readonly Regex Reference = new Regex(@"อันนี้|อันนั้น|ตรงนั้น|ตรงนี้|ตัวนั้น|ตัวเดิม|มัน|ที่เดิม|\b(it|that|there|same)\b", RegexOptions.IgnoreCase);
        static readonly Regex MultiAction = new Regex(@"แล้ว.*(?:เปิด|ตรวจ|เดิน|ไป)|จากนั้น|;|\bthen\b|\band\s+(open|inspect|go|walk)\b", RegexOptions.IgnoreCase);
        static readonly Regex Stop = new Regex(@"^(?:หยุด(?:ก่อน|เดิน|เลย)?|พอแล้ว|ยกเลิก|ไม่ต้อง(?:ทำ|ตาม|เดิน)(?:แล้ว)?|รอ(?:ก่อน|ตรงนี้)?|stop|cancel|wait)(?:\s|นะ|หน่อย|ครับ|ค่ะ|คะ|ได้แล้ว|$)", RegexOptions.IgnoreCase);
        public static bool IsStop(string text) => !CompanionDialogueCommands.RulesVeto(text, CompanionCommand.Wait) && Stop.IsMatch((text ?? "").Trim());
        static string Normalize(string text, CompanionScenarioData scenario)
        {
            string value = (text ?? "").Trim();
            foreach (string name in scenario?.addressNames ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(name)) value = Regex.Replace(value, "^" + Regex.Escape(name) + @"(?:\s+|[,，]\s*|(?=ช่วย|ลอง|เดิน|ไป|ตรวจ|เปิด))", "", RegexOptions.IgnoreCase);
            return Regex.Replace(value, @"^ไม่ใช่(?:อันนั้น|ตัวนั้น|ตรงนั้น)[ ,，]*", "");
        }
        public static bool IsRequest(string text, string room)
        {
            text = Normalize(text, KnowledgeLibrary.GetRoom(room)?.companionScenario);
            if (string.IsNullOrWhiteSpace(text) || CompanionDialogueCommands.RulesVeto(text, CompanionCommand.Follow) || !Request.IsMatch(text.Trim())) return false;
            var named = Named(text, RoomWorldRegistry.Available(room, GameState.Instance));
            string instruction = StripNames(text, named);
            return (Move.IsMatch(instruction) || Inspect.IsMatch(instruction) || Open.IsMatch(instruction)) &&
                (named.Count > 0 || Reference.IsMatch(text));
        }
        static IEnumerable<string> Names(CompanionWorldTarget target) => new[] { target.Definition.id, target.Definition.title }
            .Concat(target.Definition.aliases ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n));
        static string NamePattern(string name) => name.Any(c => c >= 'ก' && c <= '๛') ? Regex.Escape(name) :
            @"(?<![\p{L}\p{N}_])" + Regex.Escape(name) + @"(?![\p{L}\p{N}_])";
        static string StripNames(string text, IEnumerable<CompanionWorldTarget> targets)
        {
            foreach (string name in targets.SelectMany(Names).Distinct().OrderByDescending(n => n.Length))
                text = Regex.Replace(text, NamePattern(name), " ", RegexOptions.IgnoreCase);
            return text;
        }
        static bool OnlyTargetAnswer(string text, CompanionWorldTarget target)
        {
            string remaining = StripNames(text, new[] { target });
            return Regex.Replace(remaining, @"หมายถึง|เอา|ตัว|อัน|ตรง|ชิ้น|นี้|นั้น|นี่|นั่น|นะ|ครับ|ค่ะ|คะ|จ๊ะ|จ้ะ|\b(?:the|this|that|one|please)\b|[\s.!?…]+", "", RegexOptions.IgnoreCase).Length == 0;
        }
        static List<CompanionWorldTarget> Named(string text, IEnumerable<CompanionWorldTarget> targets)
        {
            // Longest non-overlapping names: 'โต๊ะข้างโซฟา' is one object, not desk + sofa.
            // Identical aliases on different objects deliberately stay ambiguous.
            var matches = new List<Tuple<CompanionWorldTarget, int, int>>();
            foreach (var target in targets) foreach (string name in Names(target))
                foreach (Match match in Regex.Matches(text ?? "", NamePattern(name), RegexOptions.IgnoreCase))
                    matches.Add(Tuple.Create(target, match.Index, match.Length));
            var selected = new List<Tuple<CompanionWorldTarget, int, int>>();
            foreach (var match in matches.OrderByDescending(m => m.Item3))
                if (!selected.Any(s => s.Item3 > match.Item3 && s.Item2 < match.Item2 + match.Item3 && match.Item2 < s.Item2 + s.Item3)) selected.Add(match);
            return selected.OrderBy(s => s.Item2).Select(s => s.Item1).Distinct().ToList();
        }
        // Object/area names are removed first: 'bottom left' names a place,
        // whereas 'left of the desk' requests an offset from that place.
        static string Relation(string instruction)
        {
            bool left = Regex.IsMatch(instruction, @"ทางซ้าย|ด้านซ้าย|ข้างซ้าย|\bleft\b", RegexOptions.IgnoreCase);
            bool right = Regex.IsMatch(instruction, @"ทางขวา|ด้านขวา|ข้างขวา|\bright\b", RegexOptions.IgnoreCase);
            bool front = Regex.IsMatch(instruction, @"ข้างหน้า|ด้านหน้า|\bin\s+front\b", RegexOptions.IgnoreCase);
            bool behind = Regex.IsMatch(instruction, @"ด้านหลัง|ข้างหลัง|ไปหลัง|\bbehind\b", RegexOptions.IgnoreCase);
            bool near = Regex.IsMatch(instruction, @"ใกล้|ข้าง\s*ๆ|\b(?:near|beside|next\s+to)\b", RegexOptions.IgnoreCase);
            bool between = Regex.IsMatch(instruction, @"ระหว่าง|\bbetween\b", RegexOptions.IgnoreCase);
            if ((left ? 1 : 0) + (right ? 1 : 0) + (front ? 1 : 0) + (behind ? 1 : 0) + (between ? 1 : 0) > 1 ||
                Regex.IsMatch(instruction, @"ข้างบน|ด้านบน|ข้างใต้|ด้านล่าง|\b(?:above|below|under)\b", RegexOptions.IgnoreCase)) return null;
            return between ? "between" : left ? "left" : right ? "right" : front ? "front" : behind ? "behind" : near ? "near" : "at";
        }
        static CompanionWorldVerb Verb(string text) => Open.IsMatch(text) ? CompanionWorldVerb.Open :
            Inspect.IsMatch(text) ? CompanionWorldVerb.Inspect : CompanionWorldVerb.Move;
        public static string Id(CompanionWorldVerb verb, string target, string relation = "at") =>
            "world:" + verb.ToString().ToLowerInvariant() + ":" + relation + ":" + target;

        public static void FillActions(NpcKnowledgeContext context)
        {
            foreach (var target in RoomWorldRegistry.Available(context.Room.roomId, GameState.Instance))
            {
                var bounds = target.SpatialBounds;
                string location = " [ตำแหน่งบนจอ " + target.ApproachPoint.x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "," +
                    target.ApproachPoint.y.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "; ขอบเขต " +
                    bounds.min.ToString("F1") + " ถึง " + bounds.max.ToString("F1") + "; ชื่ออื่น " + string.Join(", ", target.Definition.aliases ?? new List<string>()) + "]";
                foreach (string relation in new[] { "at", "left", "right", "front", "behind", "near" })
                    context.CompanionActions.Add(new CompanionActionOption { Id = Id(CompanionWorldVerb.Move, target.Definition.id, relation),
                        Description = "เดินไป " + relation + " ของ «" + target.Definition.title + "»" + (relation == "at" ? " " + target.Definition.visibleDescription + location : "") });
                foreach (var verb in new[] { CompanionWorldVerb.Inspect, CompanionWorldVerb.Open })
                    if (target.Allows(verb)) context.CompanionActions.Add(new CompanionActionOption {
                        Id = Id(verb, target.Definition.id), Description = (verb == CompanionWorldVerb.Open ? "ลองเปิด " : "เดินไปตรวจ ") + target.Definition.title +
                            " (ต้องไปถึงและผ่านเงื่อนไขจริง จึงรู้ผล)" + location });
            }
        }

        public static bool TryLocal(NpcCompanionController actor, CompanionWorldSession session, string text, out GeneratedChatReply reply)
        {
            reply = null;
            if (actor == null || session == null) return false;
            text = Normalize(text, actor.Scenario);
            if (!session.Valid(actor)) session.Clear();
            if (IsStop(text))
            {
                session.Clear();
                bool stopped = actor.CanReceiveRoomCommand ? actor.ApplyRoomCommand(CompanionCommand.Wait) :
                    actor.ApplyDialogueCommand(DialogueManager.Instance, CompanionCommand.Wait, null, out _);
                if (!stopped) return false;
                reply = Reply(actor.Bubble); return true;
            }
            var targets = RoomWorldRegistry.Available(actor.RoomId, GameState.Instance);
            var named = Named(text, targets);
            if (session.Candidates != null)
            {
                var answers = named.Where(t => session.Candidates.Contains(t.Definition.id)).ToList();
                if (answers.Count == 1 && OnlyTargetAnswer(text, answers[0]) &&
                    !CompanionDialogueCommands.RulesVeto(text, CompanionCommand.Follow))
                {
                    var verb = session.PendingVerb; string relation = session.PendingRelation;
                    session.Candidates = null;
                    return Dispatch(actor, session, answers[0], verb, relation, out reply);
                }
                session.Candidates = null;
            }
            if (!IsRequest(text, actor.RoomId)) return false; // An unfamiliar request still needs the prior focus for semantic interpretation.
            if (MultiAction.IsMatch(text)) { reply = Reply("ขอทำทีละอย่างก่อนนะ อยากให้ฉันไปที่ไหนหรือตรวจอะไรเป็นอย่างแรก?"); return true; }
            string instruction = StripNames(text, named);
            var requestVerb = Verb(instruction); string requestRelation = requestVerb == CompanionWorldVerb.Move ? Relation(instruction) : "at";
            if (requestRelation == null) { reply = Reply("ตำแหน่งที่ขอยังไม่ชัดหรือยังไปแบบนั้นไม่ได้ บอกชื่อจุดหมาย หรือเลือกด้านซ้าย ขวา หรือหน้าของสิ่งนั้นนะ"); return true; }
            if (named.Count == 0 && Reference.IsMatch(text) && session.FocusId != null)
            { var focus = targets.FirstOrDefault(t => t.Definition.id == session.FocusId); if (focus != null) named.Add(focus); }
            if (requestRelation == "between")
            {
                if (requestVerb == CompanionWorldVerb.Move && named.Count == 2)
                    return Dispatch(actor, session, named[0], requestVerb, requestRelation, out reply, named[1]);
                reply = Reply("อยากให้ยืนระหว่างสิ่งไหนกับสิ่งไหน? บอกชื่อทั้งสองอย่างอีกครั้งนะ"); return true;
            }
            if (named.Count == 0 || named.Count > 1)
            {
                var candidates = named.Count > 1 ? named : targets.Where(t => t.Allows(requestVerb)).ToList();
                session.Remember(actor); session.Candidates = candidates.Select(t => t.Definition.id).ToList();
                session.PendingVerb = requestVerb; session.PendingRelation = requestRelation;
                reply = Reply("หมายถึงตรงไหน: " + string.Join(" หรือ ", candidates.Select(t => t.Definition.title)) + "? พิมพ์ชื่อให้ฉันได้เลย"); return true;
            }
            return Dispatch(actor, session, named[0], requestVerb, requestRelation, out reply);
        }

        public static bool TryProposal(NpcCompanionController actor, CompanionWorldSession session, string text, string actionId, out GeneratedChatReply reply)
        {
            reply = null;
            if (actor == null || session == null || string.IsNullOrWhiteSpace(actionId) || !actionId.StartsWith("world:", StringComparison.Ordinal)) return false;
            text = Normalize(text, actor.Scenario);
            if (CompanionDialogueCommands.RulesVeto(text, CompanionCommand.Follow) || !Request.IsMatch((text ?? "").Trim()) || MultiAction.IsMatch(text)) return false;
            string[] parts = actionId.Split(':');
            if (parts.Length != 4 || !Enum.TryParse(parts[1], true, out CompanionWorldVerb verb) || !Enum.IsDefined(typeof(CompanionWorldVerb), verb)) return false;
            if (actionId != Id(verb, parts[3], parts[2])) return false;
            var target = RoomWorldRegistry.Find(actor.RoomId, GameState.Instance, parts[3]);
            if (target == null || !target.Allows(verb)) return false;
            var named = Named(text, RoomWorldRegistry.Available(actor.RoomId, GameState.Instance));
            if (named.Count > 1 || named.Count == 1 && named[0] != target) return false;
            string instruction = StripNames(text, new[] { target });
            if (verb == CompanionWorldVerb.Move && (Relation(instruction) == null || parts[2] != Relation(instruction))) return false;
            // A model may expand wording, but cannot promote a clear look request to opening.
            if (Open.IsMatch(instruction) && verb != CompanionWorldVerb.Open || !Open.IsMatch(instruction) && Inspect.IsMatch(instruction) && verb != CompanionWorldVerb.Inspect ||
                !Open.IsMatch(instruction) && !Inspect.IsMatch(instruction) && Move.IsMatch(instruction) && verb != CompanionWorldVerb.Move) return false;
            if (Reference.IsMatch(text) && named.Count == 0 && (!session.Valid(actor) || session.FocusId != target.Definition.id)) return false;
            return Dispatch(actor, session, target, verb, parts[2], out reply);
        }
        static bool Dispatch(NpcCompanionController actor, CompanionWorldSession session, CompanionWorldTarget target,
            CompanionWorldVerb verb, string relation, out GeneratedChatReply reply, CompanionWorldTarget reference = null)
        {
            if (!target.Allows(verb)) { reply = Reply("ฉันยังทำแบบนั้นกับ " + target.Definition.title + " ไม่ได้ ลองให้ฉันไปดูแทนได้นะ"); return true; }
            bool started = actor.BeginWorldTask(target, verb, relation, reference);
            session.Remember(actor); session.FocusId = target.Definition.id; session.Candidates = null;
            reply = Reply(started ? actor.Bubble : "ตอนนี้ไปทำตรงนั้นไม่ได้ มีทางเดินหรือเงื่อนไขที่ยังไม่พร้อม");
            return true;
        }
        public static GeneratedChatReply Reply(string text) => new GeneratedChatReply { reply = text, referencedFactIds = Array.Empty<string>(), relationshipDelta = 0 };
    }
}
