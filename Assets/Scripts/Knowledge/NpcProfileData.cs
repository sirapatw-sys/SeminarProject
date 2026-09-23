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
            return ConditionRule.AllHold(when, state);
        }
    }

    /// <summary>
    /// Something about the NPC herself rather than the room: a piece of
    /// backstory she will tell when asked, or a secret she only lets slip once
    /// <see cref="KnowledgeGrant.when"/> holds. factId doubles as the id the
    /// model cites in referencedFactIds.
    /// </summary>
    [Serializable]
    public class PersonalFact : KnowledgeGrant
    {
        [TextArea(2, 5)]
        public string statement;
    }

    /// <summary>Extra direction for the model that only applies in some states.</summary>
    [Serializable]
    public class SituationalNote
    {
        public List<ConditionRule> when = new List<ConditionRule>();

        [TextArea(2, 5)]
        public string note;

        public bool IsActive(GameState state)
        {
            return ConditionRule.AllHold(when, state);
        }
    }

    /// <summary>How this NPC feels about another NPC.</summary>
    [Serializable]
    public class NpcBond
    {
        public string otherNpcId;

        [Range(0, 100)]
        public int initialValue = 50;

        [TextArea(2, 4)]
        public string description;
    }

    /// <summary>A face shown in the dialogue's emotion box.</summary>
    [Serializable]
    public class EmotionPortrait
    {
        [Tooltip("Tag used in lines as [:emotionId] and by the AI's emotion field.")]
        public string emotionId;

        [Tooltip("What this face means, shown to the model so it picks well.")]
        public string meaning;

        public Sprite sprite;
    }

    /// <summary>
    /// One offline reaction. Rules are tried top to bottom and the first one
    /// that matches wins, so specific rules go above general ones.
    /// </summary>
    [Serializable]
    public class FallbackReplyRule
    {
        public string ruleId;

        [Tooltip("Must be present in the message. None matches any message.")]
        public PlayerIntent intent = PlayerIntent.None;

        [Tooltip("Any one of these must appear in the message. Empty means no keyword check.")]
        public List<string> keywords = new List<string>();

        public List<ConditionRule> when = new List<ConditionRule>();

        [TextArea(2, 4)]
        [Tooltip("Rotated by conversation count so repeats vary.")]
        public List<string> replies = new List<string>();

        [Range(-15, 5)]
        public int relationshipDelta;

        public string emotion;

        public bool Matches(string message, PlayerIntent detected, GameState state)
        {
            if (replies == null || replies.Count == 0)
            {
                return false;
            }

            if (intent != PlayerIntent.None && (detected & intent) == 0)
            {
                return false;
            }

            if (keywords != null && keywords.Count > 0 &&
                !PlayerIntentClassifier.ContainsAny(message, keywords.ToArray()))
            {
                return false;
            }

            return ConditionRule.AllHold(when, state);
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

        [Header("Presentation")]
        [Tooltip("Dialogue portrait, used for lines that switch speaker with [NpcId].")]
        public Sprite portrait;

        [Range(0, 100)]
        [Tooltip("Relationship with the player before they have ever spoken.")]
        public int initialRelationship = 50;

        public List<EmotionPortrait> emotions = new List<EmotionPortrait>();

        [Header("Backstory & secrets")]
        [Tooltip("Things she tells freely when asked about herself.")]
        public List<PersonalFact> backstory = new List<PersonalFact>();

        [Tooltip("Things she hides until the conditions hold.")]
        public List<PersonalFact> secrets = new List<PersonalFact>();

        [Header("Situational direction")]
        public List<SituationalNote> situationalNotes = new List<SituationalNote>();

        [Header("Other NPCs")]
        public List<NpcBond> bonds = new List<NpcBond>();

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

        [Header("Offline replies (no API key / AI failed)")]
        public List<FallbackReplyRule> fallbackReplies = new List<FallbackReplyRule>();

        [TextArea(2, 4)]
        public List<string> neutralReplies = new List<string>();

        [Header("Returning to talk")]
        [Tooltip("Opening line when the player comes back. Picked by relationship; first match wins.")]
        public List<FallbackReplyRule> returnGreetings = new List<FallbackReplyRule>();

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

        public NpcBond BondWith(string otherNpcId)
        {
            if (bonds == null || string.IsNullOrWhiteSpace(otherNpcId))
            {
                return null;
            }

            foreach (NpcBond bond in bonds)
            {
                if (bond != null && string.Equals(
                        bond.otherNpcId, otherNpcId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return bond;
                }
            }

            return null;
        }

        public EmotionPortrait FindEmotion(string emotionId)
        {
            if (emotions == null || string.IsNullOrWhiteSpace(emotionId))
            {
                return null;
            }

            foreach (EmotionPortrait emotion in emotions)
            {
                if (emotion != null && emotion.sprite != null &&
                    string.Equals(emotion.emotionId, emotionId.Trim(),
                                  StringComparison.OrdinalIgnoreCase))
                {
                    return emotion;
                }
            }

            return null;
        }

        /// <summary>Unlocked backstory and secrets, plus the secrets still locked.</summary>
        public void SplitPersonalFacts(
            GameState state,
            List<PersonalFact> shareable,
            List<PersonalFact> locked)
        {
            AddPersonal(backstory, state, shareable, locked);
            AddPersonal(secrets, state, shareable, locked);
        }

        private static void AddPersonal(
            List<PersonalFact> source,
            GameState state,
            List<PersonalFact> shareable,
            List<PersonalFact> locked)
        {
            if (source == null)
            {
                return;
            }

            foreach (PersonalFact fact in source)
            {
                if (fact == null || string.IsNullOrWhiteSpace(fact.factId))
                {
                    continue;
                }

                if (fact.IsGranted(state))
                {
                    shareable.Add(fact);
                }
                else
                {
                    locked.Add(fact);
                }
            }
        }

        public bool IsSecret(string factId)
        {
            if (secrets == null)
            {
                return false;
            }

            foreach (PersonalFact fact in secrets)
            {
                if (fact != null && fact.factId == factId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
