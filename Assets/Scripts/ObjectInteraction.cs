using MysteryGame.Core;
using UnityEngine;

public class ObjectInteraction : MonoBehaviour, IFocusable
{
    /// <summary>How often touching an uneasy object in the haunted room gets a noise back.</summary>
    private const float ScareNoiseChance = 0.35f;

    [SerializeField]
    private InteractionData interactionData;

    [Tooltip(
        "Used once interactionData is non-repeatable and already done, so one " +
        "object can hold a two-stage puzzle (inspect it, then use an item on it)."
    )]
    [SerializeField]
    private InteractionData followUpInteraction;

    private Collider2D area;

    // ------------------------------------------------------------ IFocusable
    // InteractionFocus picks the nearest interactable and calls Interact().

    public bool CanFocus
    {
        get
        {
            InteractionData data = ActiveData;
            // Sena answers E at the gate while she still guards it.
            return data != null && (!data.hideFocusUntilAvailable ||
                ConditionRule.AllHold(data.conditions, GameState.Instance));
        }
    }

    public string FocusPrompt
    {
        get
        {
            InteractionData data = ActiveData;
            return data != null ? "กด E เพื่อสำรวจ " + data.displayName : string.Empty;
        }
    }

    public Vector2 FocusPoint
    {
        get
        {
            if (area == null)
            {
                area = GetComponent<Collider2D>();
            }

            return area != null ? (Vector2)area.bounds.center : (Vector2)transform.position;
        }
    }

    public void Interact()
    {
        TryInteract();
    }

    /// <summary>The interaction this object offers right now.</summary>
    private InteractionData ActiveData
    {
        get
        {
            GameState state = GameState.Instance;
            if (followUpInteraction != null && interactionData != null &&
                !interactionData.repeatable && state != null &&
                state.HasFlag("interaction." + interactionData.interactionId + ".completed"))
            {
                return followUpInteraction;
            }

            return interactionData;
        }
    }

    private void TryInteract()
    {
        InteractionData data = ActiveData;
        if (data == null)
        {
            Debug.LogError(
                $"InteractionData is missing on {gameObject.name}."
            );

            return;
        }

        if (InteractionSystem.Instance == null)
        {
            Debug.LogError(
                "InteractionSystem.Instance is null."
            );

            return;
        }

        GameState state = GameState.Instance;
        string unavailable;
        if (data.inputPuzzle != null && data.inputPuzzle.numeric &&
            data.inputPuzzle.acceptedAnswers != null && data.inputPuzzle.acceptedAnswers.Count == 1 &&
            InteractionSystem.CanExecute(data, state, out unavailable))
        {
            var puzzle = data.inputPuzzle;
            KeypadLockUI.Show(puzzle.acceptedAnswers[0], puzzle.title, puzzle.question,
                () => ExecuteInteraction(data, puzzle.acceptedAnswers[0]));
            return;
        }
        ExecuteInteraction(data);
    }

    private void ExecuteInteraction(InteractionData data, string answer = null)
    {
        GameState state = GameState.Instance;
        string responseMessage;

        bool success = InteractionSystem.Instance.TryExecute(
            data,
            out responseMessage,
            answer
        );

        SfxPlayer.Play(success ? SfxPlayer.Cue.Interact : SfxPlayer.Cue.Locked);
        if (success && data.successClip != null)
        {
            SfxPlayer.PlayFeature(data.successClip, data.successClipVolume);
        }
        if (success && data.successSound != null)
        {
            SfxPlayer.PlayEerie(data.successSound, data.successSoundVolume);
        }
        else if (success && data.scareOnSuccess)
        {
            SfxPlayer.MaybeRoomNoiseSoon(ScareNoiseChance);
        }

        ShowDialogue(
            data.displayName,
            responseMessage
        );

        // What the player just read goes in the journal, so a clue that was
        // skipped past too quickly (or sits in a book they carried off) can
        // be read again with J. Doors that lead on are not clues.
        if (success && state != null && string.IsNullOrWhiteSpace(data.transitionScene) &&
            !data.endsDemo && state.AddJournalEntry(
                "interaction." + data.interactionId, data.displayName, responseMessage))
        {
            JournalUI.NotifyNewEntry();
        }

        // 2. Any interaction may hand the player an item to look at.
        if (success && !string.IsNullOrWhiteSpace(data.popupItemId))
        {
            ItemPopupUI.ShowItem(
                data.popupItemId,
                data.popupItemName,
                data.popupItemDescription
            );
        }

        // Doors that lead on are data: transitionScene / endsDemo on the
        // InteractionData, applied only when the interaction succeeded.
        if (success && RoomTransitionManager.Instance != null)
        {
            if (data.endsDemo)
            {
                SfxPlayer.PlayDoor();
                RoomTransitionManager.Instance.PlayEnding(data.transitionMessage);
            }
            else if (!string.IsNullOrWhiteSpace(data.transitionScene))
            {
                SfxPlayer.PlayDoor();
                RoomTransitionManager.Instance.TransitionToRoom(
                    data.transitionScene,
                    data.transitionMessage
                );
            }
        }
    }

    private void ShowDialogue(
        string speaker,
        string message)
    {
        if (DialogueManager.Instance == null)
        {
            return;
        }

        // Each written line is its own page, so a long description (the
        // music box, the tome) never spills out of the dialogue box.
        string[] pages = (message ?? string.Empty).Split(
            new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (pages.Length == 0)
        {
            return;
        }

        DialogueManager.Instance.StartDialogue(speaker, pages, false);
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            InteractionFocus.Enter(this);
        }
    }

    private void OnTriggerExit2D(
        Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            InteractionFocus.Exit(this);
        }
    }

    private void OnDisable()
    {
        InteractionFocus.Exit(this);
    }
}
