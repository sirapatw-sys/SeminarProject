using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Knowledge
{
    /// <summary>How explicit an NPC is allowed to be when hinting.</summary>
    public enum HintLevel
    {
        None = 0,
        Vague = 1,
        Normal = 2,
        Explicit = 3,
    }

    /// <summary>One looping sound layer of a room.</summary>
    [Serializable]
    public class RoomSound
    {
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 0.5f;
    }

    /// <summary>
    /// One canonical statement about the room. An NPC may only ever repeat a
    /// fact whose reveal conditions are already satisfied, which is what stops
    /// the model inventing (or leaking) puzzle state.
    /// </summary>
    [Serializable]
    public class RoomFact
    {
        public string factId;

        [TextArea(2, 4)]
        public string statement;

        [Tooltip("All conditions must hold before anyone may state this fact.")]
        public List<ConditionRule> revealedWhen = new List<ConditionRule>();

        [Tooltip("Marks the fact that literally answers the room's puzzle.")]
        public bool isPuzzleAnswer;

        public bool IsRevealed(GameState state)
        {
            if (revealedWhen == null || revealedWhen.Count == 0)
            {
                return true;
            }

            foreach (ConditionRule rule in revealedWhen)
            {
                if (rule != null && !rule.Evaluate(state))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// One rung of the room's solution ladder. The hint wordings live here so
    /// that "what do I do next" is answered from data, not from the model.
    /// </summary>
    [Serializable]
    public class PuzzleStep
    {
        public string stepId;

        [TextArea(2, 4)]
        public string summary;

        [Tooltip(
            "InteractionData.interactionId that performs this step, if an " +
            "object does. Empty for steps driven by code (talking to Sena). " +
            "The edit-mode tests check the two stay in agreement."
        )]
        public string interactionId;

        public List<ConditionRule> availableWhen = new List<ConditionRule>();
        public List<ConditionRule> completedWhen = new List<ConditionRule>();

        [TextArea(2, 4)] public string vagueHint;
        [TextArea(2, 4)] public string normalHint;
        [TextArea(2, 4)] public string explicitHint;

        public bool IsAvailable(GameState state)
        {
            return Evaluate(availableWhen, state, true);
        }

        public bool IsCompleted(GameState state)
        {
            return Evaluate(completedWhen, state, false);
        }

        public string HintFor(HintLevel level)
        {
            switch (level)
            {
                case HintLevel.Explicit:
                    return Pick(explicitHint, normalHint, vagueHint);
                case HintLevel.Normal:
                    return Pick(normalHint, vagueHint, explicitHint);
                case HintLevel.Vague:
                    return Pick(vagueHint, normalHint, explicitHint);
                default:
                    return string.Empty;
            }
        }

        private static string Pick(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static bool Evaluate(
            List<ConditionRule> rules,
            GameState state,
            bool valueWhenEmpty)
        {
            if (rules == null || rules.Count == 0)
            {
                return valueWhenEmpty;
            }

            foreach (ConditionRule rule in rules)
            {
                if (rule != null && !rule.Evaluate(state))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Everything that is true about one room, authored as data so new rooms
    /// no longer require editing the prompt builder.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewRoomKnowledge",
        menuName = "Game/Room Knowledge Data"
    )]
    public class RoomKnowledgeData : ScriptableObject
    {
        [Tooltip("Must match the scene name, e.g. Room01.")]
        public string roomId;

        public string roomName;

        [TextArea(2, 5)]
        public string roomDescription;

        [Tooltip(
            "The illustrated background for this room. Lives here so one room " +
            "owns one background; the copies on each scene's " +
            "RoomVisualController are only a fallback for rooms with no " +
            "knowledge asset."
        )]
        public Sprite background;

        [Header("Audio")]
        [Tooltip("Looping layers (music, rain, clock ticking) played while in this room.")]
        public List<RoomSound> sounds = new List<RoomSound>();

        [Tooltip("Adds the synthesised haunted drone with far-off music-box notes.")]
        public bool hauntedDrone;

        public List<RoomFact> facts = new List<RoomFact>();
        public List<PuzzleStep> steps = new List<PuzzleStep>();

        public RoomFact FindFact(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (RoomFact fact in facts)
            {
                if (fact != null && fact.factId == id)
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>The first step the player can act on but has not finished.</summary>
        public PuzzleStep CurrentStep(GameState state)
        {
            foreach (PuzzleStep step in steps)
            {
                if (step == null || step.IsCompleted(state))
                {
                    continue;
                }

                if (step.IsAvailable(state))
                {
                    return step;
                }
            }

            return null;
        }

        public int CompletedStepCount(GameState state)
        {
            int done = 0;
            foreach (PuzzleStep step in steps)
            {
                if (step != null && step.IsCompleted(state))
                {
                    done++;
                }
            }

            return done;
        }
    }
}
