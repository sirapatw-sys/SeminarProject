using UnityEngine;
using MysteryGame.Core;

[CreateAssetMenu(
    fileName = "AddHistoryEffect",
    menuName = "Game/Rules/Effect/Add History"
)]
public class AddHistoryEffect : EffectData
{
    [SerializeField]
    private string historyText;

    public override void Execute(GameState state)
    {
        state.AddHistory(historyText);
    }
}