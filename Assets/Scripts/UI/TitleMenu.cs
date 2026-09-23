using UnityEngine;
using UnityEngine.SceneManagement;
using MysteryGame.Core;

/// <summary>
/// The start menu shown once per session when the game boots into Room01,
/// before the intro. Also owns the save hotkeys (F5 save, F9 load) and the
/// little toast that confirms them, since it lives for the whole session.
/// </summary>
public class TitleMenu : MonoBehaviour
{
    private const string FirstScene = "Room01";

    public static bool IsOpen { get; private set; }

    private static bool shownThisSession;
    private static TitleMenu instance;

    private Texture2D blackTexture;
    private Texture2D buttonTexture;
    private Texture2D buttonHoverTexture;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle buttonStyle;
    private GUIStyle noteStyle;
    private GUIStyle toastStyle;
    private string toast;
    private float toastUntil;
    private float openedAt;
    private bool pendingIntroPlay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance == null)
        {
            GameObject host = new GameObject("TitleMenu");
            instance = host.AddComponent<TitleMenu>();
            DontDestroyOnLoad(host);
        }

        // Only a cold start in Room01 gets the menu. Opening Room02/03
        // straight from the editor drops you into that room as before.
        if (!shownThisSession && SceneManager.GetActiveScene().name == FirstScene)
        {
            shownThisSession = true;
            IsOpen = true;
            instance.openedAt = Time.unscaledTime;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        shownThisSession = false;
        IsOpen = false;
        instance = null;
    }

    private void Awake()
    {
        blackTexture = MakeTexture(new Color(0.02f, 0.025f, 0.035f, 1f));
        buttonTexture = MakeTexture(new Color(0.09f, 0.16f, 0.21f, 0.95f));
        buttonHoverTexture = MakeTexture(new Color(0.16f, 0.3f, 0.38f, 1f));
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingIntroPlay && scene.name == FirstScene)
        {
            pendingIntroPlay = false;
            IntroSequence.Play(force: true);
        }
    }

    private void OnDestroy()
    {
        Destroy(blackTexture);
        Destroy(buttonTexture);
        Destroy(buttonHoverTexture);
    }

    private void Update()
    {
        if (IsOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                StartNewGame();
                return;
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }
            return;
        }

        if (IntroSequence.IsPlaying || RoomTransitionManager.IsBusy)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            bool saved = SaveSystem.Save();
            ShowToast(saved ? "บันทึกเกมแล้ว (F9 เพื่อโหลด)" : "บันทึกเกมไม่สำเร็จ");
            SfxPlayer.Play(SfxPlayer.Cue.Save);
        }
        else if (Input.GetKeyDown(KeyCode.F9) && !DialogueManager.IsDialogueOpen)
        {
            if (!SaveSystem.Load())
            {
                ShowToast("ยังไม่มีเกมที่บันทึกไว้");
            }
        }
    }

    private void ShowToast(string message)
    {
        toast = message;
        toastUntil = Time.unscaledTime + 2.2f;
    }

    private void StartNewGame()
    {
        SfxPlayer.Play(SfxPlayer.Cue.Interact);
        IsOpen = false;

        if (GameState.Instance != null)
        {
            GameState.Instance.ResetState();
        }

        SaveSystem.Delete();

        if (SceneManager.GetActiveScene().name != FirstScene)
        {
            pendingIntroPlay = true;
            SceneManager.LoadScene(FirstScene);
        }
        else
        {
            // Reset player position in Room01
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = new Vector3(0f, -2f, 0f);
            }
            IntroSequence.Play(force: true);
        }
    }

    private bool IsButtonClicked(Rect rect, string text, GUIStyle style, bool enabled = true)
    {
        if (!enabled)
        {
            GUI.enabled = false;
            GUI.Button(rect, text, style);
            GUI.enabled = true;
            return false;
        }

        bool clicked = GUI.Button(rect, text, style);

        Event e = Event.current;
        if (e != null)
        {
            Vector2 mousePos = e.mousePosition;
            Vector2 localMouse = GUI.matrix.inverse.MultiplyPoint3x4(mousePos);
            bool isInside = rect.Contains(mousePos) || rect.Contains(localMouse);

            if (!clicked && e.type == EventType.MouseDown && e.button == 0 && isInside)
            {
                clicked = true;
                e.Use();
            }
        }

        return clicked;
    }

    private void OnGUI()
    {
        UiScale.Apply();
        EnsureStyles();
        GUI.depth = -10; // in front of game elements so clicks are received reliably

        if (!IsOpen)
        {
            if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
            {
                GUI.Box(new Rect(UiScale.Width - 380f, UiScale.Height - 90f, 350f, 50f),
                        toast, toastStyle);
            }
            return;
        }

        float fade = Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.8f);
        Color previous = GUI.color;
        GUI.DrawTexture(new Rect(0f, 0f, UiScale.Width, UiScale.Height),
                        blackTexture, ScaleMode.StretchToFill);

        GUI.color = new Color(1f, 1f, 1f, fade);
        float width = Mathf.Min(760f, UiScale.Width - 60f);
        float x = (UiScale.Width - width) * 0.5f;
        float y = UiScale.Height * 0.2f;
        GUI.Label(new Rect(x, y, width, 90f), "ห้องที่จำใบหน้าเราได้", titleStyle);
        GUI.Label(new Rect(x, y + 88f, width, 40f),
                  "AI Mystery Escape Room · บทที่ 1", subtitleStyle);

        float buttonWidth = 380f;
        float bx = (UiScale.Width - buttonWidth) * 0.5f;
        float by = y + 190f;

        Rect newGameRect = new Rect(bx, by, buttonWidth, 60f);
        if (IsButtonClicked(newGameRect, "เริ่มเกมใหม่", buttonStyle))
        {
            StartNewGame();
        }
        by += 76f;

        string continueLabel = SaveSystem.HasSave
            ? "เล่นต่อ  (" + SaveSystem.LastSavedAt() + ")"
            : "เล่นต่อ  (ยังไม่มีเกมที่บันทึกไว้)";
        Rect continueRect = new Rect(bx, by, buttonWidth, 60f);
        if (IsButtonClicked(continueRect, continueLabel, buttonStyle, SaveSystem.HasSave))
        {
            SfxPlayer.Play(SfxPlayer.Cue.Interact);
            IsOpen = false;
            if (!SaveSystem.Load())
            {
                StartNewGame();
            }
        }
        by += 76f;

        Rect aiRect = new Rect(bx, by, buttonWidth, 60f);
        if (IsButtonClicked(aiRect, "ตั้งค่า AI", buttonStyle))
        {
            SfxPlayer.Play(SfxPlayer.Cue.Interact);
            AiSettingsPanel.Open();
        }
        by += 76f;

        Rect quitRect = new Rect(bx, by, buttonWidth, 60f);
        if (IsButtonClicked(quitRect, "ออกจากเกม", buttonStyle))
        {
            SfxPlayer.Play(SfxPlayer.Cue.Interact);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        GUI.Label(new Rect(x, UiScale.Height - 70f, width, 30f),
                  "ระหว่างเล่น: F5 บันทึก · F9 โหลด · F10 ตั้งค่า AI · กด Space หรือ Enter เพื่อเริ่ม",
                  noteStyle);
        GUI.color = previous;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 48,
            fontStyle = FontStyle.Bold,
        };
        titleStyle.normal.textColor = new Color(0.94f, 0.82f, 0.55f);

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Italic,
        };
        subtitleStyle.normal.textColor = new Color(0.7f, 0.74f, 0.8f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.hover.background = buttonHoverTexture;
        buttonStyle.active.background = buttonHoverTexture;
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = new Color(1f, 0.9f, 0.6f);
        buttonStyle.active.textColor = new Color(1f, 0.9f, 0.6f);

        noteStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
        };
        noteStyle.normal.textColor = new Color(0.55f, 0.58f, 0.62f);

        toastStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
        };
        toastStyle.normal.background = buttonTexture;
        toastStyle.normal.textColor = Color.white;
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
