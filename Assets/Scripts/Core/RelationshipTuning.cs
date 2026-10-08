using UnityEngine;

namespace MysteryGame.Core
{
    /// <summary>
    /// How fast the player-to-NPC relationship moves, in one place. Being
    /// kind should pay off within a few conversations, so gains are larger
    /// than the raw authored numbers; losses are left as authored.
    /// </summary>
    public static class RelationshipTuning
    {
        /// <summary>Most one typed message can raise it (AI or offline).</summary>
        public const int MaxGainPerMessage = 10;

        /// <summary>Most one typed message can lower it.</summary>
        public const int MaxLossPerMessage = 15;

        /// <summary>Authored gains on dialogue and event choices count this much more.</summary>
        public const float ChoiceGainScale = 1.5f;

        /// <summary>The offline replies' authored gains (+1 to +4) count this much more.</summary>
        public const float OfflineGainScale = 2f;

        /// <summary>Scales a gain, rounding up; a loss or zero passes through unchanged.</summary>
        public static int ScaleGain(int amount, float scale)
        {
            return amount > 0 ? Mathf.CeilToInt(amount * scale) : amount;
        }

        /// <summary>A typed message's change, kept inside the per-message limits.</summary>
        public static int ClampMessage(int amount)
        {
            return Mathf.Clamp(amount, -MaxLossPerMessage, MaxGainPerMessage);
        }

        // The AI names the player's tone as well as a number; the number is
        // kept inside that tone's range, so "cold" can never come out positive.
        // The prompt quotes these same ranges.
        public static readonly string[] Tones = { "hostile", "cold", "neutral", "friendly", "kind" };

        public static bool TryGetToneRange(string tone, out int min, out int max)
        {
            switch ((tone ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "hostile": min = -15; max = -5; return true;
                case "cold": min = -8; max = -2; return true;
                case "neutral": min = 0; max = 1; return true;
                case "friendly": min = 1; max = 6; return true;
                case "kind": min = 4; max = 10; return true;
                default: min = -MaxLossPerMessage; max = MaxGainPerMessage; return false;
            }
        }

        /// <summary>
        /// The change one typed message makes, from the AI's reading (its tone
        /// and number) and the rules' reading (PlayerToneClassifier).
        ///
        /// The AI reads nuance the rules cannot, and the rules catch what the
        /// AI talks itself out of ("แย่มาก" read as sympathy). So:
        ///   * the AI's number is held to the tone it named;
        ///   * rules say rude, AI neutral or unsure: the rules' cap applies
        ///     (-5 hostile, -2 cold);
        ///   * rules say rude, AI says friendly: they disagree, the message
        ///     counts nothing — no reward for what may be rude, and a rules
        ///     misfire on a friendly line costs its gain, not a penalty;
        ///   * rules say friendly, AI neutral or unsure: a small floor;
        ///   * rules say friendly, AI says rude: the AI stands (sarcasm the
        ///     rules did not catch).
        /// With no AI (tone null) the rules' reading is the whole judgement.
        /// </summary>
        public static int Judge(int delta, string aiTone, ToneReading rules)
        {
            int min, max;
            bool aiKnown = TryGetToneRange(aiTone, out min, out max);
            if (aiKnown)
            {
                delta = Mathf.Clamp(delta, min, max);
            }

            bool aiPositive = aiKnown && min > 0;
            bool aiNegative = aiKnown && max < 0;

            if (rules.IsNegative)
            {
                int cap = rules.Tone == PlayerTone.Hostile ? -5 : -2;
                delta = aiPositive ? Mathf.Min(delta, 0) : Mathf.Min(delta, cap);
            }
            else if (rules.IsPositive && !aiNegative)
            {
                delta = Mathf.Max(delta, rules.Tone == PlayerTone.Kind ? 2 : 1);
            }

            return ClampMessage(delta);
        }
    }
}
