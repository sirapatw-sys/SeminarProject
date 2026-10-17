using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private static readonly Regex StayRequest = R(
            @"^\s*(?:(?:please|ช่วย|กรุณา)\s*)?(?:อย่า(?:เพิ่ง)?(?:ไป(?:ไหน)?|เดินไปไหน)|" +
            @"(?:don't|dont|do\s+not)\s+(?:go\s+away|leave(?:\s+yet)?))" + CourtesyParticles + SentenceEnd);
        private static readonly Regex FollowProposalVerb = R(
            @"เดิน|ตาม|มา(?:กับ|ด้วย|นี่|ตรงนี้)|ไป(?:กับ|ด้วย|กัน)|\b(?:walk|follow|come|go)\b");
        private static readonly Regex WaitProposalVerb = R(@"รอ|หยุด|พัก|อยู่|ยืน|\b(?:wait|stay|stop|pause|stand)\b");
        // Proposals may interpret a request, but a target or courtesy particle
        // alone never grants permission. Keep the action verb in the player's text.
        private static readonly Regex HelpProposalVerb = R(
            @"(?:(?:ไป|มา|ลองไป|ลอง)\s*)?(?:ช่วย|จับ|ถือ|ประคอง|ยก|เปิด)|\b(?:help|hold|lift|open)\b");
        private static readonly Regex SearchProposalVerb = R(
            @"(?:(?:ไป|มา|ลองไป|ลอง)\s*)?(?:ค้น|หา|สำรวจ|ตรวจ|ดู)|\b(?:search|look|find|inspect|explore)\b");
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
        private static readonly Regex HelpFiller = R(@"(?:ได้(?:หรือ|รึ)เปล่า|แล้ว(?:ก็)?|และ|จับ|ถือ|ยก|เปิด|ประคอง|ทำ|ประจำ|ยืน|ตรง(?:นี้|นั้น|โน้น)?|ที่|กับ|อีก\s*(?:ด้าน|ฝั่ง|ข้าง)(?:หนึ่ง)?|(?:ฝั่ง|ด้าน|ข้าง)(?:นี้|นั้น|โน้น)?|" +
            @"ของ|\b(?:hold|lift|open|side|other|of)\b|" + RequestFiller + @")");
        // Follow means accompany the player; wait means stop at the current
        // position. Neither action supports arbitrary destinations or objects.
        private static readonly Regex MovementFiller = R(@"(?:เป็นเพื่อน|ด้วยกัน|ทางนี้|ข้าง\s*ๆ?|แป๊บ(?:นึง|หนึ่ง)?|เดี๋ยว|สำรวจ|แล้ว(?:ก็)?|และ|" +
            @"เดิน|ตาม|รอ|หยุด|พัก|อยู่|ยืน|กับ|ไป|มา|ต่อ|เถอะ|เร็ว|" +
            @"\b(?:following|walking|moving|walk|follow|come|go|wait|stay|stop|pause|stand|beside|along|over|a\s+moment)\b|" + RequestFiller + @")");
        private static readonly Regex SearchFiller = R(@"(?:ได้(?:หรือ|รึ)เปล่า|แล้ว|ค้นหา|ค้น|หา|สำรวจ|ตรวจ|ดู|รอบ\s*ๆ?|ห้อง(?:นี้)?|แถวนี้|ทั่ว(?:ทั้ง)?|ทั้ง|ใน|เฉพาะ|เท่านั้น|แค่|หลักฐาน|เบาะแส|อะไร|ที่|แปลก\s*ๆ?|ผิดปกติ|น่าสงสัย|ของ|" +
            @"\b(?:search|look|find|inspect|explore|clues?|evidence|room|around|in|only|just|throughout)\b|" + RequestFiller + @")");
        private static readonly Regex WaitDestination = R(
            @"(?:ไป|เดินไป)\s*(?:รอ|ยืน|อยู่|เฝ้า)|(?:รอ|ยืน|อยู่|เฝ้า)[^,.!?;:]{0,12}?(?:ที่|ตรง|ข้าง|หน้า|หลัง|ใกล้)(?!\s*(?:นี้|นี่|ที่นี่|ๆ))|" +
            @"\b(?:wait|stay|stand)\b[^,.!?;:]{0,16}?\b(?:at|by|near|next\s+to|in\s+front\s+of|behind)\b");
        private static readonly Regex SearchVerb = R(@"ค้น|หา|สำรวจ|ตรวจ|ดู|\b(?:search|look|find|inspect|explore)\b");
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
            @"(?:มา|ไป)ด้วย(?=\s*(?:นะ|สิ|ซิ|หน่อย|ครับ|ค่ะ|คะ)*\s*$)|(?:เดิน|มา|ไป)\s*(?:ไป\s*)?เป็นเพื่อน|" +
            @"\b(?:follow\s+(?:me|us)|(?:come|walk|go)\s+with\s+(?:me|us))\b");
        private static readonly Regex Wait = R(
            @"รอ(?:อยู่)?(?:ตรงนี้|ที่นี่|เรา|ฉัน|ผม)|รอ(?:ก่อน|แป๊บ(?:นึง|หนึ่ง)?|เดี๋ยว)|อยู่\s*(?:เฝ้า|รอ)?\s*(?:ตรงนี้|ที่นี่)|อยู่\s*(?:เฝ้า|รอ)(?=\s*(?:ก่อน|นะ|หน่อย|ครับ|ค่ะ|คะ|$))|เฝ้า(?:ตรงนี้|ที่นี่|ไว้)|ยืนรอ|อยู่นิ่ง(?:ๆ)?|หยุด(?:เดิน(?:ตาม)?|ตาม|เคลื่อนที่|สำรวจ|ค้นหา|หา(?:หลักฐาน|เบาะแส))|หยุด(?=\s*(?:ก่อน|หน่อย|นะ|ครับ|ค่ะ|คะ|$))|" +
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
            text = CurrentRequest(text, scenario);
            if (StayRequest.IsMatch(text)) return CompanionCommand.Wait;
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
                // Validate the complete request before splitting coordinated
                // actions: "hold the desk and lift the chair" must not lose
                // the unsupported second target and start the first task.
                CompanionStationData combinedTarget;
                if (ReadAction(clause, scenario, out combinedTarget) == CompanionCommand.Clarify)
                {
                    pending.Add(new KeyValuePair<CompanionCommand, CompanionStationData>(CompanionCommand.Clarify, null));
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
            if (help && HasOtherActivityTarget(text, CompanionCommand.Help, matching[0])) return CompanionCommand.Clarify;
            if (search && HasOtherActivityTarget(text, CompanionCommand.Search, null)) return CompanionCommand.Clarify;
            if (follow && HasMovementTargetConflict(text, CompanionCommand.Follow) ||
                wait && HasMovementTargetConflict(text, CompanionCommand.Wait)) return CompanionCommand.Clarify;
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

        // Pure decomposition: cancellation and the remaining request are distinct.
        // The trusted send path applies the former before evaluating the latter.
        private static bool TryReadWithdrawal(string text, out string remaining)
        {
            remaining = text;
            if (Quoted.IsMatch(text) || NonDirectContext.IsMatch(text)) return false;
            string[] clauses = Clauses.Split(text);
            int last = -1;
            for (int i = 0; i < clauses.Length; i++)
            {
                bool withdraws = Withdrawal.IsMatch(clauses[i]);
                for (int c = 1; !withdraws && c < clauses[i].Length; c++)
                    if (char.IsWhiteSpace(clauses[i][c - 1]) && !char.IsWhiteSpace(clauses[i][c]))
                    {
                        string lead = clauses[i].Substring(0, c);
                        withdraws = !Decline.IsMatch(lead) && !InformationClause.IsMatch(lead) &&
                            !InformationRequest.IsMatch(lead) && !ActionConfirmation.IsMatch(lead) &&
                            Withdrawal.IsMatch(clauses[i].Substring(c));
                    }
                if (!withdraws) continue;
                // "cancel that, right?" asks about cancellation, not to do it.
                int next = i + 1;
                while (next < clauses.Length && string.IsNullOrWhiteSpace(clauses[next])) next++;
                if (next < clauses.Length && ConfirmationTag.IsMatch(clauses[next])) continue;
                last = i;
            }
            if (last < 0) return false;
            remaining = string.Join(", ", clauses, last + 1, clauses.Length - last - 1).Trim();
            return true;
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
            if (command == CompanionCommand.Wait && StayRequest.IsMatch(text)) return false;
            text = AfterLastWithdrawal(text);
            if (string.IsNullOrWhiteSpace(text)) return true;
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

        // A standalone withdrawal cancels earlier clauses, not a new request
        // spoken afterwards. Quotes/hypotheticals still veto the whole message.
        private static string AfterLastWithdrawal(string text)
        {
            string remaining;
            return TryReadWithdrawal(text, out remaining) ? remaining : text;
        }

        private static string CurrentRequest(string text, CompanionScenarioData scenario)
        {
            return CourtesyCondition.Replace(WithoutAddress(AfterLastWithdrawal(text), scenario), " ").Trim();
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

        private static bool HasOtherActivityTarget(string text, CompanionCommand command, CompanionStationData station)
        {
            // Search currently covers the authored room route, not a chosen
            // object. Do not erase its target as an activity location or station
            // name ("search in the drawer" is not a request to search elsewhere).
            if (command == CompanionCommand.Search) return NamesOtherObject(text, command);
            string remaining = WithoutStationNames(PlayerIntentClassifier.WithoutActivityLocations(text), station);
            // Preserve the existing target-first form, not arbitrary descriptive
            // words which might conceal a second object.
            if (command == CompanionCommand.Help && TargetFirstHelp.IsMatch(remaining)) return false;
            return NamesOtherObject(remaining, command);
        }

        private static string WithoutStationNames(string text, CompanionStationData station)
        {
            string remaining = text;
            if (station != null)
                foreach (string name in MatchingNames(text, station).OrderByDescending(n => n.Length))
                    remaining = Regex.Replace(remaining, TermPattern(name), " ", RegexOptions.CultureInvariant);
            return remaining;
        }

        private static string SearchClarification(DialogueManager dialogue, NpcCompanionController actor, string text, string fallback)
        {
            return actor.CanConverse(dialogue) && SearchVerb.IsMatch(text) &&
                HasOtherActivityTarget(text, CompanionCommand.Search, null) &&
                !string.IsNullOrWhiteSpace(actor.Scenario.searchScopeLine) ? actor.Scenario.searchScopeLine : fallback;
        }

        private static bool HasMovementTargetConflict(string text, CompanionCommand command)
        {
            // Stopping is always safe, so a stop request with an explanation
            // ("หยุดเดินตามก่อนนะ ผมไปทางนั้นก่อน", "รอแป๊บนึงนะ") still stops.
            // Only a wait somewhere else ("ไปรอที่ประตู") is a destination to ask about.
            if (command == CompanionCommand.Wait) return !StayRequest.IsMatch(text) && WaitDestination.IsMatch(text);
            Regex action = command == CompanionCommand.Follow ? Follow : Wait;
            string remaining = text;
            foreach (Match match in action.Matches(text))
                if (RequestLead.IsMatch(text.Substring(0, match.Index)))
                { remaining = text.Substring(match.Index); break; }
            remaining = action.Replace(remaining, " ");
            remaining = MovementFiller.Replace(remaining, string.Empty);
            return Regex.Replace(remaining, @"\p{M}", string.Empty).Length > 0;
        }

        // Called only from the trusted player-send path, never from a provider,
        // policy validator, prompt builder, timeout or repeated callback.
        public static bool TryReply(DialogueManager dialogue, string message, out GeneratedChatReply reply)
        {
            reply = null;
            if (dialogue == null || dialogue != DialogueManager.Instance || !DialogueManager.IsDialogueOpen || GameState.Instance == null) return false;
            if (TryResolveHelpClarification(dialogue, message, out reply)) return true;
            var actor = NpcCompanionController.Find(dialogue.ActiveNpcId, GameState.Instance.GetCurrentScene());
            if (actor == null) return false;
            if (actor.CanConverse(dialogue) && CompanionWorldCommands.TryLocation(actor, message, out reply)) return true;
            if (actor.CanConverse(dialogue) && CompanionWorldCommands.TryRecall(actor, message, out reply)) return true;
            string normalized = CourtesyCondition.Replace(WithoutAddress(Normalize(message), actor.Scenario), " ").Trim();
            string remaining;
            if (actor.CanConverse(dialogue) && TryReadWithdrawal(normalized, out remaining))
            {
                dialogue.ReadingContext = null;
                bool active = actor.Mode != CompanionMode.Waiting;
                string stopped = null;
                if (active) actor.ApplyDialogueCommand(dialogue, CompanionCommand.Wait, null, out stopped);
                if (string.IsNullOrWhiteSpace(remaining))
                {
                    if (!active) return false; // Idle vague refusals remain ordinary conversation.
                    reply = new GeneratedChatReply { reply = stopped, referencedFactIds = Array.Empty<string>() };
                    return true;
                }
                // Do not let a refused task, conversation or a slow/unavailable
                // provider undo the cancellation. Only a valid NEW request may
                // start new work, through the same controller guards as before.
                normalized = CurrentRequest(remaining, actor.Scenario);
                message = normalized;
            }
            bool vagueStop = StopWork.IsMatch(normalized) || AmbiguousStop.IsMatch(normalized);
            if (vagueStop) dialogue.ReadingContext = null;
            if (vagueStop && actor.Mode == CompanionMode.Waiting) return false;
            if (actor.CanConverse(dialogue) && (vagueStop || CancelsActiveRequest(normalized, actor)))
            {
                dialogue.ReadingContext = null;
                string stopped;
                actor.ApplyDialogueCommand(dialogue, CompanionCommand.Wait, null, out stopped);
                reply = new GeneratedChatReply { reply = stopped, referencedFactIds = Array.Empty<string>() };
                return true;
            }
            if (CompanionNaturalRequests.TryReply(dialogue, actor, normalized, out reply)) return true;
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
                if (matches != 1 && actor.CanConverse(dialogue))
                    return AskHelpTarget(dialogue, actor, out reply);
                command = matches == 1 ? CompanionCommand.Help : modelCanRead ? CompanionCommand.None : CompanionCommand.Clarify;
                if (command != CompanionCommand.Help) station = null;
            }
            if (command == CompanionCommand.None && !modelCanRead && !knowledge.PlayerAskedForHint && IsUnclearActionRequest(normalized))
                command = CompanionCommand.Clarify;
            bool unsupportedSearch = command == CompanionCommand.Clarify && IsDirectAction(normalized, Search, actor.Scenario) &&
                HasOtherActivityTarget(normalized, CompanionCommand.Search, null);
            if (knowledge.PlayerAskedForHint && command != CompanionCommand.Wait && command != CompanionCommand.Search &&
                !unsupportedSearch && !ContextHelp.IsMatch(normalized)) return false;
            if (command == CompanionCommand.None) return false;
            string text;
            actor.ApplyDialogueCommand(dialogue, command, station, out text);
            if (command == CompanionCommand.Clarify) text = SearchClarification(dialogue, actor, normalized, text);
            reply = new GeneratedChatReply { reply = text, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }

        private static bool IsExactStationAnswer(string text, CompanionStationData station)
        {
            return Names(station).Any(name => {
                string pattern = TermPattern(name);
                return pattern != null && Regex.IsMatch(text, @"^\s*" + pattern + CourtesyParticles + SentenceEnd, RegexOptions.CultureInvariant);
            });
        }

        private static bool AskHelpTarget(DialogueManager dialogue, NpcCompanionController actor, out GeneratedChatReply reply)
        {
            var candidates = actor.Scenario.stations.Where(s => s != null && !actor.IsCompleted(s) &&
                ConditionRule.AllHold(s.conditions, GameState.Instance)).ToList();
            string line = "ตอนนี้ยังไม่มีจุดที่ฉันช่วยจับให้ได้ ถ้าพบอะไรที่ต้องช่วยกันค่อยบอกนะ";
            if (candidates.Count > 0)
            {
                var pending = new PendingCompanionHelp { Actor = actor, State = GameState.Instance,
                    ConversationVersion = dialogue.ConversationVersion, TaskRevision = actor.TaskRevision, RoomId = actor.RoomId };
                foreach (var station in candidates) pending.CandidateIds.Add(station.id);
                dialogue.PendingCompanionHelp = pending;
                CompanionPlaytestTrace.Record("clarification", actor.RoomId, actor.Scenario.npcId,
                    request: dialogue.ConversationVersion.ToString(), outcome: "asked", target: "help");
                line = actor.Scenario.helpClarificationLine + "\n" + string.Join(" / ", candidates.Select(s => s.title));
            }
            reply = new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }

        private static bool TryResolveHelpClarification(DialogueManager dialogue, string message, out GeneratedChatReply reply)
        {
            reply = null;
            var pending = dialogue.PendingCompanionHelp;
            dialogue.PendingCompanionHelp = null; // Expire on the very next send, including unrelated talk/refusals.
            if (pending == null) return false;
            CompanionPlaytestTrace.Record("clarification_end", pending.RoomId, pending.Actor?.Scenario?.npcId,
                request: pending.ConversationVersion.ToString(), outcome: "consumed");
            var actor = pending.Actor;
            if (actor == null || !actor.CanConverse(dialogue) || pending.State != GameState.Instance ||
                pending.RoomId != GameState.Instance.GetCurrentScene() || pending.ConversationVersion != dialogue.ConversationVersion ||
                pending.TaskRevision != actor.TaskRevision) return false;
            string text = WithoutAddress(Normalize(message), actor.Scenario).Trim();
            if (RulesVeto(text, CompanionCommand.Help)) return false;
            var targets = actor.Scenario.stations.Where(s => s != null && pending.CandidateIds.Contains(s.id) &&
                IsExactStationAnswer(text, s)).ToList();
            if (targets.Count != 1) return false;
            string line;
            actor.ApplyDialogueCommand(dialogue, CompanionCommand.Help, targets[0], out line);
            CompanionPlaytestTrace.Record("clarification_resolved", actor.RoomId, actor.Scenario.npcId,
                target: targets[0].id, request: dialogue.ConversationVersion.ToString(), outcome: "matched");
            reply = new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
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
            normalized = CurrentRequest(normalized, actor.Scenario);
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
        /// rules veto unsafe speech and require positive action intent; the
        /// controller then checks range, conditions and modals
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
            if (CompanionWorldCommands.TryProposal(actor, dialogue.WorldCommandSession, message, actionId, out reply)) return true;

            if (CompanionNaturalRequests.TryApplyProposal(dialogue, actor, message, actionId, out reply)) return true;
            return TryApplyCommonProposal(dialogue, actor, message, actionId, out reply);
        }
        public static bool TryApplyRoomProposedAction(NpcCompanionController actor, string message, string actionId, out GeneratedChatReply reply)
        {
            reply = null;
            if (actor == null || !actor.CanReceiveRoomCommand || string.IsNullOrWhiteSpace(actionId) || actionId == CompanionActionOption.None) return false;
            return TryApplyCommonProposal(null, actor, message, actionId, out reply);
        }
        private static bool ApplyForSurface(NpcCompanionController actor, DialogueManager dialogue, CompanionCommand command,
            CompanionStationData station, out string line)
        {
            if (dialogue != null) return actor.ApplyDialogueCommand(dialogue, command, station, out line);
            bool applied = actor.ApplyRoomCommand(command, station);
            line = applied ? actor.Bubble : actor.Scenario.helpClarificationLine; return applied;
        }
        private static bool TryApplyCommonProposal(DialogueManager dialogue, NpcCompanionController actor,
            string message, string actionId, out GeneratedChatReply reply)
        {
            reply = null;
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
            text = CurrentRequest(text, actor.Scenario);
            // A model's movement proposal needs a direct movement request.
            // Greetings, hand-holding and other social talk do not authorize it.
            if (command == CompanionCommand.Follow && !IsDirectAction(text, FollowProposalVerb, actor.Scenario) ||
                command == CompanionCommand.Wait && !StayRequest.IsMatch(text) && !IsDirectAction(text, WaitProposalVerb, actor.Scenario))
                return false;
            if ((command == CompanionCommand.Follow || command == CompanionCommand.Wait) && HasMovementTargetConflict(text, command))
            {
                string clarify;
                ApplyForSurface(actor, dialogue, CompanionCommand.Clarify, null, out clarify);
                if (string.IsNullOrWhiteSpace(clarify)) return false;
                reply = new GeneratedChatReply { reply = clarify, referencedFactIds = Array.Empty<string>() };
                return true;
            }
            if (AiDialogueGenerator.BuildKnowledgeContext(actor.Scenario.npcId, message).PlayerAskedForHint &&
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
                if (named.Count > 1)
                {
                    string ambiguous;
                    ApplyForSurface(actor, dialogue, CompanionCommand.Clarify, null, out ambiguous);
                    reply = new GeneratedChatReply { reply = ambiguous, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
                    return true;
                }
                // A real search request naming the desk must not become holding
                // the desk. Keep the old help correction only when the player
                // actually asked for assistance ("ไปช่วยตรงโต๊ะหน่อยสิ").
                if (named.Count == 1 && station != named[0] &&
                    (command != CompanionCommand.Search || !SearchVerb.IsMatch(text)))
                { command = CompanionCommand.Help; station = named[0]; }
                // Check the final action after target correction. A pending
                // clarification is consumed only by TryResolveHelpClarification;
                // the model cannot create fresh consent from "the desk".
                if (command == CompanionCommand.Help &&
                    !IsDirectAction(text, HelpProposalVerb, actor.Scenario) &&
                    (station == null || !HasHelp(text, station, actor.Scenario, true)))
                    return false;
                if (command == CompanionCommand.Search &&
                    !IsDirectAction(text, SearchProposalVerb, actor.Scenario))
                    return false;
                // No activity named: the request must not name some other object
                // either ("ช่วยยกเก้าอี้หน่อย" proposed as the box). A social object
                // stays conversation; anything else is asked about, not guessed.
                if (HasOtherActivityTarget(text, command, named.Count == 1 ? named[0] : null))
                {
                    string socialTarget = command == CompanionCommand.Search && named.Count == 1 ? WithoutStationNames(text, named[0]) : text;
                    if (SocialObject.IsMatch(socialTarget)) return false;
                    string clarify;
                    ApplyForSurface(actor, dialogue, CompanionCommand.Clarify, null, out clarify);
                    if (command == CompanionCommand.Search && dialogue != null) clarify = SearchClarification(dialogue, actor, text, clarify);
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
            ApplyForSurface(actor, dialogue, command, station, out line);
            if (string.IsNullOrWhiteSpace(line)) return false;
            // Started or refused, the answer is the companion's authored line,
            // never a model promise about something the game did not do.
            reply = new GeneratedChatReply { reply = line, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }
    }
}
