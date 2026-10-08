using System;
using System.Collections.Generic;
using MysteryGame.Core;

[Serializable]
public class ConditionRule
{
    public ConditionType type;
    public string targetId;

    [UnityEngine.Tooltip("The second NPC for the NpcRelationship* conditions.")]
    public string secondaryId;

    public int amount;

    public bool Evaluate(GameState state)
    {
        if (state == null)
        {
            return false;
        }

        switch (type)
        {
            case ConditionType.HasFlag:
                return state.HasFlag(targetId);
            case ConditionType.MissingFlag:
                return !state.HasFlag(targetId);
            case ConditionType.HasItem:
                return state.HasItem(targetId);
            case ConditionType.MissingItem:
                return !state.HasItem(targetId);
            case ConditionType.RelationshipAtLeast:
                return state.GetRelationship(targetId) >= amount;
            case ConditionType.RelationshipAtMost:
                return state.GetRelationship(targetId) <= amount;
            case ConditionType.NpcRelationshipAtLeast:
                return state.GetNpcRelationship(targetId, secondaryId) >= amount;
            case ConditionType.NpcRelationshipAtMost:
                return state.GetNpcRelationship(targetId, secondaryId) <= amount;
            case ConditionType.WorldChangedSinceLastTalk:
                return state.HasWorldChangedSinceConversation(targetId);
            default:
                return false;
        }
    }

    /// <summary>True when every rule holds; an empty or missing list holds.</summary>
    public static bool AllHold(List<ConditionRule> rules, GameState state)
    {
        if (rules == null)
        {
            return true;
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

public enum ConditionType
{
    HasFlag,
    MissingFlag,
    HasItem,
    MissingItem,
    RelationshipAtLeast,
    RelationshipAtMost,
    NpcRelationshipAtLeast,
    NpcRelationshipAtMost,
    WorldChangedSinceLastTalk
}
