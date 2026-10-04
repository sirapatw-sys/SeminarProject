using System;
using System.Collections.Generic;
using MysteryGame.Core;

namespace MysteryGame.Knowledge
{
    public static class NpcReplyPolicy
    {
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
            if (!context.ValidateReferences(references, out reason)) return false;
            foreach (string line in text)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                // A lexical safety net; does not claim to detect every semantic hallucination.
                foreach (RoomFact fact in context.WithheldFacts)
                    if (fact != null && fact.protectedTerms != null)
                        foreach (string term in fact.protectedTerms)
                            if (!string.IsNullOrWhiteSpace(term) &&
                                InputPuzzleData.Normalize(line).Contains(InputPuzzleData.Normalize(term)))
                            { reason = fact.factId; return false; }
            }
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
