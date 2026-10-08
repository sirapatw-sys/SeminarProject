using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewDialogueData",
    menuName = "Game/Dialogue Data"
)]
public class DialogueData : ScriptableObject
{
    public string dialogueId;
    public string speakerId;
    public string speakerName;
    [Tooltip("2D portrait shown during this conversation.")]
    public Sprite speakerPortrait;

    [Tooltip("AI personality description for this speaker (optional).")]
    [TextArea(2, 5)]
    public string personalityPrompt;

    [TextArea(2, 5)]
    public List<string> lines = new List<string>();

    [Tooltip(
        "Use the authored returnLines instead of the generic contextual " +
        "greeting when the player talks to this speaker again."
    )]
    public bool overrideReturnLines;

    [Tooltip("Lines used on repeat conversations when overrideReturnLines is on.")]
    [TextArea(2, 5)]
    public List<string> returnLines = new List<string>();

    public List<DialogueChoiceData> choices =
        new List<DialogueChoiceData>();

    [Tooltip(
        "Offer the choices only the first time; later visits open straight " +
        "into free chat, so 'what is your name?' is not asked again (and " +
        "cannot be farmed for relationship points)."
    )]
    public bool firstMeetingChoicesOnly;
}

[Serializable]
public class DialogueChoiceData
{
    public string optionText;

    [TextArea(2, 5)]
    public string responseText;

    public List<ActionCommand> actions =
        new List<ActionCommand>();
}
