using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Knowledge;
using MysteryGame.Core;

namespace MysteryGame.Companions
{
    public enum CompanionCommand { None, Follow, Wait, Help, Comfort, Clarify }

    /// <summary>
    /// A deliberately bounded language adapter, not an AI tool executor.
    /// Parsing/knowledge construction is pure; only SendTypedMessage may apply a request.
    /// </summary>
    public static class CompanionDialogueCommands
    {
        private static Regex R(string pattern) { return new Regex(Normalize(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }
        private static readonly Regex ReportOrQuestion = R(
            @"ยังไง|อย่างไร|วิธี|ทำไม|คืออะไร|ทำอะไร|ช่วย(?:บอก|อธิบาย|เล่า|ตอบ)|บอก(?:หน่อย|ที|ได้ไหม)|หรือยัง|แล้วหรือ|อยู่ไหม|สถานะ|เคย|เมื่อกี้|เมื่อวาน|บอกว่า|พูดว่า|ถามว่า|คำว่า|ตัวอย่าง|สมมติ|ถ้า|หาก|กำลัง|" +
            @"ชอบเวลา|คิดว่า|จำได้ว่า|หมายถึง|แปลว่า|ผลที่ตามมา|(?:ฉัน|เรา|ผม)จะช่วย|อยากให้(?:ฉัน|เรา|ผม)|" +
            @"\b(?:how|why|what|when|whether|tell|explain|describe|understand|did|was|were|status|already|yet|earlier|yesterday|said|says|say|if|suppose|pretend|imagine)\b|\bi\s+(?:like|think|remember|will|can)\b");
        private static readonly Regex Decline = R(
            @"อย่า|ห้าม|ไม่(?:ต้อง|อยาก|เอา|ขอ|ต้องการ|ได้|ควร|จำเป็น|พร้อม|ตกลง|ใช่)|\b(?:don't|dont|do\s+not|never|not)\b");
        private static readonly Regex Follow = R(
            @"(?:เดิน|ไป|มา)(?:สำรวจ)?(?:ไป|มา)?ด้วยกัน|(?:เดิน)?ตาม(?:เรา|ฉัน|ผม|มา)|" +
            @"\b(?:follow\s+(?:me|us)|(?:come|walk|go)\s+with\s+(?:me|us))\b");
        private static readonly Regex Wait = R(
            @"รอ(?:อยู่)?(?:ตรงนี้|ที่นี่|เรา|ฉัน|ผม)|หยุด(?:เดิน)?ตาม|ไม่ต้อง(?:เดิน)?ตาม(?:มา|เรา|ฉัน|ผม)?(?:แล้ว|ก่อน)|" +
            @"\b(?:wait\s+(?:here|for\s+(?:me|us))|stay\s+here|stop\s+following(?:\s+(?:me|us))?)\b");
        private static readonly Regex Help = R(
            @"ช่วย|(?:เปิด|จับ).{0,40}(?:ให้หน่อย|ให้ที|ได้ไหม|ได้หรือเปล่า|กันไหม)|" +
            @"\b(?:help|please\s+(?:open|hold)|(?:can|could|would)\s+you\s+(?:open|hold))\b");
        private static readonly Regex Comfort = R(
            @"ไม่ต้อง(?:กลัว|ห่วง)|ไม่เป็นไรนะ|(?:เรา|ฉัน|ผม)อยู่(?:ตรงนี้|นี่|ด้วย)|อยู่ด้วยกันนะ|" +
            @"\b(?:don't\s+worry|do\s+not\s+worry|i(?:'m|\s+am)\s+here\s+for\s+you|you(?:'re|\s+are)\s+not\s+alone)\b");
        private static readonly Regex OtherTarget = R(
            @"ประตู|ลิ้นชัก|กุญแจ|รหัส|ภาพวาด|\b(?:door|drawer|key|code|painting)\b");
        private static readonly Regex Quoted = new Regex("[\"“”«»`]|‘[^’]+’|(?:^|\\s)'[^']+'(?:\\s|$)");

        public static IEnumerable<string> RequestTopicsFor(string message)
        {
            string text = Normalize(message);
            foreach (var pattern in new[] { Follow, Wait, Comfort })
                foreach (Match match in pattern.Matches(text)) yield return match.Value;
        }

        public static CompanionCommand Parse(string message, CompanionScenarioData scenario, out CompanionStationData station)
        {
            station = null;
            if (scenario == null || string.IsNullOrWhiteSpace(message) || message.Length > 1000) return CompanionCommand.None;
            string text = Normalize(message);
            // Quoted/reported, hypothetical and information-only language never starts work.
            if (Quoted.IsMatch(message) || ReportOrQuestion.IsMatch(text)) return CompanionCommand.None;
            var matching = new List<CompanionStationData>();
            if (scenario.stations != null) foreach (var item in scenario.stations)
                if (item != null && Mentions(text, item)) matching.Add(item);
            bool wait = Wait.IsMatch(text);
            bool comfort = Comfort.IsMatch(text);
            // Comfort includes "don't worry"; a stop request may include "ไม่ต้องตามแล้ว".
            string requests = Comfort.Replace(Wait.Replace(text, " "), " ");
            if (Decline.IsMatch(requests)) return CompanionCommand.None;
            bool follow = Follow.IsMatch(Wait.Replace(text, " "));
            bool help = matching.Count > 0 && Help.IsMatch(text);
            int count = (wait ? 1 : 0) + (comfort ? 1 : 0) + (follow ? 1 : 0) + (help ? 1 : 0);
            if (count > 1 || (help && (matching.Count != 1 || OtherTarget.IsMatch(PlayerIntentClassifier.WithoutActivityLocations(text))))) return CompanionCommand.Clarify;
            if (help) { station = matching[0]; return CompanionCommand.Help; }
            if (follow) return CompanionCommand.Follow;
            if (wait) return CompanionCommand.Wait;
            if (comfort) return CompanionCommand.Comfort;
            return CompanionCommand.None;
        }

        private static bool Mentions(string text, CompanionStationData station)
        {
            var names = new List<string> { station.title };
            if (station.gameplayTerms != null) names.AddRange(station.gameplayTerms);
            foreach (string name in names)
            {
                string term = Normalize(name).Trim(); if (term.Length == 0) continue;
                string pattern = Regex.Escape(term);
                if (Regex.IsMatch(term, @"^[a-z ]+$")) pattern = @"(?<![a-z])" + pattern + @"(?![a-z])";
                if (Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant)) return true;
            }
            return false;
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
            var knowledge = AiDialogueGenerator.BuildKnowledgeContext(dialogue.ActiveNpcId, message);
            if (knowledge.PlayerAskedForHint) return false;
            CompanionStationData station;
            CompanionCommand command = Parse(message, actor.Scenario, out station);
            if (command == CompanionCommand.None) return false;
            string text;
            actor.ApplyDialogueCommand(dialogue, command, station, out text);
            reply = new GeneratedChatReply { reply = text, relationshipDelta = 0, referencedFactIds = Array.Empty<string>() };
            return true;
        }
    }
}
