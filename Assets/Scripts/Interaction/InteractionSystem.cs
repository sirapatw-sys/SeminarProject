using UnityEngine;
using MysteryGame.Core;

public enum InteractionAvailability { Ready, Unavailable, AlreadyCompleted, Blocked }

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
        return CheckAvailability(data, state, out response) == InteractionAvailability.Ready;
    }

    public static InteractionAvailability CheckAvailability(InteractionData data, GameState state, out string response)
    {
        response = string.Empty;
        if (data == null || state == null) return InteractionAvailability.Unavailable;
        if (!data.repeatable && state.HasFlag("interaction." + data.interactionId + ".completed"))
        {
            response = data.alreadyCompletedMessage;
            return InteractionAvailability.AlreadyCompleted;
        }
        if (!ConditionRule.AllHold(data.conditions, state) ||
            (data.inputPuzzle != null && !ConditionRule.AllHold(data.inputPuzzle.conditions, state)))
        {
            response = data.failureMessage;
            return InteractionAvailability.Blocked;
        }
        return InteractionAvailability.Ready;
    }

    public bool TryExecute(InteractionData data, out string responseMessage, string answer = null)
    {
        return Execute(data, out responseMessage, answer, null);
    }

    public CompanionInteractionResult TryExecuteCompanion(InteractionData data, string npcId,
        CompanionInteractionOperation operation = CompanionInteractionOperation.Inspect)
    {
        var result = new CompanionInteractionResult { InteractionId = data != null ? data.interactionId : null };
        // NPCs cannot solve input puzzles, run player preparation/holds alone, or leave the room.
        if (data == null || !data.allowCompanionExecution || string.IsNullOrWhiteSpace(npcId) ||
            !System.Enum.IsDefined(typeof(CompanionInteractionOperation), operation) || data.companionOperation != operation ||
            data.inputPuzzle != null || data.endsDemo || !string.IsNullOrWhiteSpace(data.transitionScene))
        { result.Message = "ตรงนี้ต้องให้เธอเป็นคนลงมือเอง ฉันช่วยดูหรือรอข้าง ๆ ได้นะ"; return result; }
        string message;
        result.Success = Execute(data, out message, null, npcId);
        result.Message = result.Success && !string.IsNullOrWhiteSpace(data.companionMessage) ? data.companionMessage : message;
        return result;
    }

    private bool Execute(InteractionData data, out string responseMessage, string answer, string actorNpcId)
    {
        GameState state = GameState.Instance;
        var availability = CheckAvailability(data, state, out responseMessage);
        if (availability != InteractionAvailability.Ready)
        {
            // A blocked attempt is observable; re-reading a finished object or
            // missing runtime data is not a new failed gameplay attempt.
            if (availability == InteractionAvailability.Blocked) Publish(data, false, actorNpcId);
            return false;
        }
        if (data.inputPuzzle != null && !data.inputPuzzle.TrySolve(state, answer))
        {
            responseMessage = data.inputPuzzle.failureMessage;
            Publish(data, false, actorNpcId);
            return false;
        }
        foreach (ActionCommand action in data.actions) action?.Execute(state);
        if (!data.repeatable) state.SetFlag("interaction." + data.interactionId + ".completed");
        responseMessage = data.interactionMessage;
        Publish(data, true, actorNpcId);
        return true;
    }

    private static void Publish(InteractionData data, bool success, string npcId)
    {
        if (npcId == null) InteractionSignals.Publish(data, success);
        else InteractionSignals.PublishCompanion(data, success, npcId);
    }
}

/// <summary>Verified gameplay observations, not claims made in AI/player prose.</summary>
public static class InteractionSignals
{
    public static event System.Action<InteractionData, bool> Completed;
    public static event System.Action<InteractionData, bool, string> CompanionCompleted;
    public static void Publish(InteractionData data, bool success) { Completed?.Invoke(data, success); }
    public static void PublishCompanion(InteractionData data, bool success, string npcId) { CompanionCompleted?.Invoke(data, success, npcId); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Completed = null; CompanionCompleted = null; }
}
