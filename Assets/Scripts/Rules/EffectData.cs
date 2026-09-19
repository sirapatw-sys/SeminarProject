using UnityEngine;
using MysteryGame.Core;

public abstract class EffectData : ScriptableObject
{
    public abstract void Execute(GameState state);
}