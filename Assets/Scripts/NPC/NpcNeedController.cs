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
    private GameState observed;

    private void OnEnable()
    {
        BindState();
        InitializeMissingValues();
    }

    private void BindState()
    {
        if (observed == GameState.Instance) return;
        UnbindState();
        observed = GameState.Instance;
        if (observed != null) observed.StateReset += InitializeMissingValues;
    }

    private void UnbindState()
    {
        if (observed != null) observed.StateReset -= InitializeMissingValues;
        observed = null;
    }

    private void OnDisable() { UnbindState(); }

    private void Start()
    {
        if (GameState.Instance == null)
        {
            Debug.LogError("GameState is required for NPC needs.");
            enabled = false;
            return;
        }

        BindState();
        InitializeMissingValues();
    }

    // Fill only missing values, including optional fields in legacy saves.
    // Existing saved needs/emotions always win over scene defaults.
    private void InitializeMissingValues()
    {
        if (observed == null) return;

        foreach (NpcNeedRate need in needs)
        {
            if (need != null && !observed.HasNpcNeed(npcId, need.needId))
            {
                observed.SetNpcNeed(npcId, need.needId, need.initialValue);
            }
        }

        foreach (NpcEmotionRate emotion in emotions)
        {
            if (emotion != null && !observed.HasNpcEmotion(npcId, emotion.emotionId))
            {
                observed.SetNpcEmotion(
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

        BindState();
        InitializeMissingValues();
        if (!InputGate.IsGameplayActive) return;

        foreach (NpcNeedRate need in needs)
        {
            if (need == null) continue;
            float change = need.increasePerMinute * Time.deltaTime / 60f;
            GameState.Instance.ChangeNpcNeed(npcId, need.needId, change);
        }


        foreach (NpcEmotionRate emotion in emotions)
        {
            if (emotion == null) continue;
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
