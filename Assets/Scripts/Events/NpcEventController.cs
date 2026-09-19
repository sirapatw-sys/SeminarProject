using System.Collections.Generic;
using MysteryGame.Core;
using TMPro;
using UnityEngine;

public class NpcEventController : MonoBehaviour
{
    [SerializeField] private List<MiniEventData> events = new List<MiniEventData>();
    [SerializeField, Min(0.5f)] private float checkInterval = 5f;
    [SerializeField, Range(0f, 1f)] private float triggerChancePerCheck = 0.5f;
    [SerializeField] private Vector3 indicatorOffset = new Vector3(0f, 1.2f, 0f);

    private readonly Dictionary<string, float> cooldownUntil =
        new Dictionary<string, float>();

    private MiniEventData pendingEvent;
    private GeneratedDialogueContent pendingDialogue;
    private float pendingUntil;
    private float nextCheckTime;
    private TextMeshPro indicatorText;
    private bool generationInProgress;

    private void Awake()
    {
        GameObject indicator = new GameObject("TalkRequestIndicator");
        indicator.transform.SetParent(transform, false);
        indicator.transform.localPosition = indicatorOffset;

        indicatorText = indicator.AddComponent<TextMeshPro>();
        indicatorText.text = "!";
        indicatorText.fontSize = 5f;
        indicatorText.color = new Color(1f, 0.82f, 0.15f);
        indicatorText.GetComponent<Renderer>().sortingOrder = 100;
        indicator.SetActive(false);
    }

    private void Update()
    {
        if (pendingEvent != null)
        {
            if (Time.time >= pendingUntil)
            {
                ClearPendingEvent();
            }

            return;
        }

        if (generationInProgress || DialogueManager.IsDialogueOpen ||
            Time.time < nextCheckTime)
        {
            return;
        }

        nextCheckTime = Time.time + checkInterval;

        if (Random.value > triggerChancePerCheck)
        {
            return;
        }

        TryQueueRandomEvent();
    }

    public bool TryStartPendingEvent()
    {
        if (pendingEvent == null || DialogueManager.Instance == null)
        {
            return false;
        }

        MiniEventData selectedEvent = pendingEvent;
        GeneratedDialogueContent selectedDialogue = pendingDialogue;
        ClearPendingEvent();

        cooldownUntil[selectedEvent.eventId] =
            Time.time + selectedEvent.cooldownSeconds;

        if (!selectedEvent.repeatable && GameState.Instance != null)
        {
            GameState.Instance.SetFlag(
                "mini_event." + selectedEvent.eventId + ".completed"
            );
        }

        DialogueManager.Instance.StartDialogue(
            selectedEvent.dialogue,
            selectedDialogue
        );
        return true;
    }

    private void TryQueueRandomEvent()
    {
        if (GameState.Instance == null)
        {
            return;
        }

        List<MiniEventData> eligible = new List<MiniEventData>();
        float totalWeight = 0f;

        foreach (MiniEventData candidate in events)
        {
            if (candidate == null || IsOnCooldown(candidate) ||
                !candidate.CanTrigger(GameState.Instance))
            {
                continue;
            }

            eligible.Add(candidate);
            totalWeight += candidate.weight;
        }

        if (eligible.Count == 0)
        {
            return;
        }

        float roll = Random.value * totalWeight;
        foreach (MiniEventData candidate in eligible)
        {
            roll -= candidate.weight;
            if (roll <= 0f)
            {
                QueueEvent(candidate);
                return;
            }
        }

        QueueEvent(eligible[eligible.Count - 1]);
    }

    private bool IsOnCooldown(MiniEventData candidate)
    {
        return cooldownUntil.ContainsKey(candidate.eventId) &&
               Time.time < cooldownUntil[candidate.eventId];
    }

    private void QueueEvent(MiniEventData candidate)
    {
        if (candidate.useAiDialogue &&
            AiDialogueGenerator.Instance != null &&
            AiDialogueGenerator.Instance.CanGenerate)
        {
            generationInProgress = true;
            StartCoroutine(
                AiDialogueGenerator.Instance.Generate(
                    candidate,
                    generated =>
                    {
                        generationInProgress = false;
                        if (this != null)
                        {
                            SetPendingEvent(candidate, generated);
                        }
                    }
                )
            );
            return;
        }

        SetPendingEvent(candidate, null);
    }

    private void SetPendingEvent(
        MiniEventData candidate,
        GeneratedDialogueContent generated)
    {
        pendingEvent = candidate;
        pendingDialogue = generated;
        pendingUntil = Time.time + candidate.expiresSeconds;
        indicatorText.gameObject.SetActive(true);
    }

    private void ClearPendingEvent()
    {
        pendingEvent = null;
        pendingDialogue = null;
        pendingUntil = 0f;

        if (indicatorText != null)
        {
            indicatorText.gameObject.SetActive(false);
        }
    }
}
