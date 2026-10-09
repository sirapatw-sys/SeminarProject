using System;
using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class NpcEventController : MonoBehaviour
{
    [SerializeField] private List<MiniEventData> events = new List<MiniEventData>();
    [SerializeField, Min(0.5f)] private float checkInterval = 5f;
    [SerializeField, Range(0f, 1f)] private float triggerChancePerCheck = 0.5f;
    [SerializeField] private Vector3 indicatorOffset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Min(0.5f)] private float generationTimeoutSeconds = 25f;
    [Tooltip("After one of this NPC's ordinary \"!\" ends (talked to or ignored) it waits at least this long " +
             "before raising another. Story beats are never held back.")]
    [SerializeField, Min(0f)] private float ambientGapSeconds = 100f;
    [Tooltip("After any NPC's ordinary \"!\" ends, no NPC raises another for at least this long, " +
             "so two NPCs in the same room do not take turns.")]
    [SerializeField, Min(0f)] private float sharedAmbientGapSeconds = 40f;

    private readonly Dictionary<string, float> cooldownUntil =
        new Dictionary<string, float>();

    private MiniEventData pendingEvent;
    private GeneratedDialogueContent pendingDialogue;
    private bool pendingIsAi;
    private float pendingUntil;
    private float nextCheckTime;
    private float nextStoryCheckTime;
    private TextMeshPro indicatorText;
    private bool generationInProgress;
    private MiniEventData generatingEvent;
    private AiRequestOperation generationOperation;
    private int generationVersion;
    private float generationDeadline;
    private float nextAmbientTime;
    private string lastAmbientEventId;
    private static float nextSharedAmbientTime;

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
        // Independent of the provider's HTTP timeout, and of the game's time scale.
        if (generationInProgress && Time.realtimeSinceStartup >= generationDeadline)
        {
            CompleteGeneration(generatingEvent, generationVersion, null);
        }

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
                !DialogueManager.IsDialogueOpen && InputGate.IsGameplayActive)
            {
                nextStoryCheckTime = Time.time + 1f;
                MiniEventData beat = FindDueStoryBeat();
                if (beat != null)
                {
                    EndAmbient(pendingEvent);
                    ClearPendingEvent();
                    QueueEvent(beat);
                    return;
                }
            }

            if (stale || Time.time >= pendingUntil)
            {
                cooldownUntil[pendingEvent.eventId] = Time.time + pendingEvent.cooldownSeconds;
                EndAmbient(pendingEvent);
                ClearPendingEvent();
            }
            else if (pendingEvent.autoStart && !InputGate.IsBlocked &&
                     !ItemPopupUI.IsBusy && !KeypadLockUI.IsOpen)
            {
                TryStartPendingEvent();
            }

            return;
        }

        // Keep the watchdog and pending-event cleanup above running even
        // while gameplay is inactive, but do not start any new AI requests.
        if (DialogueManager.IsDialogueOpen || !InputGate.IsGameplayActive)
        {
            return;
        }

        // A slow ambient request must never hold an authored story beat hostage.
        if (generationInProgress)
        {
            if (generatingEvent != null && !generatingEvent.storyBeat && Time.time >= nextStoryCheckTime)
            {
                nextStoryCheckTime = Time.time + 1f;
                MiniEventData beat = FindDueStoryBeat();
                if (beat != null) QueueEvent(beat);
            }
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

        if (Time.time < nextAmbientTime || Time.time < nextSharedAmbientTime ||
            Random.value > triggerChancePerCheck)
        {
            return;
        }

        TryQueueRandomEvent();
    }

    /// <summary>
    /// An ordinary "!" ended (talked to, ignored or pushed aside by the
    /// story): this NPC and then everyone else keep quiet for a while.
    /// </summary>
    private void EndAmbient(MiniEventData ended)
    {
        if (ended == null || ended.storyBeat)
        {
            return;
        }

        nextAmbientTime = Time.time + ambientGapSeconds;
        nextSharedAmbientTime = Time.time + sharedAmbientGapSeconds;
        lastAmbientEventId = ended.eventId;
    }

    public bool HasPendingEvent
    {
        get { return pendingEvent != null; }
    }

    public bool TryStartPendingEvent()
    {
        if (pendingEvent == null || DialogueManager.Instance == null || DialogueManager.IsDialogueOpen ||
            !InputGate.IsGameplayActive)
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
        if (pendingIsAi)
        {
            var context = NpcKnowledgeContextBuilder.Build(selectedEvent.npcId,
                GameState.Instance.GetCurrentScene(), GameState.Instance, false);
            GeneratedDialogueContent grounded;
            string reason;
            selectedDialogue = selectedDialogue != null && selectedDialogue.IsValid(selectedEvent.dialogue.choices.Count) &&
                NpcReplyPolicy.TryGroundEvent(context, selectedDialogue, out grounded, out reason)
                ? grounded : BuildOfflineTopic(selectedEvent);
        }
        ClearPendingEvent();

        cooldownUntil[selectedEvent.eventId] =
            Time.time + selectedEvent.cooldownSeconds;
        EndAmbient(selectedEvent);

        if (!selectedEvent.repeatable && !selectedEvent.completeOnChoice && GameState.Instance != null)
        {
            GameState.Instance.SetFlag(selectedEvent.CompletedFlag);
        }

        // A free-topic chat's choices are stances, so its wording may vary
        // with each topic (AI-written or one of the authored variants); the
        // effects stay with each position. Story events keep authored text.
        DialogueManager.Instance.StartDialogue(
            selectedEvent.dialogue,
            selectedDialogue,
            isEvent: true,
            completeOnChoiceFlag: selectedEvent.completeOnChoice ? selectedEvent.CompletedFlag : null,
            useGeneratedChoices: selectedEvent.freeTopic && selectedDialogue != null
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

        // Never the same request twice in a row (Alice asking for water
        // again) while something else is possible.
        if (eligible.Count > 1)
        {
            MiniEventData repeat = eligible.Find(e => e.eventId == lastAmbientEventId);
            if (repeat != null)
            {
                eligible.Remove(repeat);
                totalWeight -= repeat.weight;
            }
        }

        MiniEventData chosen = eligible[eligible.Count - 1];
        float roll = Random.value * totalWeight;
        foreach (MiniEventData candidate in eligible)
        {
            roll -= candidate.weight;
            if (roll <= 0f)
            {
                chosen = candidate;
                break;
            }
        }

        // Hold the other NPCs back while this "!" is up (plus the time an AI
        // topic may take); EndAmbient sets the real gap once it is over.
        nextSharedAmbientTime = Mathf.Max(nextSharedAmbientTime,
            Time.time + generationTimeoutSeconds + chosen.expiresSeconds + sharedAmbientGapSeconds);
        QueueEvent(chosen);
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
        if (!InputGate.IsGameplayActive) return;
        CancelGeneration();
        ClearPendingEvent();
        if (candidate == null || candidate.dialogue == null) return;
        if ((candidate.useAiDialogue || candidate.freeTopic) && AiAvailable)
        {
            generationInProgress = true;
            generatingEvent = candidate;
            int version = generationVersion;
            generationDeadline = Time.realtimeSinceStartup + Mathf.Max(0.05f, generationTimeoutSeconds);
            var operation = new AiRequestOperation(this);
            generationOperation = operation;
            IAiDialogueProvider provider = DialogueProviders.Current;
            operation.Start(
                () => provider.Generate(candidate, content => CompleteGeneration(candidate, version, content)),
                error =>
                {
                    if (!IsCurrentGeneration(candidate, version)) return;
                    Debug.LogWarning("NPC event provider failed: " + error.GetType().Name);
                    CompleteGeneration(candidate, version, null);
                },
                () => CompleteGeneration(candidate, version, null));
            return;
        }

        SetPendingEvent(candidate, candidate.freeTopic ? BuildOfflineTopic(candidate) : null);
    }

    private bool IsCurrentGeneration(MiniEventData candidate, int version)
    {
        return this != null && isActiveAndEnabled && generationInProgress &&
            version == generationVersion && candidate == generatingEvent;
    }

    private void CompleteGeneration(MiniEventData candidate, int version, GeneratedDialogueContent generated)
    {
        if (!IsCurrentGeneration(candidate, version)) return;
        generationInProgress = false;
        var completed = generationOperation;
        generationOperation = null;
        generatingEvent = null;
        generationVersion++; // Discard duplicate, late, cancelled and old-scene callbacks.
        completed?.Cancel();
        if (GameState.Instance == null || candidate == null || !candidate.CanTrigger(GameState.Instance)) return;
        var context = NpcKnowledgeContextBuilder.Build(candidate.npcId,
            GameState.Instance.GetCurrentScene(), GameState.Instance, false);
        string reason;
        bool validAi = generated != null && generated.IsValid(candidate.dialogue.choices.Count) &&
            NpcReplyPolicy.ValidateEvent(context, generated, out reason);
        SetPendingEvent(candidate, validAi ? generated : BuildOfflineTopic(candidate), validAi);
    }

    private void CancelGeneration()
    {
        generationVersion++;
        generationInProgress = false;
        generatingEvent = null;
        var previous = generationOperation;
        generationOperation = null;
        previous?.Cancel();
    }

    private void OnDisable()
    {
        CancelGeneration();
        ClearPendingEvent();
    }

    private void OnDestroy() { CancelGeneration(); }

    // The offline topic each event used last, so the same one never plays twice in a row.
    private static readonly Dictionary<string, int> lastOfflineTopic = new Dictionary<string, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOfflineTopics() { lastOfflineTopic.Clear(); nextSharedAmbientTime = 0f; }

    public static GeneratedDialogueContent BuildOfflineTopic(MiniEventData candidate)
    {
        if (candidate == null || candidate.dialogue == null) return null;
        DialogueData source = candidate.dialogue;
        int count = candidate.offlineVariants != null ? candidate.offlineVariants.Count : 0;
        if (count > 0)
        {
            string key = candidate.eventId ?? string.Empty;
            int last;
            int pick = Random.Range(0, count);
            if (count > 1 && lastOfflineTopic.TryGetValue(key, out last) && pick == last)
                pick = (pick + 1 + Random.Range(0, count - 1)) % count;
            lastOfflineTopic[key] = pick;
            source = candidate.offlineVariants[pick] ?? source;
        }
        if (source.choices.Count != candidate.dialogue.choices.Count) source = candidate.dialogue;
        var choices = new List<GeneratedDialogueChoice>();
        foreach (var choice in source.choices)
            choices.Add(new GeneratedDialogueChoice { optionText = choice.optionText, responseText = choice.responseText });
        return new GeneratedDialogueContent { lines = source.lines.ToArray(), choices = choices.ToArray(),
            referencedFactIds = System.Array.Empty<string>() };
    }

    private void SetPendingEvent(
        MiniEventData candidate,
        GeneratedDialogueContent generated,
        bool fromAi = false)
    {
        pendingEvent = candidate;
        pendingDialogue = generated;
        pendingIsAi = fromAi;
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
        pendingIsAi = false;
        pendingUntil = 0f;

        if (indicatorText != null)
        {
            indicatorText.gameObject.SetActive(false);
        }
    }
}
