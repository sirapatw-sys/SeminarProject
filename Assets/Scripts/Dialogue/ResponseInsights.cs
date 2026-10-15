using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;

/// <summary>
/// The line of numbers under an NPC's reply: how much of a hint it gave, how
/// the game read the player's message, and how close the NPC is to the player
/// as it answers. All three come from the framework's own decisions (the hint
/// level it allowed, the relationship change it judged, the relationship it
/// keeps), not from the model grading itself, so each number can be traced.
/// </summary>
public static class ResponseInsights
{
    private const string PrefKey = "ui.responseInsights";

    /// <summary>Shown by default; the AI settings panel can turn it off.</summary>
    public static bool Enabled
    {
        get { return PlayerPrefs.GetInt(PrefKey, 1) == 1; }
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The part of the room's hint ladder this reply used: none 0, vague 33,
    /// normal 67, explicit 100.
    /// </summary>
    public static int HintPercent(HintLevel level)
    {
        return Mathf.Clamp(Mathf.RoundToInt((int)level * 100f / (int)HintLevel.Explicit), 0, 100);
    }

    /// <summary>
    /// How kindly the game read the player's message, from the relationship
    /// change it judged: 50 is neutral, 100 the warmest a message can score
    /// (+<see cref="RelationshipTuning.MaxGainPerMessage"/>), 0 the harshest
    /// (-<see cref="RelationshipTuning.MaxLossPerMessage"/>).
    /// </summary>
    public static int KindPercent(int judgedDelta)
    {
        if (judgedDelta >= 0)
        {
            float gain = Mathf.Min(judgedDelta, RelationshipTuning.MaxGainPerMessage);
            return 50 + Mathf.RoundToInt(50f * gain / RelationshipTuning.MaxGainPerMessage);
        }

        float loss = Mathf.Min(-judgedDelta, RelationshipTuning.MaxLossPerMessage);
        return 50 - Mathf.RoundToInt(50f * loss / RelationshipTuning.MaxLossPerMessage);
    }

    /// <summary>
    /// The line appended under the reply. Closeness is the relationship the
    /// NPC answered from (0-100), which also picks its tone and hint level.
    /// </summary>
    public static string Format(int hintPercent, int kindPercent, int closenessPercent)
    {
        // How friendly the player's message read: 50 neutral, 100 warmest, 0 hostile.
        int kind = Mathf.Clamp(kindPercent, 0, 100);
        return "\n<size=75%><color=#8E97AD>〔ใบ้ " + Mathf.Clamp(hintPercent, 0, 100) + "%" +
               "  ·  ความเป็นมิตรของผู้เล่น " + kind + "%" +
               "  ·  ความสนิทในการตอบ " + Mathf.Clamp(closenessPercent, 0, 100) + "%〕</color></size>";
    }
}
