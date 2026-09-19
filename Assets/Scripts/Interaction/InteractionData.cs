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

    [Header("Conditions")]
    public List<ConditionData> conditions =
        new List<ConditionData>();

    [Header("Effects")]
    public List<EffectData> effects =
        new List<EffectData>();
}