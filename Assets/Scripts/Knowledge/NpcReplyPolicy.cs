using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Core;

namespace MysteryGame.Knowledge
{
    public static class NpcReplyPolicy
    {
        public const string GeneratedTextContract =
            "ข้อความที่เป็นข้อเท็จจริงของห้องหรือเรื่องส่วนตัวที่อ้าง factId ต้องใช้ {fact:factId} " +
            "แทนการเขียนหรือเรียบเรียงข้อเท็จจริงเอง เช่น {fact:door_locked} เกมจะใส่ข้อความ canon ให้เอง " +
            "แต่ละ fact ต้องอยู่บรรทัดแยก ห้ามใส่คำอื่นก่อนหรือหลัง fact ในบรรทัดนั้น " +
            "ส่ง referencedFactIds ให้ตรงกับ fact ที่ใช้จริงทุกตัว ห้ามอ้าง id เปล่าเพื่อรับรองข้อความที่แต่งขึ้น " +
            "ข้อความนอกวงเล็บเขียนได้เฉพาะบทคุยทางสังคม ความรู้สึก และการตอบผู้เล่น " +
            "ห้ามกล่าวอ้างไอเท็ม ตำแหน่ง เบาะแส รหัส วิธีผ่านด่าน ประวัติ ครอบครัว หรือความลับของตัวละครนอก fact " +
            "ถ้าไม่มี fact ที่ยืนยันได้ ให้บอกว่าไม่รู้ ห้ามเดา";

        private static readonly Regex FactToken = new Regex(@"\{fact:(?<id>[^{}]+)\}", RegexOptions.IgnoreCase);
        private static readonly Regex GameplayDirection = new Regex(
            @"(?:ไป|ลอง|จง).{0,24}(?:ค้น|สำรวจ|ตรวจ|หยิบ|ไข|ปลด)|(?:อยู่|ซ่อน).{0,12}(?:ใต้|หลัง|ข้าง)|\p{Nd}");
        private static readonly Regex EnglishGameplayDirection = new Regex(
            @"\b(?:go|try|(?:you|we)\s+(?:should|must|need\s+to))\s+(?:to\s+|and\s+)?" +
            @"(?:inspect(?:ing)?|examin(?:e|ing)|search(?:ing)?|explor(?:e|ing)|investigat(?:e|ing)|read(?:ing)?)\b",
            RegexOptions.CultureInvariant);
        private static readonly string[] GameplayTerms = {
            "รหัส", "กุญแจ", "ลิ้นชัก", "เบาะแส", "ปลดล็อ", "เฉลย", "ทางออก", "ซ่อน", "ใต้เตียง",
            "เปิดประตู", "ไขประตู", "ล็อค", "ล็อก", "key", "keys", "keycode", "code", "codes", "password",
            "clue", "clues", "puzzle", "drawer", "drawers", "unlock", "lock", "locked", "door", "doors",
            "exit", "hidden", "hiding", "lever", "switch", "under the", "behind the", "secret passage"
        };
        private static readonly string[] ThaiDigits = { "ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า" };
        private static readonly string[] EnglishDigits = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        private static readonly Regex[] ThaiNumberPatterns = SpelledNumberPatterns(ThaiDigits, false);
        private static readonly Regex[] EnglishNumberPatterns = SpelledNumberPatterns(EnglishDigits, true);

        private static Regex[] SpelledNumberPatterns(string[] words, bool englishBoundaries)
        {
            var patterns = new Regex[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                var pattern = new StringBuilder();
                foreach (char c in words[i])
                {
                    if (pattern.Length > 0) pattern.Append(@"[\s\p{P}\p{S}]*");
                    pattern.Append(Regex.Escape(c.ToString()));
                }
                string source = pattern.ToString();
                if (englishBoundaries) source = @"(?<![a-z])" + source + @"(?![a-z])";
                patterns[i] = new Regex(source, RegexOptions.CultureInvariant);
            }
            return patterns;
        }

        public static GeneratedChatReply AnswerReply(NpcKnowledgeContext context, string message, GameState state)
        {
            if (context?.Npc == null) return null;
            foreach (var rule in context.Npc.fallbackReplies)
                if (rule != null && rule.answerPuzzle != null &&
                    rule.Matches(message, PlayerIntentClassifier.Classify(message), state))
                    return new GeneratedChatReply { reply = rule.replies[0],
                        referencedFactIds = Array.Empty<string>(), relationshipDelta = rule.relationshipDelta,
                        emotion = rule.emotion };
            return null;
        }
        // Gameplay hints are authored output, never unrestricted model paraphrases.
        public static GeneratedChatReply HintReply(NpcKnowledgeContext context)
        {
            if (context == null || !context.HasData || !context.PlayerAskedForHint) return null;
            string text = NpcKnowledgeContextBuilder.BuildOfflineReply(context);
            if (string.IsNullOrWhiteSpace(text)) text = context.CompletedSteps == context.TotalSteps
                ? "เราได้ทำครบทุกขั้นของห้องนี้แล้วค่ะ"
                : "ตอนนี้เรายังไม่มีคำใบ้ที่ยืนยันได้ ลองทบทวนบันทึกที่พบก่อนนะ";
            bool hint = context.CurrentStep != null &&
                !string.IsNullOrWhiteSpace(context.DeterministicHint);
            return new GeneratedChatReply
            {
                reply = text, referencedFactIds = Array.Empty<string>(),
                hintId = hint ? "hint." + context.Room.roomId + "." + context.CurrentStep.stepId +
                    "." + context.NpcId + "." + context.AllowedHintLevel : null
            };
        }

        public static bool Validate(NpcKnowledgeContext context, IEnumerable<string> references,
            IEnumerable<string> text, out string reason)
        {
            reason = null;
            if (context == null) { reason = "missing context"; return false; }
            if (references == null) { reason = "missing referencedFactIds"; return false; }
            var declared = new HashSet<string>(references, StringComparer.Ordinal);
            foreach (string id in declared)
                if (string.IsNullOrWhiteSpace(id)) { reason = "empty fact id"; return false; }
            if (!context.ValidateReferences(declared, out reason)) return false;
            if (text == null) { reason = "missing text"; return false; }
            var used = new HashSet<string>(StringComparer.Ordinal);
            var segments = new List<string>();
            var rawSocial = new StringBuilder();
            var expandedText = new StringBuilder();
            foreach (string value in text)
                if (value != null) segments.AddRange(value.Split('\n'));
            foreach (string line in segments)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string remainder = line;
                bool hasFact = false;
                foreach (Match token in FactToken.Matches(line))
                {
                    string id = token.Groups["id"].Value;
                    if (!declared.Contains(id) || string.IsNullOrWhiteSpace(Statement(context, id)))
                    { reason = "unverified fact token: " + id; return false; }
                    used.Add(id);
                    hasFact = true;
                    remainder = remainder.Replace(token.Value, string.Empty);
                }
                if (remainder.IndexOf("{fact", StringComparison.OrdinalIgnoreCase) >= 0)
                { reason = "malformed fact token"; return false; }

                // Keep exact authored statements compatible with older providers. Merely
                // attaching a valid ID to unrelated/paraphrased claims is not evidence.
                foreach (string id in declared)
                {
                    string statement = Statement(context, id);
                    if (!string.IsNullOrWhiteSpace(statement) && remainder.Contains(statement))
                    {
                        used.Add(id);
                        hasFact = true;
                        remainder = remainder.Replace(statement, string.Empty);
                    }
                }
                if (hasFact && !string.IsNullOrWhiteSpace(remainder))
                { reason = "authored facts must stand alone without model qualifiers"; return false; }
                if (ContainsGameplayClaim(context, remainder))
                { reason = "gameplay text must come from an authored fact"; return false; }
                rawSocial.AppendLine(remainder);

                string expanded = ExpandFacts(context, line);
                expandedText.AppendLine(expanded);
                // Defence in depth: punctuation, invisible separators and Thai/full-width
                // digits must not turn a protected answer into an unprotected spelling.
                if (ContainsWithheldRoomDetail(context, expanded, out reason)) return false;
            }
            // A protected answer may be distributed across otherwise harmless
            // lines, event lines or choices. Use the same normalization for the
            // assembled output as for each individual segment.
            string assembled = expandedText.ToString();
            // Instructions can be split between social lines or event choices.
            // Only inspect raw prose here, not already-verified canon facts.
            if (ContainsGameplayClaim(context, rawSocial.ToString()))
            { reason = "gameplay text must come from an authored fact"; return false; }
            if (ContainsWithheldRoomDetail(context, assembled, out reason)) return false;
            // The same protection applies to locked personal details.
            foreach (PersonalFact fact in context.LockedSecrets)
                if (ContainsPersonalDetail(assembled, fact))
                { reason = "locked personal fact: " + fact.factId; return false; }
            foreach (PersonalFact fact in context.ShareablePersonalFacts)
                if (ContainsPersonalDetail(rawSocial.ToString(), fact))
                { reason = "personal text must come from an authored fact: " + fact.factId; return false; }
            if (!declared.SetEquals(used)) { reason = "unused referencedFactIds"; return false; }
            return true;
        }

        private static bool ContainsWithheldRoomDetail(NpcKnowledgeContext context, string text, out string reason)
        {
            reason = null;
            foreach (RoomFact fact in context.WithheldFacts)
                if (fact != null && fact.protectedTerms != null)
                    foreach (string term in fact.protectedTerms)
                        if (!string.IsNullOrWhiteSpace(term) && ContainsProtectedTerm(text, term))
                        { reason = fact.factId; return true; }
            return false;
        }

        private static bool ContainsPersonalDetail(string text, PersonalFact fact)
        {
            if (fact == null) return false;
            if (!string.IsNullOrWhiteSpace(fact.statement) && ContainsProtectedTerm(text, fact.statement)) return true;
            if (fact.protectedTerms != null)
                foreach (string term in fact.protectedTerms)
                    if (!string.IsNullOrWhiteSpace(term) && ContainsProtectedTerm(text, term)) return true;
            return false;
        }

        private static string Statement(NpcKnowledgeContext context, string id)
        {
            foreach (RoomFact fact in context.KnownFacts)
                if (fact != null && fact.factId == id && !fact.isPuzzleAnswer && !fact.isPuzzleGuidance)
                    return fact.statement;
            foreach (PersonalFact fact in context.ShareablePersonalFacts)
                if (fact != null && fact.factId == id) return fact.statement;
            return null;
        }

        private static string ExpandFacts(NpcKnowledgeContext context, string text)
        {
            return FactToken.Replace(text ?? string.Empty, token => Statement(context, token.Groups["id"].Value) ?? string.Empty);
        }

        private static bool ContainsGameplayClaim(NpcKnowledgeContext context, string text)
        {
            string value = RemoveInvisibleCharacters(text).ToLowerInvariant();
            if (GameplayDirection.IsMatch(value) || EnglishGameplayDirection.IsMatch(value)) return true;
            foreach (string term in GameplayTerms)
                if (ContainsWord(value, term)) return true;
            if (context.Room != null && context.Room.gameplayTerms != null)
                foreach (string term in context.Room.gameplayTerms)
                    if (!string.IsNullOrWhiteSpace(term) && ContainsWord(value, term)) return true;
            return false;
        }

        private static bool ContainsWord(string text, string term)
        {
            term = RemoveInvisibleCharacters(term).ToLowerInvariant();
            // English words need boundaries: "key" inside "monkey" is not a claim.
            if (Regex.IsMatch(term, @"^[a-z ]+$"))
                return Regex.IsMatch(text, @"(?<![a-z])" + Regex.Escape(term) + @"(?![a-z])");
            return text.Contains(term);
        }

        private static string RemoveInvisibleCharacters(string text)
        {
            var value = new StringBuilder();
            string source = text ?? string.Empty;
            try { source = source.Normalize(NormalizationForm.FormKC); }
            catch (ArgumentException) { return source; }
            foreach (char c in source)
                if (char.GetUnicodeCategory(c) != UnicodeCategory.Format) value.Append(c);
            return value.ToString();
        }

        private static string ProtectedForm(string text, bool digitsSpelledOut)
        {
            string value = RemoveInvisibleCharacters(text).ToLowerInvariant();
            if (digitsSpelledOut)
                for (int i = 0; i < 10; i++)
                {
                    // Separators may occur inside a spelled digit as well as
                    // between digits. English boundaries still protect words
                    // such as "someone" from becoming a numeric claim.
                    string digit = i.ToString(CultureInfo.InvariantCulture);
                    value = ThaiNumberPatterns[i].Replace(value, digit);
                    value = EnglishNumberPatterns[i].Replace(value, digit);
                }
            var folded = new StringBuilder();
            foreach (char c in value)
            {
                if (char.IsDigit(c)) folded.Append((int)char.GetNumericValue(c));
                else if (char.IsLetter(c) || char.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    folded.Append(c);
            }
            return folded.ToString();
        }

        private static bool ContainsProtectedTerm(string text, string term)
        {
            bool numeric = Regex.IsMatch(ProtectedForm(term, false), @"^\d+$");
            string needle = ProtectedForm(term, numeric);
            return needle.Length > 0 && ProtectedForm(text, numeric).Contains(needle);
        }

        public static bool TryGroundReply(NpcKnowledgeContext context, GeneratedChatReply reply,
            out GeneratedChatReply grounded, out string reason)
        {
            grounded = null;
            reason = "missing or malformed reply";
            if (reply == null || !reply.IsValid() || reply.reply.Length > 1200 ||
                !Validate(context, reply.referencedFactIds, new[] { reply.reply }, out reason)) return false;
            grounded = new GeneratedChatReply { reply = ExpandFacts(context, reply.reply),
                referencedFactIds = reply.referencedFactIds, relationshipDelta = reply.relationshipDelta,
                emotion = reply.emotion, playerTone = reply.playerTone };
            return true;
        }

        public static bool ValidateEvent(NpcKnowledgeContext context, GeneratedDialogueContent content,
            out string reason)
        {
            reason = null;
            if (content == null) return false;
            var lines = new List<string>(content.lines ?? Array.Empty<string>());
            if (content.choices != null)
                foreach (var choice in content.choices)
                    if (choice != null) { lines.Add(choice.optionText); lines.Add(choice.responseText); }
            return Validate(context, content.referencedFactIds, lines, out reason);
        }

        public static bool TryGroundEvent(NpcKnowledgeContext context, GeneratedDialogueContent content,
            out GeneratedDialogueContent grounded, out string reason)
        {
            grounded = null;
            reason = "malformed event";
            if (content == null || content.choices == null || !content.IsValid(content.choices.Length)) return false;
            if (!ValidateEvent(context, content, out reason)) return false;
            var lines = new List<string>();
            foreach (string line in content.lines ?? Array.Empty<string>()) lines.Add(ExpandFacts(context, line));
            var choices = new List<GeneratedDialogueChoice>();
            foreach (var choice in content.choices ?? Array.Empty<GeneratedDialogueChoice>())
            {
                if (choice == null) { reason = "missing choice"; return false; }
                choices.Add(new GeneratedDialogueChoice { optionText = ExpandFacts(context, choice.optionText),
                    responseText = ExpandFacts(context, choice.responseText) });
            }
            grounded = new GeneratedDialogueContent { lines = lines.ToArray(), choices = choices.ToArray(),
                referencedFactIds = content.referencedFactIds };
            if (!grounded.IsValid(content.choices.Length))
            { grounded = null; reason = "expanded event exceeds display limits"; return false; }
            return true;
        }

        public static int RepetitionAdjustedGain(GameState state, string npcId, string message, int delta)
        {
            if (delta <= 0 || state == null) return delta;
            string normalized = InputPuzzleData.Normalize(message);
            foreach (var turn in state.GetConversationLog(npcId))
                if (turn != null && turn.SpeakerId == ConversationTurn.Player &&
                    InputPuzzleData.Normalize(turn.Text) == normalized) return 0;
            return delta;
        }
    }
}
