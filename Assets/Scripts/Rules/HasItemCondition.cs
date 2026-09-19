using UnityEngine;
using MysteryGame.Core;

[CreateAssetMenu(
    fileName = "HasItemCondition",
    menuName = "Game/Rules/Condition/Has Item"
)]
public class HasItemCondition : ConditionData
{
    [SerializeField]
    private string itemId;

    public override bool Evaluate(GameState state)
    {
        return state.HasItem(itemId);
    }
}