using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Knowledge
{
    /// <summary>A fact this NPC only learns once something happens.</summary>
    [Serializable]
    public class KnowledgeGrant
    {
        public string factId;
        public List<ConditionRule> when = new List<ConditionRule>();

        public bool IsGranted(GameState state)
        {
            if (when == null || when.Count == 0)
            {
                return true;
            }

            foreach (ConditionRule rule in when)
            {
                if (rule != null && !rule.Evaluate(state))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>How the NPC behaves inside one relationship band.</summary>
    [Serializable]
    public class RelationshipTone
    {
        [Tooltip("Applies while relationship <= this value.")]
        public int upTo = 100;

        [TextArea(2, 4)]
        public string tone;

        [Tooltip("Caps how explicit this NPC may be at this relationship.")]
        public HintLevel maxHintLevel = HintLevel.Normal;
    }

    /// <summary>
    /// Who an NPC is, what they know, and what they are never allowed to say.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NewNpcProfile",
        menuName = "Game/NPC Profile Data"
    )]
    public class NpcProfileData : ScriptableObject
    {
        [Tooltip("Must match DialogueData.speakerId, e.g. Alice.")]
        public string npcId;

        public string displayName;

        [TextArea(3, 8)] public string persona;
        [TextArea(2, 5)] public string speechStyle;
        [TextArea(2, 4)] public string personalGoal;

        [Header("Knowledge")]
        [Tooltip("Facts this NPC knows from the very start.")]
        public List<string> knownFactIds = new List<string>();

        [Tooltip("Facts this NPC picks up once a condition is met.")]
        public List<KnowledgeGrant> learnedFacts = new List<KnowledgeGrant>();

        [Tooltip("Facts this NPC must never reveal, even when they know them.")]
        public List<string> forbiddenFactIds = new List<string>();

        [Header("Hinting")]
        [Tooltip("Off for gatekeepers like Sena, who berate instead of helping.")]
        public bool givesHints = true;

        [TextArea(2, 4)]
        [Tooltip("Used verbatim as the intent when givesHints is off.")]
        public string refuseHintLine;

        public List<RelationshipTone> tones = new List<RelationshipTone>();

        public bool KnowsFact(string factId, GameState state)
        {
            if (string.IsNullOrWhiteSpace(factId))
            {
                return false;
            }

            if (knownFactIds != null && knownFactIds.Contains(factId))
            {
                return true;
            }

            if (learnedFacts == null)
            {
                return false;
            }

            foreach (KnowledgeGrant grant in learnedFacts)
            {
                if (grant != null && grant.factId == factId &&
                    grant.IsGranted(state))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsForbidden(string factId)
        {
            return forbiddenFactIds != null && forbiddenFactIds.Contains(factId);
        }

        public RelationshipTone ToneFor(int relationship)
        {
            RelationshipTone best = null;
            foreach (RelationshipTone candidate in tones)
            {
                if (candidate == null || relationship > candidate.upTo)
                {
                    continue;
                }

                if (best == null || candidate.upTo < best.upTo)
                {
                    best = candidate;
                }
            }

            return best;
        }
    }
}
