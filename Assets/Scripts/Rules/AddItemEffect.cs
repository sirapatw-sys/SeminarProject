using UnityEngine;
using MysteryGame.Core;

[CreateAssetMenu(
    fileName = "AddItemEffect",
    menuName = "Game/Rules/Effect/Add Item"
)]
public class AddItemEffect : EffectData
{
    [SerializeField]
    private string itemId;

    public override void Execute(GameState state)
    {
        state.AddItem(itemId);
    }
}