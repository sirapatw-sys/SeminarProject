using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewInteractionData",
    menuName = "Game/Interaction Data"
)]
public class InteractionData : ScriptableObject
{
    [Header("Identity")]
    public string interactionId;
    public string displayName;

    [Header("Dialogue")]
    [TextArea(2, 5)]
    public string interactionMessage;

    [TextArea(2, 4)]
    public string failureMessage = "ตอนนี้ฉันยังทำสิ่งนี้ไม่ได้...";

    [Header("Repeat")]
    public bool repeatable = true;

    [TextArea(2, 4)]
    public string alreadyCompletedMessage = "ฉันตรวจสอบสิ่งนี้ไปแล้ว";

    [Header("Conditions")]
    public List<ConditionRule> conditions =
        new List<ConditionRule>();

    [Header("Actions")]
    public List<ActionCommand> actions =
        new List<ActionCommand>();

    [Header("Item Popup")]
    [Tooltip(
        "Item id to show in the item popup after a successful interaction. " +
        "Leave empty for interactions that hand out nothing."
    )]
    public string popupItemId;

    public string popupItemName;

    [TextArea(2, 4)]
    public string popupItemDescription;

    [Header("Room Transition")]
    [Tooltip(
        "Scene to move to after a successful interaction, e.g. Room02. " +
        "Leave empty to stay in the room."
    )]
    public string transitionScene;

    [TextArea(2, 4)]
    public string transitionMessage;

    [Tooltip("Ends the demo instead of loading a scene: fades out on the message.")]
    public bool endsDemo;

    [Header("Feedback")]
    [Tooltip("Plays the scare sting on success, for the haunted room's jump moments.")]
    public bool scareOnSuccess;
}
