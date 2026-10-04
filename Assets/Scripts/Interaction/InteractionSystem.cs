using UnityEngine;
using MysteryGame.Core;

public class InteractionSystem : MonoBehaviour
{
    public static InteractionSystem Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static bool CanExecute(InteractionData data, GameState state, out string response)
    {
        response = string.Empty;
        if (data == null || state == null) return false;
        if (!data.repeatable && state.HasFlag("interaction." + data.interactionId + ".completed"))
        {
            response = data.alreadyCompletedMessage;
            return false;
        }
        if (!ConditionRule.AllHold(data.conditions, state) ||
            (data.inputPuzzle != null && !ConditionRule.AllHold(data.inputPuzzle.conditions, state)))
        {
            response = data.failureMessage;
            return false;
        }
        return true;
    }

    public bool TryExecute(InteractionData data, out string responseMessage, string answer = null)
    {
        GameState state = GameState.Instance;
        if (!CanExecute(data, state, out responseMessage)) return false;
        if (data.inputPuzzle != null && !data.inputPuzzle.TrySolve(state, answer))
        {
            responseMessage = data.inputPuzzle.failureMessage;
            return false;
        }
        foreach (ActionCommand action in data.actions) action?.Execute(state);
        if (!data.repeatable) state.SetFlag("interaction." + data.interactionId + ".completed");
        responseMessage = data.interactionMessage;
        return true;
    }
}
