using UnityEngine;

public class NPCInteraction : MonoBehaviour, IFocusable
{
    [SerializeField]
    private DialogueData dialogueData;

    private NpcEventController eventController;
    private Collider2D talkArea;

    private void Awake()
    {
        eventController = GetComponent<NpcEventController>();
        talkArea = GetComponent<Collider2D>();
    }

    private string Prompt
    {
        get
        {
            return dialogueData != null && !string.IsNullOrWhiteSpace(dialogueData.speakerName)
                ? "กด E เพื่อคุยกับ " + dialogueData.speakerName
                : "กด E เพื่อคุย";
        }
    }

    // ------------------------------------------------------------ IFocusable
    // InteractionFocus picks the nearest interactable and calls Interact().

    public bool CanFocus
    {
        get { return true; }
    }

    public string FocusPrompt
    {
        get { return Prompt; }
    }

    /// <summary>The talk circle sits at the NPC's feet.</summary>
    public Vector2 FocusPoint
    {
        get
        {
            return talkArea != null ? (Vector2)talkArea.bounds.center : (Vector2)transform.position;
        }
    }

    public void Interact()
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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            InteractionFocus.Enter(this);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
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
