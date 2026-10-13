using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Knowledge;
using MysteryGame.Core;

namespace MysteryGame.Companions
{
    public enum CompanionCommand { None, Follow, Wait, Help, Comfort, Clarify, Search }

    /// <summary>
    /// A deliberately bounded language adapter, not an AI tool executor.
    /// Parsing/knowledge construction is pure; only SendTypedMessage may apply a request.
    /// </summary>
    public static class CompanionDialogueCommands
    {
        private static Regex R(string pattern) { return new Regex(Normalize(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }
        // Keep quoted/reported/hypothetical speech opaque across punctuation:
        // "if I said, follow me" must not become a command after the comma.
        private static readonly Regex NonDirectContext = R(
            @"บอกว่า|พูดว่า|ถามว่า|คำว่า|ตัวอย่าง|สมมติ|ถ้า|หาก|ชอบเวลา|คิดว่า|จำได้ว่า|หมายถึง|แปลว่า|ผลที่ตามมา|" +
            @"\b(?:said|says|say|if|suppose|pretend|imagine)\b|\bi\s+(?:like|think|remember)\b");
        private static readonly Regex InformationClause = R(
            @"ยังไง|อย่างไร|วิธี|ทำไม|คืออะไร|ทำอะไร|ช่วย(?:บอก|อธิบาย|เล่า|ตอบ)|บอก(?:หน่อย|ที|ได้ไหม)|สถานะ|เคย|เมื่อกี้|เมื่อวาน|กำลัง|" +
            @"(?:ฉัน|เรา|ผม)จะช่วย|อยากให้(?:ฉัน|เรา|ผม)|" +
            @"\b(?:how|why|what|when|whether|tell|explain|describe|understand|did|was|were|status|already|yet|earlier|yesterday)\b|\bi\s+(?:will|can)\b");
        private static readonly Regex InformationRequest = R(
            @"สอน|สาธิต|ช่วย\s*แนะนำ|แนะนำ\s*(?:วิธี|การ)|\bteach\b|\bshow\s+(?:me\s+)?how\b");
        // Aspect + question is status, whereas ได้ไหม/ได้หรือเปล่า and
        // can/could/would-you are supported polite requests.
        private static readonly Regex ActionConfirmation = R(
            @"ใช่(?:ไหม|มั้ย)|จริง(?:ไหม|มั้ย)|เหรอ|หรอ|หรือยัง|แล้วหรือ|" +
            @"(?:อยู่|ไปแล้ว|แล้ว|เสร็จ|สำเร็จ|เรียบร้อย)\s*(?:ไหม|มั้ย|(?:หรือ|รึ)\s*(?:เปล่า|ไม่|ยัง))|" +
            @"(?<!ได้)(?:หรือ|รึ)\s*(?:เปล่า|ไม่)|" +
            @"\b(?:do|does|are|is|have|has)\s+(?:you|we|it)\b");
        private static readonly Regex Clauses = R(@"[\r\n,.!?;:]+|\b(?:but|then|instead|actually)\b|แต่(?:ว่า)?");
        // Standalone tags qualify the preceding clause; they are not unrelated
        // information to discard after accepting a command from that clause.
        private static readonly Regex ConfirmationTag = R(
            @"^\s*(?:(?:ใช่|จริง|ถูก(?:ต้อง)?)(?:ไหม|มั้ย|(?:หรือ|รึ)(?:เปล่า|ไม่))|เหรอ|หรอ|(?:หรือ|รึ)(?:เปล่า|ไม่|ยัง)|" +
            @"right|correct|is\s+that\s+right|(?:do|are|have|don't|aren't|haven't)\s+you|(?:isn't|is\s+not)\s+it)" +
            @"(?:\s*(?:นะ|ล่ะ|ละ|ครับ|ค่ะ|คะ|please)){0,3}\s*$");
        private static readonly Regex Withdrawal = R(
            @"^\s*(?:ไม่(?:ต้อง|เอา)(?:แล้ว|ก่อน)|ยกเลิก(?:คำขอ|คำสั่ง)?(?:นี้|นั้น|ก่อน)?|" +
            @"never\s*mind|scratch\s+that|cancel\s+(?:that|(?:my|the)\s+request))" +
            @"(?:\s*(?:นะ|ล่ะ|ละ|ครับ|ค่ะ|คะ|please)){0,3}\s*$");
        private static readonly Regex StopWork = R(@"^\s*(?:หยุดช่วย(?:ก่อน|แล้ว)?|หยุดงาน(?:ก่อน)?|ยกเลิกงาน(?:นี้|ก่อน)?|stop\s+(?:helping|working)|cancel\s+(?:the\s+)?task)(?:\s*(?:นะ|หน่อย|ครับ|ค่ะ|คะ|please))*\s*[.!?]*$");
        // Only split coordinated actions after classifying the enclosing
        // clause. A question about two actions must not execute its first half.
        private static readonly Regex ActionJoiner = R(
            @"(?:แล้ว(?:ก็)?|และ)(?=\s*(?:ช่วย|กรุณา|โปรด|ขอให้|อยากให้|ตาม|เดิน|รอ|หยุด|ไม่ต้อง|อย่า))|\band\b");
        private static readonly Regex RequestLead = R(
            @"^\s*(?:(?:เอ่อ|อืม|งั้น|โอเค|ตอนนี้|เธอ|คุณ|ขอให้|อยากให้|ช่วย(?:เรา|ฉัน|ผม)?|กรุณา|โปรด)\s*|" +
            @"(?:please|do|you\s+(?:should|must)|(?:can|could|would|will)\s+you)\s+){0,6}$");
        private const string HelpRequestPhrase =
            @"(?:(?:เธอ|คุณ|กรุณา|โปรด|ขอให้|อยากให้)\s*){0,4}" +
            @"(?:ช่วย\s*(?:(?:เรา|ฉัน|ผม|กัน)\s*)?(?:(?:มา|ไป)\s*)?(?:(?:เปิด|จับ|ถือ|ทำกิจกรรม|เรื่อง|กับ)\s*)?|(?:เปิด|จับ|ถือ)\s*)|" +
            @"(?:(?:please|(?:can|could|would|will)\s+you|you\s+should)\s+){0,4}" +
            @"(?:help(?:\s+(?:me|us))?(?:\s+(?:with|open|hold))?|open|hold)\s+(?:(?:the|a)\s+)?";
        private static readonly Regex HelpToTarget = R(@"^\s*(?:" + HelpRequestPhrase + @")$");
        private static readonly Regex HelpMentionToTarget = R(@"(?:" + HelpRequestPhrase + @")$");
        private static readonly Regex StandaloneRefusal = R(@"^\s*(?:อย่า(?:เพิ่ง)?|ไม่ต้อง|ไม่เอา|don't|dont|do\s+not|never|not)\s*(?:นะ|please)?\s*$");
        private static readonly Regex Decline = R(
            @"อย่า|ห้าม|ไม่(?:ต้อง|อยาก|เอา|ขอ|ต้องการ|ได้|ควร|จำเป็น|พร้อม|ตกลง|ใช่)|\b(?:don't|dont|do\s+not|never|not)\b");
        private static readonly Regex Follow = R(
            @"(?:เดิน|ไป|มา)(?:สำรวจ)?(?:ไป|มา)?ด้วยกัน|(?:เดิน)?ตาม(?:เรา|ฉัน|ผม|มา)|" +
            @"\b(?:follow\s+(?:me|us)|(?:come|walk|go)\s+with\s+(?:me|us))\b");
        private static readonly Regex Wait = R(
            @"รอ(?:อยู่)?(?:ตรงนี้|ที่นี่|เรา|ฉัน|ผม)|รอ(?:ก่อน|แป๊บ|เดี๋ยว)|อยู่(?:ตรงนี้|ที่นี่)|ยืนรอ|อยู่นิ่ง(?:ๆ)?|หยุด(?:เดิน(?:ตาม)?|ตาม|เคลื่อนที่|สำรวจ|ค้นหา|หา(?:หลักฐาน|เบาะแส))|หยุด(?=\s*(?:ก่อน|หน่อย|นะ|ครับ|ค่ะ|คะ|$))|" +
            @"ไม่ต้อง(?:เดิน)?ตาม(?:มา|เรา|ฉัน|ผม)?(?:แล้ว|ก่อน)|ไม่ต้องเดิน(?:แล้ว|ก่อน)|" +
            @"\b(?:wait\s+(?:here|for\s+(?:me|us))|wait(?=\s*(?:please|a\s+moment|$))|stay\s+here|stop\s+(?:following|walking|moving)(?:\s+(?:me|us))?|stop(?=\s*(?:please|here|$)))\b");
        private static readonly Regex Search = R(
            @"(?:ช่วย\s*(?:(?:เรา|ฉัน|ผม|กัน)\s*)?)?(?:(?:ลองไป|ไป|ลอง)\s*)?(?:ค้นหา|หา|ค้น)\s*(?:หลักฐาน|เบาะแส)|ช่วย\s*(?:เรา\s*)?สำรวจ(?:ห้อง|แถวนี้)|" +
            @"\b(?:search(?:\s+(?:the\s+)?room)?(?:\s+for\s+(?:clues|evidence))?|look\s+for\s+(?:clues|evidence)|help\s+(?:me\s+)?(?:find|search\s+for)\s+(?:clues|evidence))\b");
        private static readonly Regex ContextHelp = R(@"^\s*(?:ช่วย\s*(?:(?:เรา|ฉัน|ผม)\s*)?)?(?:จับ|ถือ|เปิด)(?:ให้)?(?:\s*(?:หน่อย|ที|ก่อน|ได้ไหม|นะ|ครับ|ค่ะ|คะ))*[.!?]*$");
        private static readonly Regex Comfort = R(
            @"ไม่ต้อง(?:กลัว|ห่วง)|ไม่เป็นไรนะ|(?:เรา|ฉัน|ผม)อยู่(?:ตรงนี้|นี่|ด้วย)|อยู่ด้วยกันนะ|" +
            @"\b(?:don't\s+worry|do\s+not\s+worry|i(?:'m|\s+am)\s+here\s+for\s+you|you(?:'re|\s+are)\s+not\s+alone)\b");
        private static readonly Regex OtherTarget = R(
            @"ประตู|ลิ้นชัก|กุญแจ|รหัส|ภาพวาด|\b(?:door|drawer|key|code|painting)\b");
        private static readonly Regex Quoted = new Regex("[\"“”«»`]|‘[\\s\\S]+?’|(?:^|[\\s,.!?;:])'(?=\\S)[\\s\\S]+?'(?=[\\s,.!?;:]|$)");

        public static IEnumerable<string> RequestTopicsFor(string message)
        {
            string text = Normalize(message);
            foreach (var pattern in new[] { Follow, Wait, Comfort, Search, ContextHelp })
                foreach (Match match in pattern.Matches(text)) yield return match.Value;
        }

        public static CompanionCommand Parse(string message, CompanionScenarioData scenario, out CompanionStationData station)
        {
            station = null;
            if (scenario == null || string.IsNullOrWhiteSpace(message) || message.Length > 1000) return CompanionCommand.None;
            string text = Normalize(message);
            if (Quoted.IsMatch(text) || NonDirectContext.IsMatch(text)) return CompanionCommand.None;
            string[] clauses = Clauses.Split(text);
            foreach (string clause in clauses)
                if (StandaloneRefusal.IsMatch(clause)) return CompanionCommand.None;
            var pending = new List<KeyValuePair<CompanionCommand, CompanionStationData>>();
            int previousClauseStart = 0;
            foreach (string clause in clauses)
            {
                if (string.IsNullOrWhiteSpace(clause)) continue;
                if (Withdrawal.IsMatch(clause))
                {
                    pending.Clear(); previousClauseStart = 0;
                    continue;
                }
                if (ConfirmationTag.IsMatch(clause))
                {
                    pending.RemoveRange(previousClauseStart, pending.Count - previousClauseStart);
                    continue;
                }
                previousClauseStart = pending.Count;
                if (InformationClause.IsMatch(clause) || InformationRequest.IsMatch(clause) || ActionConfirmation.IsMatch(clause)) continue;
                // Negation spans coordinated actions, but not a later independent
                // request. Wait/comfort contain valid negative constructions.
                if (Decline.IsMatch(Comfort.Replace(Wait.Replace(clause, " "), " ")))
                {
                    // A later refusal retracts the same earlier request; merely
                    // ignoring it would execute an action the player withdrew.
                    CompanionStationData refusedTarget;
                    var refused = ReadAction(Decline.Replace(clause, " "), scenario, out refusedTarget);
                    if (refused == CompanionCommand.Clarify) pending.Clear();
                    else if (refused != CompanionCommand.None)
                        pending.RemoveAll(p => p.Key == refused &&
                            (refusedTarget == null || p.Value == refusedTarget));
                    previousClauseStart = pending.Count;
                    continue;
                }
                foreach (string action in ActionJoiner.Split(clause))
                {
                    CompanionStationData candidate;
                    CompanionCommand command = ReadAction(action, scenario, out candidate);
                    if (command == CompanionCommand.None) continue;
                    pending.Add(new KeyValuePair<CompanionCommand, CompanionStationData>(command, candidate));
                }
            }
            // Resolve only after qualifiers/withdrawals have updated the plan.
            // Nothing in parsing applies a command or changes game state.
            CompanionCommand result = CompanionCommand.None;
            CompanionStationData target = null;
            foreach (var request in pending)
            {
                if (request.Key == CompanionCommand.Clarify || (result != CompanionCommand.None &&
                    (result != request.Key || target != request.Value))) return CompanionCommand.Clarify;
                result = request.Key; target = request.Value;
            }
            station = target;
            return result;
        }

        private static CompanionCommand ReadAction(string text, CompanionScenarioData scenario, out CompanionStationData station)
        {
            station = null;
            var matching = new List<CompanionStationData>();
            if (scenario.stations != null) foreach (var item in scenario.stations)
                if (item != null && Mentions(text, item)) matching.Add(item);
            bool wait = IsDirectAction(Comfort.Replace(text, " "), Wait, scenario);
            bool comfort = IsDirectAction(text, Comfort, scenario);
            bool follow = IsDirectAction(Wait.Replace(text, " "), Follow, scenario);
            bool search = IsDirectAction(Wait.Replace(text, " "), Search, scenario);
            bool help = false, helpMention = false;
            foreach (var item in matching)
            {
                if (HasHelp(text, item, scenario, true)) help = true;
                if (HasHelp(text, item, scenario, false)) helpMention = true;
            }
            int count = (wait ? 1 : 0) + (comfort ? 1 : 0) + (follow ? 1 : 0) + (help ? 1 : 0) + (search ? 1 : 0);
            // A valid leading request does not license silently dropping a
            // second incompatible action without a conjunction ("follow me wait here").
            int mentioned = (Wait.IsMatch(Comfort.Replace(text, " ")) ? 1 : 0) + (Comfort.IsMatch(text) ? 1 : 0) +
                (Follow.IsMatch(Wait.Replace(text, " ")) ? 1 : 0) + (helpMention ? 1 : 0) + (Search.IsMatch(Wait.Replace(text, " ")) ? 1 : 0);
            if ((count > 0 && mentioned > 1) || (help && (matching.Count != 1 || OtherTarget.IsMatch(PlayerIntentClassifier.WithoutActivityLocations(text))))) return CompanionCommand.Clarify;
            if (help) { station = matching[0]; return CompanionCommand.Help; }
            if (follow) return CompanionCommand.Follow;
            if (wait) return CompanionCommand.Wait;
            if (comfort) return CompanionCommand.Comfort;
            if (search) return CompanionCommand.Search;
            return CompanionCommand.None;
        }

        private static bool IsDirectAction(string text, Regex action, CompanionScenarioData scenario)
        {
            foreach (Match match in action.Matches(text))
                if (RequestLead.IsMatch(WithoutAddress(text.Substring(0, match.Index), scenario))) return true;
            return false;
        }

        private static bool HasHelp(string text, CompanionStationData station, CompanionScenarioData scenario, bool direct)
        {
            foreach (string name in Names(station))
            {
                string term = TermPattern(name); if (term == null) continue;
                foreach (Match match in Regex.Matches(text, term, RegexOptions.CultureInvariant))
                    if ((direct ? HelpToTarget : HelpMentionToTarget).IsMatch(WithoutAddress(text.Substring(0, match.Index), scenario))) return true;
            }
            return false;
        }

        private static string WithoutAddress(string lead, CompanionScenarioData scenario)
        {
            foreach (string name in new[] { scenario.npcId, scenario.displayName })
            {
                string term = TermPattern(name); if (term == null) continue;
                lead = Regex.Replace(lead, @"^\s*" + term + @"\s+", string.Empty, RegexOptions.CultureInvariant);
            }
            return lead;
        }

        private static IEnumerable<string> Names(CompanionStationData station)
        {
            yield return station.title;
            if (station.gameplayTerms != null) foreach (string name in station.gameplayTerms) yield return name;
            if (station.requestAliases != null) foreach (string name in station.requestAliases) yield return name;
        }

        private static string TermPattern(string name)
        {
            string term = Normalize(name).Trim(); if (term.Length == 0) return null;
            string pattern = Regex.Escape(term);
            return Regex.IsMatch(term, @"^[a-z ]+$") ? @"(?<![a-z])" + pattern + @"(?![a-z])" : pattern;
        }

        private static bool Mentions(string text, CompanionStationData station)
        {
            foreach (string name in Names(station))
            {
                string pattern = TermPattern(name); if (pattern == null) continue;
                if (Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant)) return true;
            }
            return false;
        }

        private static bool CancelsActiveRequest(string text, NpcCompanionController actor)
        {
            if (Quoted.IsMatch(text) || NonDirectContext.IsMatch(text) || InformationClause.IsMatch(text) ||
                ActionConfirmation.IsMatch(text) || Clauses.Split(text.TrimEnd('.', '!', '?', ' ')).Length != 1 ||
                !Decline.IsMatch(Comfort.Replace(Wait.Replace(text, " "), " "))) return false;
            CompanionStationData target;
            var command = ReadAction(Decline.Replace(text, " "), actor.Scenario, out target);
            return command == CompanionCommand.Help && target != null && target == actor.ActiveStation ||
                command == CompanionCommand.Follow && actor.Mode == CompanionMode.Following ||
                command == CompanionCommand.Search && actor.Mode == CompanionMode.Searching;
        }

        private static string Normalize(string message)
        {
            string source = message ?? string.Empty;
            try { source = source.Normalize(NormalizationForm.FormKC); } catch (ArgumentException) { }
            var result = new StringBuilder(source.Length);
            foreach (char c in source) if (char.GetUnicodeCategory(c) != UnicodeCategory.Format)
                result.Append(c == '\u2019' || c == '\u2018' ? '\'' : c);
            return result.ToString().ToLowerInvariant();
        }

        // Called only from the trusted player-send path, never from a provider,
        // policy validator, prompt builder, timeout or repeated callback.
        public static bool TryReply(DialogueManager dialogue, string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (dialogue == null || dialogue != DialogueManager.Instance || !DialogueManager.IsDialogueOpen || GameState.Instance == null) return false;
            var actor = NpcCompanionController.Find(dialogue.ActiveNpcId, GameState.Instance.GetCurrentScene());
            if (actor == null) return false;
            string normalized = Normalize(message);
            if (actor.CanConverse(dialogue) && (StopWork.IsMatch(normalized) || Withdrawal.IsMatch(normalized) || CancelsActiveRequest(normalized, actor)))
            {
                string stopped;
                actor.ApplyDialogueCommand(dialogue, CompanionCommand.Wait, null, out stopped);
                reply = new GeneratedChatReply { reply = stopped, referencedFactIds = Array.Empty<string>() };
                return true;
            }
            if (actor.CanConverse(dialogue) && CompanionConversation.TryReply(actor, normalized, out reply)) return true;
            var knowledge = AiDialogueGenerator.BuildKnowledgeContext(dialogue.ActiveNpcId, message);
            CompanionStationData station;
            CompanionCommand command = Parse(message, actor.Scenario, out station);
            if (command == CompanionCommand.None && ContextHelp.IsMatch(normalized))
            {
                int matches = 0;
                foreach (var candidate in actor.Scenario.stations)
                    if (ConditionRule.AllHold(candidate.conditions, GameState.Instance) &&
                        UnityEngine.Vector2.Distance(actor.PlayerFeet, (candidate.playerPoint + candidate.companionPoint) * 0.5f) <= actor.Scenario.activityLabelDistance)
                    { station = candidate; matches++; }
                command = matches == 1 ? CompanionCommand.Help : CompanionCommand.Clarify;
            }
            if (knowledge.PlayerAskedForHint && command != CompanionCommand.Wait && command != CompanionCommand.Search && !ContextHelp.IsMatch(normalized)) return false;
            if (command == CompanionCommand.None) return false;
            string text;
            actor.ApplyDialogueCommand(dialogue, command, station, out text);
            reply = new GeneratedChatReply { reply = text, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }
    }
}
