using System.Collections.Generic;
using MysteryGame.Core;

namespace MysteryGame.Knowledge
{
    /// <summary>
    /// What an NPC says when the AI is unavailable. Everything character
    /// specific comes from NpcProfileData, so adding an NPC never means
    /// editing this file or DialogueManager.
    ///
    /// Order: the NPC's own rules (top to bottom, first match wins), then the
    /// room's hint ladder if the player asked for help, then the NPC's neutral
    /// lines, then a generic line.
    /// </summary>
    public static class NpcOfflineReplies
    {
        public const string GenericNeutral = "...อืม";

        public static GeneratedChatReply Build(
            NpcKnowledgeContext knowledge,
            string playerMessage,
            GameState state)
        {
            string message = playerMessage ?? string.Empty;
            PlayerIntent intent = PlayerIntentClassifier.Classify(message);
            NpcProfileData npc = knowledge != null ? knowledge.Npc : null;
            int rotation = state != null && knowledge != null
                ? state.GetConversationCount(knowledge.NpcId) +
                  state.GetConversationLog(knowledge.NpcId).Count
                : 0;

            FallbackReplyRule rule = FirstMatch(
                npc != null ? npc.fallbackReplies : null, message, intent, state);
            if (rule != null)
            {
                return new GeneratedChatReply
                {
                    reply = Pick(rule.replies, rotation),
                    relationshipDelta = rule.relationshipDelta,
                    emotion = rule.emotion,
                };
            }

            if (knowledge != null && knowledge.HasData)
            {
                string authored = NpcKnowledgeContextBuilder.BuildOfflineReply(knowledge);
                if (!string.IsNullOrWhiteSpace(authored))
                {
                    return new GeneratedChatReply
                    {
                        reply = authored,
                        relationshipDelta =
                            knowledge.AllowedHintLevel == HintLevel.None ? -5 : 2,
                    };
                }
            }

            string neutral = npc != null ? Pick(npc.neutralReplies, rotation) : null;
            return new GeneratedChatReply
            {
                reply = string.IsNullOrWhiteSpace(neutral) ? GenericNeutral : neutral,
                relationshipDelta = 0,
            };
        }

        /// <summary>The opening line when the player comes back, or null.</summary>
        public static string ReturnGreeting(NpcProfileData npc, GameState state)
        {
            if (npc == null)
            {
                return null;
            }

            FallbackReplyRule rule = FirstMatch(
                npc.returnGreetings, string.Empty, PlayerIntent.None, state);
            int rotation = state != null ? state.GetConversationCount(npc.npcId) : 0;
            return rule != null ? Pick(rule.replies, rotation) : null;
        }

        public static FallbackReplyRule FirstMatch(
            List<FallbackReplyRule> rules,
            string message,
            PlayerIntent intent,
            GameState state)
        {
            if (rules == null)
            {
                return null;
            }

            foreach (FallbackReplyRule rule in rules)
            {
                if (rule != null && rule.Matches(message, intent, state))
                {
                    return rule;
                }
            }

            return null;
        }

        private static string Pick(List<string> options, int rotation)
        {
            if (options == null || options.Count == 0)
            {
                return null;
            }

            int index = rotation % options.Count;
            if (index < 0)
            {
                index += options.Count;
            }

            return options[index];
        }
    }
}
