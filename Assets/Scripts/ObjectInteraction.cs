using UnityEngine;

public class ObjectInteraction : MonoBehaviour
{
    [SerializeField]
    private InteractionData interactionData;

    private bool playerInRange;

    private void Update()
    {
        if (DialogueManager.IsDialogueOpen || AiSettingsPanel.IsOpen)
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

        string responseMessage;

        InteractionSystem.Instance.TryExecute(
            interactionData,
            out responseMessage
        );

        ShowDialogue(
            interactionData.displayName,
            responseMessage
        );
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
