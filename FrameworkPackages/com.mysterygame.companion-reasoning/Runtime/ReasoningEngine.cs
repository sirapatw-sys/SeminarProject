using System;
using System.Collections.Generic;
using System.Linq;

namespace CompanionFramework
{
    // Supply a fresh snapshot per reply. This engine never grants evidence or executes AI proposals.
    public interface IReasoningWorld
    {
        bool KnowsFact(string factId);
        bool HasFlag(string flagId);
        int AllowedDetail { get; }
    }

    [Serializable]
    public sealed class ReasoningRule
    {
        public string id;
        public List<string> phrases = new List<string>();
        public List<string> requiredFacts = new List<string>();
        public List<string> requiredFlags = new List<string>();
        public List<string> excludedFlags = new List<string>();
        public string cautiousLine;
        public string normalLine;
        public string detailedLine;
        public string insufficientLine;
    }
    public enum ReasoningOutcome { Unavailable, InsufficientEvidence, Consideration }
    public sealed class ReasoningResult
    {
        public ReasoningOutcome Outcome { get; internal set; }
        public string Line { get; internal set; }
        public string[] ReferencedFacts { get; internal set; } = Array.Empty<string>();
    }
    public static class ReasoningEngine
    {
        public static ReasoningResult Evaluate(ReasoningRule rule, IReasoningWorld world)
        {
            var result = new ReasoningResult { Outcome = ReasoningOutcome.Unavailable };
            if (world == null || rule == null || string.IsNullOrWhiteSpace(rule.id) ||
                rule.requiredFacts == null || rule.requiredFacts.Count == 0 ||
                rule.requiredFacts.Any(string.IsNullOrWhiteSpace) ||
                (rule.requiredFlags != null && rule.requiredFlags.Any(f => string.IsNullOrWhiteSpace(f) || !world.HasFlag(f))) ||
                (rule.excludedFlags != null && rule.excludedFlags.Any(f => string.IsNullOrWhiteSpace(f) || world.HasFlag(f))))
                return result;
            if (!rule.requiredFacts.All(world.KnowsFact))
            {
                result.Outcome = ReasoningOutcome.InsufficientEvidence;
                result.Line = rule.insufficientLine;
                return result;
            }
            if (world.AllowedDetail <= 0) return result;
            result.Line = world.AllowedDetail >= 3 && !string.IsNullOrWhiteSpace(rule.detailedLine)
                ? rule.detailedLine : world.AllowedDetail >= 2 && !string.IsNullOrWhiteSpace(rule.normalLine)
                    ? rule.normalLine : rule.cautiousLine;
            if (string.IsNullOrWhiteSpace(result.Line)) return result;
            result.Outcome = ReasoningOutcome.Consideration;
            result.ReferencedFacts = rule.requiredFacts.ToArray();
            return result;
        }
    }

    // Invoke only from a verified host interaction, never from model text.
    public sealed class CompanionEffectRegistry
    {
        private readonly Dictionary<string, Func<bool>> handlers = new Dictionary<string, Func<bool>>(StringComparer.Ordinal);
        public void Register(string id, Func<bool> handler)
        {
            if (string.IsNullOrWhiteSpace(id) || handler == null) throw new ArgumentException("Effect needs an ID and handler.");
            if (handlers.ContainsKey(id)) throw new ArgumentException("Duplicate effect: " + id);
            handlers.Add(id, handler);
        }
        public bool TryExecute(string id)
        {
            Func<bool> handler;
            return id != null && handlers.TryGetValue(id, out handler) && handler();
        }
    }
}
