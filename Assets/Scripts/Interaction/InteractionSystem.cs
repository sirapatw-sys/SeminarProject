using UnityEngine;
using MysteryGame.Core;

public class InteractionSystem : MonoBehaviour
{
    public static InteractionSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public bool TryExecute(InteractionData data)
    {
        if (data == null)
        {
            Debug.LogError("InteractionData is null.");
            return false;
        }

        if (GameState.Instance == null)
        {
            Debug.LogError("GameState.Instance is null.");
            return false;
        }

        // =====================================================
        // Check Conditions
        // =====================================================

        foreach (ConditionData condition in data.conditions)
        {
            if (condition == null)
            {
                continue;
            }

            if (!condition.Evaluate(GameState.Instance))
            {
                return false;
            }
        }

        // =====================================================
        // Execute Effects
        // =====================================================

        foreach (EffectData effect in data.effects)
        {
            if (effect == null)
            {
                continue;
            }

            effect.Execute(GameState.Instance);
        }

        return true;
    }
}