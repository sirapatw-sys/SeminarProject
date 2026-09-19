using UnityEngine;
using MysteryGame.Core;

public abstract class ConditionData : ScriptableObject
{
    public abstract bool Evaluate(GameState state);
}