using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CompanionFramework
{
    public enum ReasoningQueryKind { None, Report, Hypothesis }
    public sealed class ReasoningQuery
    {
        public ReasoningQueryKind Kind;
        public string Topic;
        public List<ReasoningRule> Rules = new List<ReasoningRule>();
    }
    // Bounded phrase recognition. It never removes negation or other semantic
    // content to force a match, and unrelated social thoughts stay ordinary chat.
    public static class ReasoningQueryMatcher
    {
        private static readonly Regex Courtesy = new Regex(
            @"(?:\s*(?:หน่อย|นะ|ครับ|ค่ะ|คะ)|\s+please)[.!?…]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            try { text = text.Normalize(NormalizationForm.FormKC); } catch (ArgumentException) { return ""; }
            var result = new StringBuilder(text.Length);
            foreach (char c in text) if (char.GetUnicodeCategory(c) != UnicodeCategory.Format) result.Append(c);
            text = Regex.Replace(result.ToString().ToLowerInvariant(), @"\s+", " ").Trim().TrimEnd('.', '!', '?', '…').Trim();
            string previous;
            do { previous = text; text = Courtesy.Replace(text, "").Trim().TrimEnd('.', '!', '?', '…', ',').Trim(); } while (text != previous);
            return text;
        }
        private static bool Word(char c) => c <= 127 && char.IsLetterOrDigit(c);
        private static bool Starts(string text, string phrase) =>
            text.StartsWith(phrase, StringComparison.Ordinal) &&
            (text.Length == phrase.Length || !Word(phrase[phrase.Length - 1]) || !Word(text[phrase.Length]));
        private static bool Ends(string text, string phrase) =>
            text.EndsWith(phrase, StringComparison.Ordinal) &&
            (text.Length == phrase.Length || !Word(phrase[0]) || !Word(text[text.Length - phrase.Length - 1]));
        private static IEnumerable<string> Phrases(List<string> phrases) =>
            (phrases ?? new List<string>()).Select(Normalize).Where(p => p.Length > 0).OrderByDescending(p => p.Length);
        public static ReasoningQuery Read(ReasoningProfile profile, string message)
        {
            var query = new ReasoningQuery();
            string text = Normalize(message);
            if (profile == null || text.Length == 0) return query;
            if (Phrases(profile.reportPhrases).Contains(text))
            { query.Kind = ReasoningQueryKind.Report; return query; }
            query.Rules = FindRules(profile, text);
            if (query.Rules.Count > 0)
            { query.Kind = ReasoningQueryKind.Hypothesis; query.Topic = text; return query; }
            bool prefixed = false;
            foreach (string prefix in Phrases(profile.hypothesisPrefixes))
                if (Starts(text, prefix)) { text = text.Substring(prefix.Length).Trim(); prefixed = true; break; }
            bool questioned = false;
            foreach (string suffix in Phrases(profile.hypothesisSuffixes))
                if (Ends(text, suffix)) { text = text.Substring(0, text.Length - suffix.Length).Trim(); questioned = true; break; }
            query.Topic = text;
            query.Rules = FindRules(profile, text);
            bool relevant = Phrases(profile.topicTerms).Any(term =>
                Regex.IsMatch(text, (Word(term[0]) ? @"(?<![a-z0-9])" : "") + Regex.Escape(term) +
                    (Word(term[term.Length - 1]) ? @"(?![a-z0-9])" : "")));
            if (query.Rules.Count > 0 || relevant && (prefixed || questioned)) query.Kind = ReasoningQueryKind.Hypothesis;
            return query;
        }
        private static List<ReasoningRule> FindRules(ReasoningProfile profile, string text) =>
            (profile.rules ?? new List<ReasoningRule>()).Where(r => r != null && r.phrases != null &&
                r.phrases.Any(p => !string.IsNullOrWhiteSpace(p) && Normalize(p) == text)).ToList();
    }
}
