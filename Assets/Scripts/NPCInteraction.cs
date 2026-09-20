using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    [SerializeField]
    private DialogueData dialogueData;

    private bool playerInRange = false;
    private NpcEventController eventController;

    private void Awake()
    {
        eventController = GetComponent<NpcEventController>();
    }

    private void Update()
    {
        if (IntroSequence.IsPlaying || DialogueManager.IsDialogueOpen ||
            AiSettingsPanel.IsOpen)
        {
            return;
        }

        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            if (eventController != null &&
                eventController.TryStartPendingEvent())
            {
                return;
            }

            if (dialogueData == null)
            {
                Debug.LogError(
                    "DialogueData is missing on " + gameObject.name
                );
                return;
            }

            DialogueManager.Instance.StartDialogue(dialogueData);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            AiSettingsPanel.SetInteractionPrompt("กด E เพื่อคุย");
            Debug.Log("Press E to talk");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            AiSettingsPanel.SetInteractionPrompt(string.Empty);
        }
    }
}
