using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace CompanionFramework
{
    [CreateAssetMenu(menuName = "Companion Framework/Reasoning Profile")]
    public sealed class ReasoningProfile : ScriptableObject
    {
        public string npcId;
        [TextArea] public string insufficientEvidenceLine = "I do not have enough evidence to judge that yet.";
        [TextArea] public string clarifyLine = "Which observation supports that idea?";
        [Tooltip("Shared across hypotheses so cautious replies cannot confirm an answer by comparison.")]
        [TextArea] public string cautiousLine = "We have observations, but should check assumptions before concluding.";
        [TextArea] public string unavailableLine = "That idea does not apply to our current step. What should we examine next?";
        public List<string> reportPhrases = new List<string> {
            "เธอเจออะไรบ้าง", "เธอพบอะไรบ้าง", "เจออะไรไหม", "เธอรู้อะไรบ้าง", "what did you find", "what do you know" };
        public List<string> hypothesisPrefixes = new List<string> {
            "เราคิดว่า", "ฉันคิดว่า", "คิดว่า", "ถ้า", "i think", "maybe" };
        public List<string> hypothesisSuffixes = new List<string> {
            "เธอว่าไง", "เธอคิดว่าไง", "ใช่ไหม", "ดีไหม", "what do you think" };
        [Tooltip("Specific puzzle topics for asking back on unknown hypotheses. Do not include generic social words.")]
        public List<string> topicTerms = new List<string>();
        public List<ReasoningRule> rules = new List<ReasoningRule>();

        public ReasoningResult Evaluate(ReasoningRule rule, IReasoningWorld world) =>
            ReasoningEngine.Evaluate(rule, world, cautiousLine);
        public IEnumerable<string> SpokenLines()
        {
            foreach (string line in new[] { insufficientEvidenceLine, clarifyLine, cautiousLine, unavailableLine }) yield return line;
            if (rules != null) foreach (var rule in rules) if (rule != null)
                foreach (string line in new[] { rule.cautiousLine, rule.normalLine, rule.detailedLine, rule.insufficientLine }) yield return line;
        }

        public List<string> ValidateConfiguration(Func<string, bool> factAllowed = null)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(npcId)) errors.Add("Reasoning profile needs an NPC ID.");
            if (new[] { insufficientEvidenceLine, clarifyLine, cautiousLine, unavailableLine }.Any(string.IsNullOrWhiteSpace))
                errors.Add("Reasoning profile needs nonempty safe fallback lines.");
            foreach (var values in new[] { reportPhrases, hypothesisPrefixes, hypothesisSuffixes, topicTerms })
                if (values != null && values.Any(string.IsNullOrWhiteSpace)) errors.Add("Reasoning language contains an empty phrase.");
            if (rules == null || rules.Count == 0) { errors.Add("Reasoning profile needs rules."); return errors; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var phrases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.id) || !ids.Add(rule.id))
                { errors.Add("Reasoning rule ID is empty/duplicate."); continue; }
                if (rule.requiredFacts == null || rule.requiredFacts.Count == 0 ||
                    rule.requiredFacts.Any(f => string.IsNullOrWhiteSpace(f) || factAllowed != null && !factAllowed(f)))
                    errors.Add("Reasoning rule references missing or unsafe evidence: " + rule.id);
                if (rule.phrases == null || rule.phrases.Count == 0)
                    errors.Add("Reasoning rule needs phrases: " + rule.id);
                else foreach (string phrase in rule.phrases)
                    if (string.IsNullOrWhiteSpace(phrase) || !phrases.Add(ReasoningQueryMatcher.Normalize(phrase)))
                        errors.Add("Reasoning phrase is empty/ambiguous: " + rule.id);
                if (string.IsNullOrWhiteSpace(rule.insufficientLine)) errors.Add("Missing insufficient-evidence line: " + rule.id);
                if ((rule.requiredFlags != null && rule.requiredFlags.Any(string.IsNullOrWhiteSpace)) ||
                    (rule.excludedFlags != null && rule.excludedFlags.Any(string.IsNullOrWhiteSpace)) ||
                    (rule.requiredFlags != null && rule.excludedFlags != null && rule.requiredFlags.Intersect(rule.excludedFlags).Any()))
                    errors.Add("Reasoning rule flags are empty or contradictory: " + rule.id);
            }
            return errors;
        }
    }
}
