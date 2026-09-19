using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

public class NpcNeedController : MonoBehaviour
{
    [SerializeField] private string npcId;
    [SerializeField] private List<NpcNeedRate> needs = new List<NpcNeedRate>();
    [SerializeField] private List<NpcEmotionRate> emotions =
        new List<NpcEmotionRate>();

    private void Start()
    {
        if (GameState.Instance == null)
        {
            Debug.LogError("GameState is required for NPC needs.");
            enabled = false;
            return;
        }

        foreach (NpcNeedRate need in needs)
        {
            if (!GameState.Instance.HasNpcNeed(npcId, need.needId))
            {
                GameState.Instance.SetNpcNeed(npcId, need.needId, need.initialValue);
            }
        }

        foreach (NpcEmotionRate emotion in emotions)
        {
            if (!GameState.Instance.HasNpcEmotion(npcId, emotion.emotionId))
            {
                GameState.Instance.SetNpcEmotion(
                    npcId,
                    emotion.emotionId,
                    emotion.initialValue
                );
            }
        }
    }

    private void Update()
    {
        if (GameState.Instance == null)
        {
            return;
        }

        foreach (NpcNeedRate need in needs)
        {
            float change = need.increasePerMinute * Time.deltaTime / 60f;
            GameState.Instance.ChangeNpcNeed(npcId, need.needId, change);
        }


        foreach (NpcEmotionRate emotion in emotions)
        {
            float change = emotion.changePerMinute * Time.deltaTime / 60f;
            GameState.Instance.ChangeNpcEmotion(
                npcId,
                emotion.emotionId,
                change
            );
        }
    }
}

[Serializable]
public class NpcNeedRate
{
    public string needId;
    [Range(0f, 100f)] public float initialValue;
    public float increasePerMinute;
}

[Serializable]
public class NpcEmotionRate
{
    public string emotionId;
    [Range(0f, 100f)] public float initialValue;
    public float changePerMinute;
}
