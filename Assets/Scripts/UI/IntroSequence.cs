using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The cold open: black screen, narration fading in line by line, then a slow
/// fade into Room01. Started by the title menu's "new game", so no scene
/// wiring can go stale, and plays at most once per session.
/// </summary>
public class IntroSequence : MonoBehaviour
{
    private const string IntroScene = "Room01";

    public static bool IsPlaying { get; private set; }

    private static bool hasPlayed;

    [Header("Narration")]
    [TextArea(1, 3)]
    [SerializeField]
    private string[] lines =
    {
        "ค.ศ. 2001",
        "ข้าพเจ้าอธิบายมิได้ว่ามาอยู่ ณ ที่แห่งนี้ได้อย่างไร",
        "ข้าพเจ้าเป็นเพียงพนักงานของบริษัทเฟอร์นิเจอร์แห่งหนึ่ง\nไม่มีสิ่งใดในชีวิตที่ควรค่าแก่การถูกเลือก",
        "คืนนั้นข้าพเจ้ากลับถึงบ้าน วางกุญแจลงบนโต๊ะ\nแล้วเผลอหลับไปทั้งที่ยังไม่ได้ถอดรองเท้า",
        "...นั่นคือสิ่งสุดท้ายที่ข้าพเจ้าจำได้",
        "เมื่อลืมตาขึ้นอีกครา ไม่มีประตูที่ข้าพเจ้าเดินเข้ามา\nไม่มีเสียงจากภายนอก ไม่มีวันเวลาใดให้นับ",
        "มีเพียงห้องห้องหนึ่ง\nที่ดูราวกับจำใบหน้าข้าพเจ้าได้ดีกว่าที่ข้าพเจ้าจำมันได้",
    };

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float fadeInDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float holdDuration = 2.6f;
    [SerializeField, Min(0.1f)] private float fadeOutDuration = 0.9f;
    [SerializeField, Min(0.1f)] private float roomRevealDuration = 2.2f;

    private float textAlpha;
    private float curtainAlpha = 1f;
    private string currentLine = string.Empty;
    private Texture2D blackTexture;
    private GUIStyle narrationStyle;
    private GUIStyle skipStyle;

    public static void Play(bool force = false)
    {
        if (!force && hasPlayed)
        {
            return;
        }

        if (SceneManager.GetActiveScene().name != IntroScene)
        {
            return;
        }

        // Clean up any stale IntroSequence instance if present
        IntroSequence existing = FindObjectOfType<IntroSequence>();
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        hasPlayed = true;
        // Raised immediately so nothing moves in the frame before Start runs.
        IsPlaying = true;
        GameObject host = new GameObject("IntroSequence");
        host.AddComponent<IntroSequence>();
    }

    /// <summary>
    /// Lets a fresh play session replay the intro after domain reload is
    /// disabled in the editor.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        hasPlayed = false;
        IsPlaying = false;
    }

    private void Awake()
    {
        blackTexture = new Texture2D(1, 1);
        blackTexture.SetPixel(0, 0, Color.black);
        blackTexture.Apply();
    }

    private void Start()
    {
        StartCoroutine(PlayIntro());
    }

    private void OnDestroy()
    {
        IsPlaying = false;
        if (blackTexture != null)
        {
            Destroy(blackTexture);
        }
    }

    private IEnumerator PlayIntro()
    {
        hasPlayed = true;
        IsPlaying = true;
        curtainAlpha = 1f;

        bool skipped = false;
        foreach (string line in lines)
        {
            currentLine = line;

            skipped = SkipRequested();
            if (!skipped)
            {
                yield return FadeText(0f, 1f, fadeInDuration);

                float held = 0f;
                while (held < holdDuration && !SkipRequested())
                {
                    held += Time.unscaledDeltaTime;
                    yield return null;
                }
                skipped = SkipRequested();
            }

            yield return FadeText(textAlpha, 0f, fadeOutDuration);

            if (skipped)
            {
                break;
            }
        }

        currentLine = string.Empty;
        textAlpha = 0f;

        // Lift the curtain on the room itself.
        float timer = 0f;
        while (timer < roomRevealDuration)
        {
            timer += Time.unscaledDeltaTime;
            curtainAlpha = Mathf.Clamp01(1f - timer / roomRevealDuration);
            yield return null;
        }

        curtainAlpha = 0f;
        IsPlaying = false;
        Destroy(gameObject);
    }

    private static bool SkipRequested()
    {
        return Input.GetKeyDown(KeyCode.Escape) ||
               Input.GetKeyDown(KeyCode.Space) ||
               Input.GetKeyDown(KeyCode.Return) ||
               Input.GetMouseButtonDown(0);
    }

    private IEnumerator FadeText(float from, float to, float duration)
    {
        float timer = 0f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            textAlpha = Mathf.Lerp(from, to, timer / duration);
            if (SkipRequested())
            {
                break;
            }
            yield return null;
        }
        textAlpha = to;
    }

    private void OnGUI()
    {
        UiScale.Apply();
        if (curtainAlpha <= 0f)
        {
            return;
        }

        EnsureStyles();

        Color previous = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, curtainAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, UiScale.Width, UiScale.Height),
                        blackTexture, ScaleMode.StretchToFill);

        if (!string.IsNullOrEmpty(currentLine) && textAlpha > 0f)
        {
            GUI.color = new Color(0.86f, 0.84f, 0.78f, textAlpha * curtainAlpha);
            float width = Mathf.Min(900f, UiScale.Width - 120f);
            Rect rect = new Rect((UiScale.Width - width) * 0.5f,
                                 UiScale.Height * 0.5f - 130f, width, 260f);
            GUI.Label(rect, currentLine, narrationStyle);

            GUI.color = new Color(0.55f, 0.55f, 0.55f, textAlpha * curtainAlpha * 0.7f);
            GUI.Label(new Rect((UiScale.Width - width) * 0.5f,
                               UiScale.Height - 70f, width, 30f),
                      "กด Space, Esc หรือคลิกเมาส์เพื่อข้าม", skipStyle);
        }

        GUI.color = previous;
    }

    private void EnsureStyles()
    {
        if (narrationStyle != null)
        {
            return;
        }

        narrationStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
            wordWrap = true,
            richText = true,
        };
        narrationStyle.normal.textColor = Color.white;

        skipStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Italic,
        };
        skipStyle.normal.textColor = Color.white;
    }
}
