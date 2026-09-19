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

    [TextArea(2, 5)]
    public List<string> lines = new List<string>();

    public List<DialogueChoiceData> choices =
        new List<DialogueChoiceData>();
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
