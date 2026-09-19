using UnityEngine;
using MysteryGame.Core;

[CreateAssetMenu(
    fileName = "FlagCondition",
    menuName = "Game/Rules/Condition/Flag"
)]
public class FlagCondition : ConditionData
{
    [SerializeField]
    private string flagId;

    public override bool Evaluate(GameState state)
    {
        return state.HasFlag(flagId);
    }
}