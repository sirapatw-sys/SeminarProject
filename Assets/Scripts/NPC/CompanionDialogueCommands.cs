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
        private const string CourtesyParticles = @"(?:\s*(?:นะ|หน่อย|ล่ะ|ละ|ก่อน|ครับ|ค่ะ|คะ|please)){0,4}";
        private const string SentenceEnd = @"[\s.!?,;:…]*$";
        private static readonly Regex Withdrawal = R(
            @"^\s*(?:ไม่(?:ต้อง|เอา)(?:แล้ว|ก่อน)|ยกเลิก(?:คำขอ|คำสั่ง)?(?:นี้|นั้น|ก่อน)?|" +
            @"never\s*mind|scratch\s+that|cancel\s+(?:that|(?:my|the)\s+request))" +
            CourtesyParticles + SentenceEnd);
        private static readonly Regex StopWork = R(@"^\s*(?:หยุดช่วย(?:ก่อน|แล้ว)?|หยุดงาน(?:ก่อน)?|ยกเลิกงาน(?:นี้|ก่อน)?|stop\s+(?:helping|working)|cancel\s+(?:the\s+)?task)" + CourtesyParticles + SentenceEnd);
        private static readonly Regex AmbiguousStop = R(@"^\s*(?:หยุด|พักก่อน|พอแล้ว)" + CourtesyParticles + SentenceEnd);
        // Remove only courtesy conditions, not arbitrary if/hypothetical clauses.
        private static readonly Regex CourtesyCondition = R(
            @"^\s*(?:ถ้าว่าง|ถ้าไม่ยุ่ง)" + CourtesyParticles + @"\s*" +
            @"(?:ก็\s*|(?=\s*(?:ช่วย|กรุณา|โปรด|ขอให้|อยากให้|ตาม|เดิน|มา|ไป|รอ|หยุด|ค้น|หา|ลอง|พัก|พอ)))\s*|" +
            @"\s*(?:ถ้าไม่ลำบาก|ถ้าสะดวก)" + CourtesyParticles + SentenceEnd);
        private static readonly Regex TargetFirstHelp = R(@"^\s*(?:มันโยกนะ|โยกนะ|นี้|มันโยก|โยก)?\s*ช่วย(?:จับ|ถือ|ประคอง|ยก)?(?:ไว้|ให้)?(?:ได้ไหม|หน่อย|ที)(?:นะ|ครับ|ค่ะ|คะ)*\s*$");
        // An unclear activity verb asks back, but a social object is ordinary
        // talk for the AI: "ช่วยจับมือฉันหน่อย", "ช่วยเปิดใจ", "ช่วยหาเรื่องคุย".
        private const string SocialObjects = @"มือ|แขน|ไหล่|ใจ|เพลง|เรื่อง|ความ|คำ|ข้าว|น้ำ";
        private static readonly Regex SocialObject = R(@"(?:" + SocialObjects + @")");
        private static readonly Regex IncompleteActionRequest = R(@"^\s*(?:ช่วย|กรุณา|โปรด|ขอให้|อยากให้)\s*(?:(?:เรา|ฉัน|ผม|มา|ไป|กัน)\s*)?(?:จับ|ถือ|เปิด|ประคอง|ยก|เดิน|ค้น|หา|สำรวจ|ตรวจ)" +
            @"(?!\s*(?:" + SocialObjects + @"))");
        // Words a target-less request may contain. Whatever is left after
        // removing them names a different object ("ช่วยยกเก้าอี้หน่อย").
        private const string RequestFiller =
            // Longer Thai words first: "ที่นี่" must not be eaten as "ที" + a stray mark.
            @"ที่นี่|ที่นั่น|ตรง(?:นี้|นั้น|โน้น)|อัน(?:นี้|นั้น)|ได้ไหม|ได้มั้ย|ขอให้|อยากให้|กรุณา|โปรด|" +
            @"ช่วย|กัน|มา|ไป|ลอง|เธอ|คุณ|เรา|ฉัน|ผม|ให้|ไว้|ด้วย|หน่อย|ที|สิ|ซิ|นะ|ครับ|ค่ะ|คะ|จ้ะ|จ้า|ไหม|มั้ย|เลย|ก่อน|นี่|นั่น|" +
            // R() lower-cases patterns, so no \p{..} or \W classes here: list punctuation.
            @"\b(?:please|help|me|us|you|can|could|would|will|the|a|this|that|it|here|there|with|and|for|to)\b|[\s.,!?;:…'""()/~\-]+";
        private static readonly Regex HelpFiller = R(@"(?:จับ|ถือ|ยก|เปิด|ประคอง|ทำ|ประจำ|ยืน|อีก\s*(?:ด้าน|ฝั่ง|ข้าง)(?:หนึ่ง)?|(?:ฝั่ง|ด้าน|ข้าง)(?:นี้|นั้น|โน้น)?|" +
            @"\b(?:hold|lift|open|side|other)\b|" + RequestFiller + @")");
        private static readonly Regex SearchFiller = R(@"(?:ค้นหา|ค้น|หา|สำรวจ|ตรวจ|ดู|รอบ\s*ๆ?|ห้อง(?:นี้)?|แถวนี้|หลักฐาน|เบาะแส|อะไร|ที่|แปลก\s*ๆ?|ผิดปกติ|น่าสงสัย|ของ|" +
            @"\b(?:search|look|find|clues?|evidence|room|around)\b|" + RequestFiller + @")");
        // Only split coordinated actions after classifying the enclosing
        // clause. A question about two actions must not execute its first half.
        private static readonly Regex ActionJoiner = R(
            @"(?:แล้ว(?:ก็)?|และ)(?=\s*(?:ช่วย|กรุณา|โปรด|ขอให้|อยากให้|ตาม|เดิน|รอ|หยุด|ไม่ต้อง|อย่า))|\band\b");
        private static readonly Regex RequestLead = R(
            @"^\s*(?:(?:เอ่อ|อืม|งั้น|โอเค|ตอนนี้|เดี๋ยวจะไปดูทางนั้น|เธอ|คุณ|ขอให้|อยากให้|ช่วย(?:เรา|ฉัน|ผม)?|กรุณา|โปรด)\s*|" +
            @"(?:please|do|you\s+(?:should|must)|(?:can|could|would|will)\s+you)\s+){0,6}$");
        private const string HelpRequestPhrase =
            @"(?:(?:เธอ|คุณ|กรุณา|โปรด|ขอให้|อยากให้|มา)\s*){0,4}" +
            @"(?:ช่วย\s*(?:(?:เรา|ฉัน|ผม|กัน)\s*)?(?:(?:มา|ไป)\s*)?(?:(?:เปิด|จับ|ถือ|ประคอง|ยก|ทำกิจกรรม|เรื่อง|กับ)\s*)?|(?:เปิด|จับ|ถือ|ประคอง|ยก)\s*)" +
            // "ช่วยจับอีกด้านของกล่อง": the side names the companion's half, not another target.
            @"(?:อีก\s*(?:ด้าน|ฝั่ง|ข้าง)(?:หนึ่ง)?\s*(?:ของ\s*)?)?|" +
            @"(?:(?:please|(?:can|could|would|will)\s+you|you\s+should)\s+){0,4}" +
            @"(?:help(?:\s+(?:me|us))?(?:\s+(?:with|open|hold|lift))?|open|hold|lift)\s+(?:(?:the|a)\s+)?";
        private static readonly Regex HelpToTarget = R(@"^\s*(?:" + HelpRequestPhrase + @")$");
        private static readonly Regex HelpMentionToTarget = R(@"(?:" + HelpRequestPhrase + @")$");
        private static readonly Regex StandaloneRefusal = R(@"^\s*(?:อย่า(?:เพิ่ง)?|ไม่ต้อง|ไม่เอา|don't|dont|do\s+not|never|not)\s*(?:นะ|please)?\s*$");
        private static readonly Regex Decline = R(
            @"อย่า|ห้าม|ไม่(?:ต้อง|อยาก|เอา|ขอ|ต้องการ|ได้|ควร|จำเป็น|พร้อม|ตกลง|ใช่)|\b(?:don't|dont|do\s+not|never|not)\b");
        private static readonly Regex Follow = R(
            @"(?:เดิน|ไป|มา)(?:สำรวจ)?(?:ไป|มา)?ด้วยกัน|มากับ(?:เรา|ฉัน|ผม)|ไปกันเถอะ|มาทางนี้|(?:เดิน)?ตาม(?:เรา|ฉัน|ผม|มา)|" +
            // Short spoken invitations; the lookahead keeps idioms such as "ไปกันใหญ่" out.
            @"มา(?:นี่|ตรงนี้)(?=\s*(?:สิ|ซิ|หน่อย|เร็ว|นะ|ที)*\s*$)|ไปกัน(?:เลย|ต่อ)?(?=\s*(?:นะ|สิ|ซิ|เถอะ)*\s*$)|" +
            @"(?:มา|ไป)ด้วย(?=\s*(?:นะ|สิ|ซิ|หน่อย|ครับ|ค่ะ|คะ)*\s*$)|" +
            @"\b(?:follow\s+(?:me|us)|(?:come|walk|go)\s+with\s+(?:me|us))\b");
        private static readonly Regex Wait = R(
            @"รอ(?:อยู่)?(?:ตรงนี้|ที่นี่|เรา|ฉัน|ผม)|รอ(?:ก่อน|แป๊บ|เดี๋ยว)|อยู่(?:ตรงนี้|ที่นี่)|ยืนรอ|อยู่นิ่ง(?:ๆ)?|หยุด(?:เดิน(?:ตาม)?|ตาม|เคลื่อนที่|สำรวจ|ค้นหา|หา(?:หลักฐาน|เบาะแส))|หยุด(?=\s*(?:ก่อน|หน่อย|นะ|ครับ|ค่ะ|คะ|$))|" +
            @"พักก่อน|พอแล้ว|ไม่ต้อง(?:เดิน)?ตาม(?:มา|เรา|ฉัน|ผม)?(?:แล้ว|ก่อน|(?=\s*$))|ไม่ต้องเดิน(?:แล้ว|ก่อน)|" +
            @"\b(?:wait\s+(?:here|for\s+(?:me|us))|wait(?=\s*(?:please|a\s+moment|$))|stay\s+here|stop\s+(?:following|walking|moving)(?:\s+(?:me|us))?|stop(?=\s*(?:please|here|$)))\b");
        private static readonly Regex Search = R(
            @"ช่วยค้นห้อง|ช่วยดูรอบ\s*ๆ?\s*ห้อง|ลองหาดูหน่อย|(?:ช่วย\s*(?:(?:เรา|ฉัน|ผม|กัน)\s*)?)?(?:(?:ลองไป|ไป|ลอง)\s*)?(?:ค้นหา|หา|ค้น)\s*(?:หลักฐาน|เบาะแส)|" +
            @"ช่วย\s*(?:(?:เรา|กัน)\s*)?(?:สำรวจ|ตรวจ)(?:ดู)?\s*(?:ห้อง|แถวนี้|รอบ\s*ๆ?)|" +
            // A bare "help me look" in this room means the authored search walk.
            @"ช่วย\s*(?:(?:เรา|ฉัน|ผม|กัน)\s*)?(?:ค้นหา|หา|ค้น)(?:ดู)?(?=\s*(?:หน่อย|ที|สิ|ซิ|นะ|ด้วย|ได้ไหม|ครับ|ค่ะ|คะ)*\s*$)|" +
            @"(?:ช่วย|ลอง)\s*(?:(?:เรา|กัน)\s*)?(?:ค้นหา|หา|ค้น)\s*(?:ดู\s*)?(?:อะไร|ของ)\s*(?:ที่\s*)?(?:แปลก\s*ๆ?|ผิดปกติ|น่าสงสัย)|" +
            @"\b(?:search(?:\s+(?:the\s+)?room)?(?:\s+for\s+(?:clues|evidence))?|look\s+for\s+(?:clues|evidence)|help\s+(?:me\s+)?(?:find|search\s+for)\s+(?:clues|evidence))\b");
        private static readonly Regex ContextHelp = R(@"^\s*(?:ช่วย\s*(?:(?:เรา|ฉัน|ผม)\s*)?)?(?:จับ|ถือ|เปิด|ประคอง|ยก)(?:ไว้|ให้)?(?:\s*(?:หน่อย|ที|ก่อน|ได้ไหม|นะ|ครับ|ค่ะ|คะ))*[.!?]*$");
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
            string text = CourtesyCondition.Replace(WithoutAddress(Normalize(message), scenario), " ");
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
                {
                    string lead = WithoutAddress(text.Substring(0, match.Index), scenario);
                    if ((direct ? HelpToTarget : HelpMentionToTarget).IsMatch(lead)) return true;
                    if (string.IsNullOrWhiteSpace(lead) && TargetFirstHelp.IsMatch(text.Substring(match.Index + match.Length))) return true;
                }
            }
            return false;
        }

        private static string WithoutAddress(string lead, CompanionScenarioData scenario)
        {
            var names = new List<string> { scenario.npcId, scenario.displayName };
            if (scenario.addressNames != null) names.AddRange(scenario.addressNames);
            foreach (string name in names)
            {
                string term = TermPattern(name); if (term == null) continue;
                lead = Regex.Replace(lead, @"^\s*" + term + @"[\s,:，]*", string.Empty, RegexOptions.CultureInvariant);
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
            if (term == "กล่อง") return pattern + @"(?!ดนตรี)";
            if (term == "box") return @"(?<!music )(?<![a-z])box(?![a-z])";
            return Regex.IsMatch(term, @"^[a-z ]+$") ? @"(?<![a-z])" + pattern + @"(?![a-z])" : pattern;
        }

        public static IEnumerable<string> MatchingNames(string message, CompanionStationData station)
        {
            string text = Normalize(message);
            foreach (string name in Names(station))
            {
                string pattern = TermPattern(name);
                if (pattern != null && Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant)) yield return name;
            }
        }

        public static bool IsActivityQuestion(string message, CompanionStationData station)
        {
            string text = Normalize(message);
            if (Quoted.IsMatch(text) || NonDirectContext.IsMatch(CourtesyCondition.Replace(text, " "))) return false;
            var names = new List<string> { station.title };
            if (station.gameplayTerms != null) names.AddRange(station.gameplayTerms);
            if (station.questionAliases != null) names.AddRange(station.questionAliases);
            return PlayerIntentClassifier.IsCompanionRequest(text, names);
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
                ActionConfirmation.IsMatch(text) || Clauses.Split(text.TrimEnd('.', '!', '?', '…', ',', ';', ':', ' ', '\t', '\r', '\n')).Length != 1 ||
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

        /// <summary>
        /// The parser's speech-act rules applied to a model proposal. They can
        /// only say no: quoted, hypothetical or reported speech, questions,
        /// confirmations, information requests, withdrawn requests ("follow me,
        /// scratch that") and refusals ("อย่าหยุดเดิน"). For a stop request the
        /// negative stop phrases themselves ("ไม่ต้องตามแล้ว") are not refusals.
        /// </summary>
        public static bool RulesVeto(string text, CompanionCommand command)
        {
            text = Normalize(text);
            if (string.IsNullOrWhiteSpace(text) || Quoted.IsMatch(text) || NonDirectContext.IsMatch(text)) return true;
            foreach (string clause in Clauses.Split(text))
            {
                if (string.IsNullOrWhiteSpace(clause)) continue;
                if (Withdrawal.IsMatch(clause) || StandaloneRefusal.IsMatch(clause) || ConfirmationTag.IsMatch(clause) ||
                    InformationClause.IsMatch(clause) || InformationRequest.IsMatch(clause) || ActionConfirmation.IsMatch(clause))
                    return true;
                // A withdrawal typed without punctuation still ends the request ("ตามมา ยกเลิก").
                for (int i = 1; i < clause.Length; i++)
                    if (char.IsWhiteSpace(clause[i - 1]) && !char.IsWhiteSpace(clause[i]) && Withdrawal.IsMatch(clause.Substring(i)))
                        return true;
            }
            string withoutAllowed = Comfort.Replace(command == CompanionCommand.Wait ? Wait.Replace(text, " ") : text, " ");
            return Decline.IsMatch(withoutAllowed);
        }

        /// <summary>True when a help/search request names something other than
        /// a companion activity, e.g. "ช่วยยกเก้าอี้หน่อย" or "ช่วยจับมือฉันหน่อย".</summary>
        public static bool NamesOtherObject(string text, CompanionCommand command)
        {
            Regex filler = command == CompanionCommand.Help ? HelpFiller : command == CompanionCommand.Search ? SearchFiller : null;
            if (filler == null) return false;
            string residue = filler.Replace(Normalize(text), string.Empty);
            // A tone mark left from a split word is not an object.
            return Regex.Replace(residue, @"\p{M}", string.Empty).Length > 0;
        }

        // Called only from the trusted player-send path, never from a provider,
        // policy validator, prompt builder, timeout or repeated callback.
        public static bool TryReply(DialogueManager dialogue, string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (dialogue == null || dialogue != DialogueManager.Instance || !DialogueManager.IsDialogueOpen || GameState.Instance == null) return false;
            var actor = NpcCompanionController.Find(dialogue.ActiveNpcId, GameState.Instance.GetCurrentScene());
            if (actor == null) return false;
            string normalized = CourtesyCondition.Replace(WithoutAddress(Normalize(message), actor.Scenario), " ").Trim();
            bool vagueStop = StopWork.IsMatch(normalized) || Withdrawal.IsMatch(normalized) || AmbiguousStop.IsMatch(normalized);
            if (vagueStop && actor.Mode == CompanionMode.Waiting) return false;
            if (actor.CanConverse(dialogue) && (vagueStop || CancelsActiveRequest(normalized, actor)))
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
            // Rules act when they are sure. When they are merely unsure and a
            // model is available, the model reads the request instead (its
            // proposal is validated by TryApplyProposedAction); offline, ask back.
            bool modelCanRead = DialogueProviders.Current != null && DialogueProviders.Current.CanGenerate;
            if (command == CompanionCommand.None && ContextHelp.IsMatch(normalized))
            {
                int matches = 0;
                foreach (var candidate in actor.Scenario.stations)
                    if (!actor.IsCompleted(candidate) && ConditionRule.AllHold(candidate.conditions, GameState.Instance) &&
                        UnityEngine.Vector2.Distance(actor.PlayerFeet, (candidate.playerPoint + candidate.companionPoint) * 0.5f) <= actor.Scenario.activityLabelDistance)
                    { station = candidate; matches++; }
                command = matches == 1 ? CompanionCommand.Help : modelCanRead ? CompanionCommand.None : CompanionCommand.Clarify;
                if (command != CompanionCommand.Help) station = null;
            }
            if (command == CompanionCommand.None && !modelCanRead && !knowledge.PlayerAskedForHint && IsUnclearActionRequest(normalized))
                command = CompanionCommand.Clarify;
            if (knowledge.PlayerAskedForHint && command != CompanionCommand.Wait && command != CompanionCommand.Search && !ContextHelp.IsMatch(normalized)) return false;
            if (command == CompanionCommand.None) return false;
            string text;
            actor.ApplyDialogueCommand(dialogue, command, station, out text);
            reply = new GeneratedChatReply { reply = text, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }

        private static bool IsUnclearActionRequest(string normalized)
        {
            return IncompleteActionRequest.IsMatch(normalized) && !Quoted.IsMatch(normalized) &&
                !NonDirectContext.IsMatch(normalized) && !InformationClause.IsMatch(normalized) &&
                !InformationRequest.IsMatch(normalized) && !ActionConfirmation.IsMatch(normalized) && !Decline.IsMatch(normalized);
        }

        /// <summary>
        /// The model was asked about an unclear request ("ช่วยเดินอีกแบบหนึ่งหน่อย") but
        /// produced nothing usable. Ask back, as offline play does, rather than
        /// answering with an unrelated neutral line.
        /// </summary>
        public static bool TryAskBack(DialogueManager dialogue, string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (dialogue == null || dialogue != DialogueManager.Instance || !DialogueManager.IsDialogueOpen || GameState.Instance == null) return false;
            var actor = NpcCompanionController.Find(dialogue.ActiveNpcId, GameState.Instance.GetCurrentScene());
            if (actor == null || actor.Scenario == null) return false;
            string normalized = CourtesyCondition.Replace(WithoutAddress(Normalize(message), actor.Scenario), " ").Trim();
            if (!IsUnclearActionRequest(normalized) ||
                AiDialogueGenerator.BuildKnowledgeContext(dialogue.ActiveNpcId, message).PlayerAskedForHint) return false;
            string line;
            actor.ApplyDialogueCommand(dialogue, CompanionCommand.Clarify, null, out line);
            if (string.IsNullOrWhiteSpace(line)) return false;
            reply = new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }

        /// <summary>
        /// The model's reading of a message the rules could not classify. The
        /// rules can only veto (quoted, hypothetical, confirming questions,
        /// refusals); the controller then checks range, conditions and modals
        /// exactly as for a parsed command. Returns false to keep the model's
        /// own reply (no proposal, a veto, an unknown id, or a no-op).
        /// </summary>
        public static bool TryApplyProposedAction(DialogueManager dialogue, string message, string actionId, out GeneratedChatReply reply)
        {
            reply = null;
            if (string.IsNullOrWhiteSpace(actionId) || actionId == CompanionActionOption.None ||
                dialogue == null || dialogue != DialogueManager.Instance || !DialogueManager.IsDialogueOpen || GameState.Instance == null) return false;
            var actor = NpcCompanionController.Find(dialogue.ActiveNpcId, GameState.Instance.GetCurrentScene());
            if (actor == null || actor.Scenario == null) return false;

            CompanionCommand command = CompanionCommand.None;
            CompanionStationData station = null;
            if (actionId == CompanionActionOption.Follow) command = CompanionCommand.Follow;
            else if (actionId == CompanionActionOption.Wait) command = CompanionCommand.Wait;
            else if (actionId == CompanionActionOption.Search) command = CompanionCommand.Search;
            else if (actionId.StartsWith(CompanionActionOption.HelpPrefix, StringComparison.Ordinal))
            {
                string id = actionId.Substring(CompanionActionOption.HelpPrefix.Length);
                if (actor.Scenario.stations != null)
                    foreach (var candidate in actor.Scenario.stations)
                        if (candidate != null && candidate.id == id) { station = candidate; break; }
                if (station != null) command = CompanionCommand.Help;
            }
            if (command == CompanionCommand.None) return false;

            string text = CourtesyCondition.Replace(WithoutAddress(Normalize(message), actor.Scenario), " ").Trim();
            if (RulesVeto(text, command)) return false;
            if (AiDialogueGenerator.BuildKnowledgeContext(dialogue.ActiveNpcId, message).PlayerAskedForHint &&
                command != CompanionCommand.Wait && command != CompanionCommand.Search) return false;

            // The player named an activity's object ("ไปช่วยตรงโต๊ะหน่อยสิ"). The model
            // may only judge that it is a request; it must not swap in a different
            // task (a search walk) when that object's activity is locked or done.
            if (command == CompanionCommand.Search || command == CompanionCommand.Help)
            {
                var named = new List<CompanionStationData>();
                if (actor.Scenario.stations != null)
                    foreach (var candidate in actor.Scenario.stations)
                        if (candidate != null && Mentions(text, candidate)) named.Add(candidate);
                if (named.Count > 1 && (command == CompanionCommand.Search || !named.Contains(station))) return false;
                if (named.Count == 1 && station != named[0]) { command = CompanionCommand.Help; station = named[0]; }
                // No activity named: the request must not name some other object
                // either ("ช่วยยกเก้าอี้หน่อย" proposed as the box). A social object
                // stays conversation; anything else is asked about, not guessed.
                if (named.Count == 0 && NamesOtherObject(text, command))
                {
                    if (SocialObject.IsMatch(text)) return false;
                    string clarify;
                    actor.ApplyDialogueCommand(dialogue, CompanionCommand.Clarify, null, out clarify);
                    if (string.IsNullOrWhiteSpace(clarify)) return false;
                    reply = new GeneratedChatReply { reply = clarify, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
                    return true;
                }
            }

            // Already doing it: let the model's reply answer instead of a stock line.
            if ((command == CompanionCommand.Wait && actor.Mode == CompanionMode.Waiting) ||
                (command == CompanionCommand.Follow && actor.Mode == CompanionMode.Following) ||
                (command == CompanionCommand.Search && actor.Mode == CompanionMode.Searching) ||
                (command == CompanionCommand.Help && actor.ActiveStation == station)) return false;

            string line;
            actor.ApplyDialogueCommand(dialogue, command, station, out line);
            if (string.IsNullOrWhiteSpace(line)) return false;
            // Started or refused, the answer is the companion's authored line,
            // never a model promise about something the game did not do.
            reply = new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }
    }
}
