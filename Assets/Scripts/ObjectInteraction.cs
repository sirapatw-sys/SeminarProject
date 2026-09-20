using UnityEngine;

public class ObjectInteraction : MonoBehaviour
{
    [SerializeField]
    private InteractionData interactionData;

    private bool playerInRange;

    private void Update()
    {
        if (IntroSequence.IsPlaying || DialogueManager.IsDialogueOpen ||
            AiSettingsPanel.IsOpen || KeypadLockUI.IsOpen)
        {
            return;
        }

        if (playerInRange &&
            Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        if (interactionData == null)
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
        if (IsCelestialDoor(interactionData) && SenaInteraction.IsGuardingDoor)
        {
            return;
        }

        // 1. Special Case: Drawer 4-Digit Combination Lock
        if (interactionData.interactionId == "open_drawer")
        {
            if (MysteryGame.Core.GameState.Instance != null &&
                MysteryGame.Core.GameState.Instance.HasFlag("drawer_opened"))
            {
                ShowDialogue(
                    interactionData.displayName,
                    "ลิ้นชักเปิดออกแล้ว และไม่มีอะไรเหลืออยู่ข้างในแล้ว"
                );
                return;
            }

            KeypadLockUI.Show(
                targetCode: "4592",
                title: "แม่กุญแจรหัสของลิ้นชัก (Drawer Lock)",
                hint: "ใส่รหัสตัวเลข 4 หลักเพื่อปลดล็อคลิ้นชัก",
                onSuccess: () =>
                {
                    if (MysteryGame.Core.GameState.Instance != null)
                    {
                        MysteryGame.Core.GameState.Instance.SetFlag("drawer_opened");
                        MysteryGame.Core.GameState.Instance.AddItem("key");
                    }
                    ShowDialogue(
                        interactionData.displayName,
                        "รหัสถูกต้อง! ได้ยินเสียงสลักปลดล็อคดังคลิก...\nในลิ้นชักมีกุญแจทองเหลืองโบราณซ่อนอยู่!"
                    );
                }
            );
            return;
        }

        string responseMessage;

        bool success = InteractionSystem.Instance.TryExecute(
            interactionData,
            out responseMessage
        );

        ShowDialogue(
            interactionData.displayName,
            responseMessage
        );

        // 2. Any interaction may hand the player an item to look at.
        if (success && !string.IsNullOrWhiteSpace(interactionData.popupItemId))
        {
            ItemPopupUI.ShowItem(
                interactionData.popupItemId,
                interactionData.popupItemName,
                interactionData.popupItemDescription
            );
        }

        // 3. Special Case: Desk Note (Repeatable reading)
        if (interactionData.interactionId == "inspect_desk" &&
            MysteryGame.Core.GameState.Instance != null &&
            MysteryGame.Core.GameState.Instance.HasFlag("found_note"))
        {
            ItemPopupUI.ShowItem("paper");
        }

        // 4. Special Case: Room01 Door Room Transition to Room02
        if (interactionData.interactionId == "unlock_door" &&
            MysteryGame.Core.GameState.Instance != null &&
            MysteryGame.Core.GameState.Instance.HasFlag("door_unlocked"))
        {
            if (RoomTransitionManager.Instance != null)
            {
                RoomTransitionManager.Instance.TransitionToRoom(
                    "Room02",
                    "คุณใช้กุญแจเปิดประตูสำเร็จ...\nก้าวเดินเข้าสู่ห้องถัดไป (Room 02)"
                );
            }
        }

        // 5. Special Case: Room02 Celestial Door Transition to Room03.
        // DoorR2_Data requires the "sena_passed" flag, so a failed
        // TryExecute has already shown the locked-gate message for us.
        if (IsCelestialDoor(interactionData) &&
            success &&
            MysteryGame.Core.GameState.Instance != null &&
            MysteryGame.Core.GameState.Instance.HasFlag("sena_passed"))
        {
            if (RoomTransitionManager.Instance != null)
            {
                RoomTransitionManager.Instance.TransitionToRoom(
                    "Room03",
                    "คุณก้าวผ่านประตูดวงดาวที่เปิดออก...\nมุ่งหน้าสู่ห้องถัดไป (Room 03)"
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

        DialogueManager.Instance.StartDialogue(
            speaker,
            new string[]
            {
                message
            },
            false
        );
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;

            if (IsCelestialDoor(interactionData) && SenaInteraction.IsGuardingDoor)
            {
                // Sena's own prompt wins while she is blocking the gate.
                return;
            }

            if (interactionData != null)
            {
                AiSettingsPanel.SetInteractionPrompt(
                    "กด E เพื่อสำรวจ " + interactionData.displayName
                );
                Debug.Log(
                    "Press E to interact with " +
                    interactionData.displayName
                );
            }
        }
    }

    private void OnTriggerExit2D(
        Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            AiSettingsPanel.SetInteractionPrompt(string.Empty);
        }
    }
}
