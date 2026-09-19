using System;
using MysteryGame.Core;

[Serializable]
public class ConditionRule
{
    public ConditionType type;
    public string targetId;
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
            default:
                return false;
        }
    }
}

public enum ConditionType
{
    HasFlag,
    MissingFlag,
    HasItem,
    MissingItem,
    RelationshipAtLeast,
    RelationshipAtMost
}
