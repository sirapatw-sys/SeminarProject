using UnityEngine;
using MysteryGame.Core;

[CreateAssetMenu(
    fileName = "SetFlagEffect",
    menuName = "Game/Rules/Effect/Set Flag"
)]
public class SetFlagEffect : EffectData
{
    [SerializeField]
    private string flagId;

    public override void Execute(GameState state)
    {
        state.SetFlag(flagId);
    }
}