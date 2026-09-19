using System;
using MysteryGame.Core;

[Serializable]
public class ActionCommand
{
    public ActionType type;
    public string targetId;
    public string secondaryId;
    public int amount;
    public string text;

    public void Execute(GameState state)
    {
        if (state == null)
        {
            return;
        }

        switch (type)
        {
            case ActionType.SetFlag:
                state.SetFlag(targetId);
                break;
            case ActionType.ClearFlag:
                state.RemoveFlag(targetId);
                break;
            case ActionType.AddItem:
                state.AddItem(targetId);
                break;
            case ActionType.RemoveItem:
                state.RemoveItem(targetId);
                break;
            case ActionType.ChangeRelationship:
                state.ChangeRelationship(targetId, amount);
                break;
            case ActionType.AddHistory:
                state.AddHistory(text);
                break;
            case ActionType.ChangeGoal:
                state.SetCurrentGoal(targetId);
                break;
            case ActionType.AddNpcMemory:
                state.AddNpcMemory(targetId, text);
                break;
            case ActionType.ChangeNpcNeed:
                state.ChangeNpcNeed(targetId, secondaryId, amount);
                break;
            case ActionType.ChangeNpcEmotion:
                state.ChangeNpcEmotion(targetId, secondaryId, amount);
                break;
            case ActionType.ChangeNpcRelationship:
                state.ChangeNpcRelationship(targetId, secondaryId, amount);
                break;
        }
    }
}

public enum ActionType
{
    SetFlag,
    ClearFlag,
    AddItem,
    RemoveItem,
    ChangeRelationship,
    AddHistory,
    ChangeGoal,
    AddNpcMemory,
    ChangeNpcNeed,
    ChangeNpcEmotion,
    ChangeNpcRelationship
}
