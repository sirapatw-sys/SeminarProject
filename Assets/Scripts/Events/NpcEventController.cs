using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
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
    private float nextStoryCheckTime;
    private TextMeshPro indicatorText;
    private bool generationInProgress;

    private void Awake()
    {
        GameObject indicator = new GameObject("TalkRequestIndicator");
        indicator.transform.SetParent(transform, false);

        // indicatorOffset is in world units: undo the NPC's own scale (Sena
        // is authored at 0.2) so every "!" sits at the same height and size.
        Vector3 parentScale = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(parentScale.x), 0.01f);
        float sy = Mathf.Max(Mathf.Abs(parentScale.y), 0.01f);
        indicator.transform.localPosition =
            new Vector3(indicatorOffset.x / sx, indicatorOffset.y / sy, indicatorOffset.z);
        indicator.transform.localScale = new Vector3(1f / sx, 1f / sy, 1f);

        indicatorText = indicator.AddComponent<TextMeshPro>();
        // A new TextMeshPro is a 20 x 5 box aligned top-left, which drew the
        // "!" about 10 units to the NPC's left. Centre it on the NPC.
        indicatorText.rectTransform.sizeDelta = new Vector2(2f, 2f);
        indicatorText.alignment = TextAlignmentOptions.Center;
        indicatorText.enableWordWrapping = false;
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
            // A story beat that the player has already moved past (Stelle's
            // fright at the mirror once the music box is open) is stale, so
            // its "!" goes away instead of replaying the moment out of order.
            bool stale = false;
            if (pendingEvent.storyBeat && Time.time >= nextStoryCheckTime &&
                !DialogueManager.IsDialogueOpen)
            {
                nextStoryCheckTime = Time.time + 1f;
                stale = GameState.Instance != null &&
                        !pendingEvent.CanTrigger(GameState.Instance);
            }

            // A story beat that becomes due while an ordinary "!" is still
            // waiting (Stelle asking for company, then the mirror message)
            // takes its place, so the story is never held up by small talk.
            if (!pendingEvent.storyBeat && Time.time >= nextStoryCheckTime &&
                !DialogueManager.IsDialogueOpen)
            {
                nextStoryCheckTime = Time.time + 1f;
                MiniEventData beat = FindDueStoryBeat();
                if (beat != null)
                {
                    ClearPendingEvent();
                    QueueEvent(beat);
                    return;
                }
            }

            if (stale || Time.time >= pendingUntil)
            {
                cooldownUntil[pendingEvent.eventId] = Time.time + pendingEvent.cooldownSeconds;
                ClearPendingEvent();
            }
            else if (pendingEvent.autoStart && !InputGate.IsBlocked &&
                     !ItemPopupUI.IsBusy && !KeypadLockUI.IsOpen)
            {
                TryStartPendingEvent();
            }

            return;
        }

        if (generationInProgress || DialogueManager.IsDialogueOpen)
        {
            return;
        }

        // Story beats react to what the player just did, so they are checked
        // often and skip the random roll entirely.
        if (Time.time >= nextStoryCheckTime)
        {
            nextStoryCheckTime = Time.time + 1f;
            if (TryQueueStoryBeat())
            {
                return;
            }
        }

        if (Time.time < nextCheckTime)
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

    public bool HasPendingEvent
    {
        get { return pendingEvent != null; }
    }

    public bool TryStartPendingEvent()
    {
        if (pendingEvent == null || DialogueManager.Instance == null)
        {
            return false;
        }

        MiniEventData selectedEvent = pendingEvent;
        if (GameState.Instance == null || !selectedEvent.CanTrigger(GameState.Instance))
        {
            ClearPendingEvent();
            return false;
        }
        GeneratedDialogueContent selectedDialogue = pendingDialogue;
        ClearPendingEvent();

        cooldownUntil[selectedEvent.eventId] =
            Time.time + selectedEvent.cooldownSeconds;

        if (!selectedEvent.repeatable && GameState.Instance != null)
        {
            GameState.Instance.SetFlag(selectedEvent.CompletedFlag);
        }

        DialogueManager.Instance.StartDialogue(
            selectedEvent.dialogue,
            selectedDialogue,
            isEvent: true
        );
        return true;
    }

    private bool TryQueueStoryBeat()
    {
        MiniEventData beat = FindDueStoryBeat();
        if (beat == null)
        {
            return false;
        }

        QueueEvent(beat);
        return true;
    }

    private MiniEventData FindDueStoryBeat()
    {
        if (GameState.Instance == null)
        {
            return null;
        }

        foreach (MiniEventData candidate in events)
        {
            if (candidate != null && candidate.storyBeat &&
                !IsOnCooldown(candidate) &&
                candidate.CanTrigger(GameState.Instance))
            {
                return candidate;
            }
        }

        return null;
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
            if (candidate == null || candidate.storyBeat ||
                IsOnCooldown(candidate) ||
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

    private static bool AiAvailable
    {
        get { return DialogueProviders.Current != null && DialogueProviders.Current.CanGenerate; }
    }

    private void QueueEvent(MiniEventData candidate)
    {
        if ((candidate.useAiDialogue || candidate.freeTopic) && AiAvailable)
        {
            generationInProgress = true;
            StartCoroutine(
                DialogueProviders.Current.Generate(
                    candidate,
                    generated =>
                    {
                        generationInProgress = false;
                        if (this == null)
                        {
                            return;
                        }

                        // A topic of its own is all a free-topic event has:
                        // when the AI could not write one, there is no "!".
                        if (candidate.freeTopic &&
                            (generated == null || !generated.IsValid(candidate.dialogue.choices.Count)))
                        {
                            generated = BuildOfflineTopic(candidate);
                        }
                        var context = NpcKnowledgeContextBuilder.Build(candidate.npcId,
                            GameState.Instance != null ? GameState.Instance.GetCurrentScene() : string.Empty,
                            GameState.Instance, false);
                        string reason;
                        if (generated != null && !NpcReplyPolicy.ValidateEvent(context, generated, out reason))
                            generated = BuildOfflineTopic(candidate);
                        if (GameState.Instance == null || !candidate.CanTrigger(GameState.Instance)) return;
                        SetPendingEvent(candidate, generated);
                    }
                )
            );
            return;
        }

        SetPendingEvent(candidate, candidate.freeTopic ? BuildOfflineTopic(candidate) : null);
    }

    public static GeneratedDialogueContent BuildOfflineTopic(MiniEventData candidate)
    {
        if (candidate == null || candidate.dialogue == null) return null;
        DialogueData source = candidate.dialogue;
        if (candidate.offlineVariants != null && candidate.offlineVariants.Count > 0)
            source = candidate.offlineVariants[Random.Range(0, candidate.offlineVariants.Count)] ?? source;
        if (source.choices.Count != candidate.dialogue.choices.Count) source = candidate.dialogue;
        var choices = new List<GeneratedDialogueChoice>();
        foreach (var choice in source.choices)
            choices.Add(new GeneratedDialogueChoice { optionText = choice.optionText, responseText = choice.responseText });
        return new GeneratedDialogueContent { lines = source.lines.ToArray(), choices = choices.ToArray(),
            referencedFactIds = System.Array.Empty<string>() };
    }

    private void SetPendingEvent(
        MiniEventData candidate,
        GeneratedDialogueContent generated)
    {
        pendingEvent = candidate;
        pendingDialogue = generated;
        pendingUntil = Time.time + candidate.expiresSeconds;
        indicatorText.gameObject.SetActive(true);
        // The chime calls the player over; an event that starts by itself
        // (Room03's story beats) needs no call.
        if (!candidate.autoStart)
        {
            SfxPlayer.Play(SfxPlayer.Cue.EventPing);
        }
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
