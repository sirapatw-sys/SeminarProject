using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/NPC Puzzle Data")]
public class NpcPuzzleData : ScriptableObject
{
    public string npcId;
    public string requiredItemId;
    public string offeringGivenFlag;
    public DialogueData demandDialogue;
    public DialogueData questionDialogue;
    public DialogueData solvedDialogue;
    public InputPuzzleData puzzle;
    public List<ActionCommand> meetingActions = new List<ActionCommand>();
    public List<ActionCommand> offeringActions = new List<ActionCommand>();
    public bool hideOnSolved = true;
    [Min(0)] public float readReplyDelay = 2.4f;
    [Min(0)] public float fadeDuration = 2f;
    public bool HasOffering(GameState state)
    {
        return state != null && (string.IsNullOrWhiteSpace(requiredItemId) || state.HasFlag(offeringGivenFlag));
    }
}
