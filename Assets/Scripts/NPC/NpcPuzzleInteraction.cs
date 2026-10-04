using System.Collections;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;

/// <summary>Reusable offering + typed-answer interaction. All story rules live in its asset.</summary>
public class NpcPuzzleInteraction : MonoBehaviour, IFocusable
{
    [SerializeField] private NpcPuzzleData definition;
    private bool solving;
    private Collider2D area;
    private SpriteRenderer[] renderers;
    private Color[] colors;
    private NpcEventController events;
    private bool IsSolved { get { return definition != null && definition.puzzle != null &&
        GameState.Instance != null && GameState.Instance.HasFlag(definition.puzzle.solvedFlag); } }
    public bool CanFocus { get { return definition != null && !solving && !IsSolved; } }
    public string FocusPrompt
    {
        get
        {
            var npc = definition != null ? KnowledgeLibrary.GetNpc(definition.npcId) : null;
            return "กด E เพื่อสนทนากับ " + (npc != null ? npc.displayName : definition?.npcId);
        }
    }
    public Vector2 FocusPoint { get { return area != null ? (Vector2)area.bounds.center : (Vector2)transform.position; } }
    private void Awake()
    {
        area = GetComponent<Collider2D>();
        events = GetComponent<NpcEventController>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
    }
    private void OnEnable() { DialogueSignals.TypedReplyCompleted += OnReply; }
    private void OnDisable()
    {
        DialogueSignals.TypedReplyCompleted -= OnReply;
        InteractionFocus.Exit(this);
    }
    private void Start()
    {
        if (IsSolved && definition.hideOnSolved) gameObject.SetActive(false);
    }
    public void Interact()
    {
        GameState state = GameState.Instance;
        if (!CanFocus || state == null || DialogueManager.Instance == null) return;
        bool carrying = !string.IsNullOrWhiteSpace(definition.requiredItemId) &&
            state.HasItem(definition.requiredItemId);
        if (!carrying && events != null && events.TryStartPendingEvent()) return;
        if (!definition.HasOffering(state) && carrying)
        {
            state.RemoveItem(definition.requiredItemId);
            foreach (ActionCommand action in definition.offeringActions) action?.Execute(state);
            state.SetFlag(definition.offeringGivenFlag);
        }
        if (definition.HasOffering(state)) Open(definition.questionDialogue);
        else
        {
            foreach (ActionCommand action in definition.meetingActions) action?.Execute(state);
            Open(definition.demandDialogue);
        }
    }
    private static void Open(DialogueData data)
    {
        if (data != null && DialogueManager.Instance != null) DialogueManager.Instance.StartDialogue(data);
    }
    private void OnReply(string npcId, string message)
    {
        if (!CanFocus || npcId != definition.npcId || !definition.HasOffering(GameState.Instance)) return;
        if (definition.puzzle == null || !definition.puzzle.TrySolve(GameState.Instance, message)) return;
        solving = true;
        if (area != null) area.enabled = false;
        InteractionFocus.Exit(this);
        StartCoroutine(SolvedSequence());
    }
    private IEnumerator SolvedSequence()
    {
        yield return new WaitForSeconds(definition.readReplyDelay);
        if (DialogueManager.Instance != null && definition.solvedDialogue != null)
        {
            DialogueManager.Instance.HideDialogue();
            Open(definition.solvedDialogue);
        }
        if (!definition.hideOnSolved) { solving = false; yield break; }
        yield return new WaitForSeconds(0.8f);
        float duration = Mathf.Max(0.01f, definition.fadeDuration);
        for (float timer = 0; timer < duration; timer += Time.deltaTime)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].color = new Color(colors[i].r, colors[i].g, colors[i].b,
                        colors[i].a * (1f - timer / duration));
            yield return null;
        }
        gameObject.SetActive(false);
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && CanFocus) InteractionFocus.Enter(this);
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) InteractionFocus.Exit(this);
    }
}
