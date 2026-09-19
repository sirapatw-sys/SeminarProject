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

    public bool TryExecute(
        InteractionData data,
        out string responseMessage)
    {
        responseMessage = string.Empty;

        if (data == null)
        {
            Debug.LogError("InteractionData is null.");
            return false;
        }

        string completionFlag =
            "interaction." + data.interactionId + ".completed";

        if (!data.repeatable &&
            GameState.Instance.HasFlag(completionFlag))
        {
            responseMessage = data.alreadyCompletedMessage;
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

        foreach (ConditionRule condition in data.conditions)
        {
            if (condition == null)
            {
                continue;
            }

            if (!condition.Evaluate(GameState.Instance))
            {
                responseMessage = data.failureMessage;
                return false;
            }
        }

        // =====================================================
        // Execute Effects
        // =====================================================

        foreach (ActionCommand action in data.actions)
        {
            if (action == null)
            {
                continue;
            }

            action.Execute(GameState.Instance);
        }

        if (!data.repeatable)
        {
            GameState.Instance.SetFlag(completionFlag);
        }

        responseMessage = data.interactionMessage;
        return true;
    }
}
