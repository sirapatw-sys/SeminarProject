using System.Collections;
using MysteryGame.Core;
using UnityEngine;

/// <summary>
/// Sena guards the celestial gate in Room02 across three stages:
///   1. she demands an offering and will not even state her riddle,
///   2. she accepts the offering and poses the riddle,
///   3. the riddle is answered and she dissolves, unlocking the gate.
/// </summary>
public class SenaInteraction : MonoBehaviour, IFocusable
{
    public const string OfferingItemId = "tome";
    public const string WantsOfferingFlag = "sena_wants_tome";
    public const string OfferingGivenFlag = "sena_offering_given";
    public const string PassedFlag = "sena_passed";

    public static SenaInteraction Instance { get; private set; }

    /// <summary>
    /// True while Sena is still guarding the celestial gate, so the door
    /// interactable knows to stay quiet and let her handle the E key.
    /// </summary>
    public static bool IsGuardingDoor
    {
        get
        {
            return Instance != null &&
                   Instance.isActiveAndEnabled &&
                   !Instance.hasBeenPassed;
        }
    }

    /// <summary>
    /// Sena only listens for the riddle answer once she has taken the
    /// offering; before that a correct guess must not open the gate.
    /// </summary>
    public static bool IsListeningForAnswer
    {
        get
        {
            GameState state = GameState.Instance;
            return IsGuardingDoor &&
                   state != null &&
                   state.HasFlag(OfferingGivenFlag);
        }
    }

    [Header("Dialogue & Portrait")]
    [SerializeField] private Sprite senaPortrait;

    [Tooltip("Stage 1: she demands the offering and gives no riddle.")]
    [SerializeField] private DialogueData senaDemandDialogue;

    [Tooltip("Stage 2: she accepts the offering and poses the riddle.")]
    [SerializeField] private DialogueData senaRiddleDialogue;

    [Header("Riddle Settings")]
    [TextArea(2, 4)]
    [SerializeField] private string riddleText =
        "ข้าวิ่งนำหน้าเจ้าอยู่เสมอ ทว่ามิเคยไปถึงแห่งหนใด " +
        "เจ้าเฝ้ารอข้าไปทั้งชีวิต แต่ครานที่ข้ามาถึงเจ้าจริงๆ " +
        "ชื่อของข้าก็เปลี่ยนไปเสียแล้ว... ข้าคือสิ่งใด?";

    /// <summary>The riddle is always shown with this translation under it.</summary>
    public const string RiddleEnglish =
        "(I am always running ahead of you, yet I never arrive. You spend your life " +
        "anticipating me, but the moment I reach you, my name has already changed. What am I?)";

    [SerializeField] private float readReplyDelay = 2.4f;
    [SerializeField] private float fadeDuration = 2.0f;

    private bool isFading;
    private NpcEventController eventController;
    private bool hasBeenPassed;
    private SpriteRenderer[] spriteRenderers;
    private Color[] spriteBaseColors;
    private Collider2D senaCollider;

    private void Awake()
    {
        Instance = this;
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        spriteBaseColors = new Color[spriteRenderers.Length];
        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            spriteBaseColors[index] = spriteRenderers[index].color;
        }
        senaCollider = GetComponent<Collider2D>();
        eventController = GetComponent<NpcEventController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // If Sena was already passed in this session, hide her immediately.
        if (GameState.Instance != null && GameState.Instance.HasFlag(PassedFlag))
        {
            hasBeenPassed = true;
            gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------ IFocusable
    // InteractionFocus picks the nearest interactable and calls Interact().

    private const string TalkPrompt = "กด E เพื่อสนทนากับเซนะ";

    public bool CanFocus
    {
        get { return !isFading && !hasBeenPassed; }
    }

    public string FocusPrompt
    {
        get { return TalkPrompt; }
    }

    public Vector2 FocusPoint
    {
        get
        {
            return senaCollider != null ? (Vector2)senaCollider.bounds.center : (Vector2)transform.position;
        }
    }

    public void Interact()
    {
        InteractWithSena();
    }

    public void InteractWithSena()
    {
        if (isFading || hasBeenPassed)
        {
            return;
        }

        GameState state = GameState.Instance;
        if (state != null && state.HasFlag(PassedFlag))
        {
            return;
        }

        if (DialogueManager.Instance == null)
        {
            return;
        }

        bool offeringGiven = state != null && state.HasFlag(OfferingGivenFlag);
        bool carryingOffering = state != null && state.HasItem(OfferingItemId);

        // A pending taunt plays first, unless the player has come to hand
        // over the offering: that must never be blocked by small talk.
        if (!carryingOffering && eventController != null &&
            eventController.TryStartPendingEvent())
        {
            return;
        }

        // Stage 2 begins the moment the player turns up holding the tome.
        if (!offeringGiven && carryingOffering)
        {
            AcceptOffering(state);
            return;
        }

        if (offeringGiven)
        {
            StartStageDialogue(senaRiddleDialogue, BuildRiddleFallbackLines());
            return;
        }

        if (state != null)
        {
            state.SetFlag("sena_met");
            state.SetFlag(WantsOfferingFlag);
        }

        StartStageDialogue(senaDemandDialogue, BuildDemandFallbackLines());
    }

    private void AcceptOffering(GameState state)
    {
        state.RemoveItem(OfferingItemId);
        state.SetFlag(OfferingGivenFlag);
        state.AddHistory("Gave Sena the unfinished tome");
        state.ChangeRelationship("Sena", 5);

        StartStageDialogue(senaRiddleDialogue, BuildRiddleFallbackLines());
    }

    /// <summary>
    /// Opens an authored DialogueData when one is wired up — that is what
    /// brings the portrait and the chat box the player types the answer into.
    /// The plain lines are only a last resort so the room stays playable.
    /// </summary>
    private void StartStageDialogue(DialogueData data, string[] fallbackLines)
    {
        if (data != null)
        {
            if (data.speakerPortrait == null)
            {
                Debug.LogWarning(
                    data.name + " has no speakerPortrait assigned, so Sena's " +
                    "portrait will not show during the conversation."
                );
            }

            DialogueManager.Instance.StartDialogue(data);
            return;
        }

        Debug.LogWarning(
            "SenaInteraction is missing a DialogueData for this stage; " +
            "falling back to plain lines without a chat box."
        );

        DialogueManager.Instance.StartDialogue(
            "เซนะ (Sena)",
            fallbackLines,
            false,
            senaPortrait
        );
    }

    private static string[] BuildDemandFallbackLines()
    {
        return new string[]
        {
            "หยุดเถิด มนุษย์ผู้เดินมาด้วยมืออันว่างเปล่า",
            "ข้าคือเซนะ ผู้เฝ้าประตูดวงดาวแห่งนี้มาแล้วสามพันฤดู",
            "ผู้ใดใคร่ฟังปริศนาของข้า ผู้นั้นต้องมีของถวายเสียก่อน — " +
            "จงเสาะหา 'จารึกที่ยังเขียนมิจบ' ในหอสมุดแห่งนี้มาให้ข้า"
        };
    }

    private string[] BuildRiddleFallbackLines()
    {
        return new string[]
        {
            "ข้ายอมรับของถวายของเจ้าแล้ว จงฟังปริศนาให้ดี:",
            "\"" + riddleText + "\"\n" + RiddleEnglish,
            "จงพิมพ์คำตอบลงในช่องเบื้องล่างแล้วกดส่งมาเถิด"
        };
    }

    /// <summary>
    /// Every answer that opens the gate, compared with spaces removed. Sena's
    /// profile (Sena.asset) quotes the same list to the AI, so she never
    /// praises an answer this check would then refuse.
    /// </summary>
    public static readonly string[] AcceptedAnswers =
    {
        "พรุ่งนี้", "วันพรุ่งนี้", "วันถัดไป", "วันต่อไป", "วันรุ่งขึ้น", "อนาคต",
        "tomorrow", "nextday", "future", "tmr",
    };

    public static bool IsCorrectRiddleAnswer(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = System.Text.RegularExpressions.Regex.Replace(
            text.ToLowerInvariant(), @"\s+", string.Empty);
        foreach (string answer in AcceptedAnswers)
        {
            if (normalized.Contains(answer))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Called when player submits the correct answer to Sena.
    /// </summary>
    public void HandleRiddleSolved()
    {
        if (isFading || hasBeenPassed)
        {
            return;
        }

        GameState state = GameState.Instance;
        if (state != null && !state.HasFlag(OfferingGivenFlag))
        {
            // She has not asked anything yet, so there is nothing to answer.
            return;
        }

        isFading = true;
        hasBeenPassed = true;

        if (state != null)
        {
            state.SetFlag(PassedFlag);
            state.SetFlag("room02_door_unlocked");
            state.AddHistory("Solved Sena's riddle (Tomorrow)");
        }

        if (senaCollider != null)
        {
            senaCollider.enabled = false;
        }

        InteractionFocus.Exit(this);
        StartCoroutine(SenaSolvedSequence());
    }

    private IEnumerator SenaSolvedSequence()
    {
        // Let the player finish reading Sena's reply inside the chat panel.
        yield return new WaitForSeconds(readReplyDelay);

        // The chat panel is still open from the typed answer, and
        // StartDialogue refuses to open on top of it — so close it first.
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.HideDialogue();

            string[] satisfiedLines =
            {
                "...หึ เจ้าตอบถูกจนได้ฤๅ",
                "สามพันฤดูที่ข้ายืนเฝ้าประตูนี้ มีเพียงหยิบมือที่เอ่ยคำนั้นได้... " +
                "ข้ายอมรับในปัญญาของเจ้าในครานี้ มนุษย์",
                "ประตูดวงดาวเบื้องหลังข้าเปิดแล้ว จงก้าวไปสู่วันที่เจ้าเฝ้ารอเถิด " +
                "...แม้เจ้าจะไม่มีวันไปถึงมันก็ตาม"
            };

            DialogueManager.Instance.StartDialogue(
                "เซนะ (Sena)",
                satisfiedLines,
                false,
                senaPortrait
            );
        }

        // She dissolves while the player reads her farewell.
        yield return new WaitForSeconds(0.8f);
        yield return FadeOut();

        AiSettingsPanel.SetInteractionPrompt("ประตูดวงดาวปลดล็อคแล้ว!");
        yield return new WaitForSeconds(1.5f);
        AiSettingsPanel.SetInteractionPrompt(string.Empty);

        gameObject.SetActive(false);
    }

    private IEnumerator FadeOut()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            yield break;
        }

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            ApplyAlpha(alpha);
            yield return null;
        }

        ApplyAlpha(0f);
    }

    private void ApplyAlpha(float alpha)
    {
        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            SpriteRenderer renderer = spriteRenderers[index];
            if (renderer == null)
            {
                continue;
            }

            Color baseColor = spriteBaseColors[index];
            renderer.color = new Color(
                baseColor.r,
                baseColor.g,
                baseColor.b,
                baseColor.a * alpha
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isFading)
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
