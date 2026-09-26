using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMiniEvent", menuName = "Game/Mini Event")]
public class MiniEventData : ScriptableObject
{
    public string eventId;
    public string npcId;
    public MiniEventTriggerType triggerType;

    [Tooltip(
        "Need/emotion id for the threshold triggers, or the PuzzleStep.stepId " +
        "of the current room for RoomProgress."
    )]
    public string metricId;
    public string relatedNpcId;
    [Range(0f, 100f)] public float threshold = 70f;
    [Min(0f)] public float minimumRoomTimeSeconds;
    [Min(0.01f)] public float weight = 1f;
    [Min(0f)] public float cooldownSeconds = 120f;
    [Min(0f)] public float expiresSeconds = 45f;
    public bool repeatable = true;
    public bool useAiDialogue = true;

    [Tooltip(
        "A story beat fires as soon as it is eligible instead of waiting on " +
        "the random roll, and wins over ordinary events. Use it for events " +
        "that react to what the player just did."
    )]
    public bool storyBeat;

    [Tooltip(
        "Plays by itself as soon as nothing else is on screen, instead of " +
        "waiting behind a \"!\" for the player to walk over and press E."
    )]
    public bool autoStart;

    [Tooltip(
        "The NPC starts a conversation about something it picks itself: the AI " +
        "chooses a topic from what this NPC knows, what has happened and what " +
        "it remembers, and rewrites the choices to fit while keeping their " +
        "order (their actions stay). Needs AI; without it the event is skipped."
    )]
    public bool freeTopic;

    [TextArea(2, 5)] public string situationPrompt;
    public string tonePrompt = "เป็นธรรมชาติ กระชับ และเข้ากับเกมลึกลับ";

    [Tooltip("Used immediately when AI is unavailable or returns invalid data.")]
    public DialogueData dialogue;
    public List<ConditionRule> conditions = new List<ConditionRule>();

    public string CompletedFlag
    {
        get { return "mini_event." + eventId + ".completed"; }
    }

    public bool CanTrigger(GameState state)
    {
        if (state == null || dialogue == null ||
            Time.timeSinceLevelLoad < minimumRoomTimeSeconds)
        {
            return false;
        }

        switch (triggerType)
        {
            case MiniEventTriggerType.NeedThreshold:
                if (state.GetNpcNeed(npcId, metricId) < threshold)
                {
                    return false;
                }
                break;
            case MiniEventTriggerType.EmotionThreshold:
                if (state.GetNpcEmotion(npcId, metricId) < threshold)
                {
                    return false;
                }
                break;
            case MiniEventTriggerType.NpcConflict:
                if (state.GetNpcRelationship(npcId, relatedNpcId) > threshold)
                {
                    return false;
                }
                break;
            case MiniEventTriggerType.RoomProgress:
                if (!IsCurrentStep(state))
                {
                    return false;
                }
                break;
            case MiniEventTriggerType.TimeInRoom:
            case MiniEventTriggerType.RandomAmbient:
                break;
        }

        if (!repeatable && state.HasFlag(CompletedFlag))
        {
            return false;
        }

        return ConditionRule.AllHold(conditions, state);
    }

    /// <summary>True while the room's next step is the one named by metricId.</summary>
    private bool IsCurrentStep(GameState state)
    {
        RoomKnowledgeData room = KnowledgeLibrary.GetRoom(state.GetCurrentScene());
        if (room == null)
        {
            return false;
        }

        PuzzleStep step = room.CurrentStep(state);
        return step != null && step.stepId == metricId;
    }
}

public enum MiniEventTriggerType
{
    NeedThreshold,
    EmotionThreshold,
    TimeInRoom,
    NpcConflict,
    RandomAmbient,
    RoomProgress
}
