using MysteryGame.Core;
using UnityEngine;

public class ObjectInteraction : MonoBehaviour, IFocusable
{
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
            return data != null && !(IsCelestialDoor(data) && SenaInteraction.IsGuardingDoor);
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

        // 0. Sena guards the celestial gate. While she is still standing
        // there, her trigger overlaps the door's, so let her own script own
        // the E key instead of both of them opening a dialogue at once.
        if (IsCelestialDoor(data) && SenaInteraction.IsGuardingDoor)
        {
            return;
        }

        GameState state = GameState.Instance;

        // 1. Special Case: Drawer 4-Digit Combination Lock
        if (data.interactionId == "open_drawer")
        {
            if (state != null && state.HasFlag("drawer_opened"))
            {
                ShowDialogue(
                    data.displayName,
                    "ลิ้นชักเปิดออกแล้ว และไม่มีอะไรเหลืออยู่ข้างในแล้ว"
                );
                return;
            }

            SfxPlayer.Play(SfxPlayer.Cue.Interact);
            KeypadLockUI.Show(
                targetCode: "4592",
                title: "แม่กุญแจรหัสของลิ้นชัก (Drawer Lock)",
                hint: "ใส่รหัสตัวเลข 4 หลักเพื่อปลดล็อคลิ้นชัก",
                onSuccess: () =>
                {
                    if (GameState.Instance != null)
                    {
                        GameState.Instance.SetFlag("drawer_opened");
                        GameState.Instance.AddItem("key");
                    }
                    SfxPlayer.Play(SfxPlayer.Cue.Success);
                    ShowDialogue(
                        data.displayName,
                        "รหัสถูกต้อง! ได้ยินเสียงสลักปลดล็อคดังคลิก...\nในลิ้นชักมีกุญแจทองเหลืองโบราณซ่อนอยู่!"
                    );
                }
            );
            return;
        }

        string responseMessage;

        bool success = InteractionSystem.Instance.TryExecute(
            data,
            out responseMessage
        );

        SfxPlayer.Play(success
            ? (data.scareOnSuccess ? SfxPlayer.Cue.Scare : SfxPlayer.Cue.Interact)
            : SfxPlayer.Cue.Locked);
        if (success && data.successClip != null)
        {
            SfxPlayer.PlayFeature(data.successClip, data.successClipVolume);
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

        // 3. Special Case: Desk Note (Repeatable reading)
        if (data.interactionId == "inspect_desk" &&
            state != null && state.HasFlag("found_note"))
        {
            ItemPopupUI.ShowItem("paper");
        }

        // 4. Doors that lead on are data: transitionScene / endsDemo on the
        // InteractionData, applied only when the interaction succeeded.
        if (success && RoomTransitionManager.Instance != null)
        {
            if (data.endsDemo)
            {
                RoomTransitionManager.Instance.PlayEnding(data.transitionMessage);
            }
            else if (!string.IsNullOrWhiteSpace(data.transitionScene))
            {
                RoomTransitionManager.Instance.TransitionToRoom(
                    data.transitionScene,
                    data.transitionMessage
                );
            }
        }
    }

    private static bool IsCelestialDoor(InteractionData data)
    {
        return data != null &&
               (data.interactionId == "unlock_celestial_door" ||
                data.interactionId == "unlock_door_r2");
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
