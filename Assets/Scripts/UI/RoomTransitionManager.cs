using System.Collections;
using MysteryGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomTransitionManager : MonoBehaviour
{
    private static RoomTransitionManager _instance;

    public static RoomTransitionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<RoomTransitionManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("RoomTransitionManager");
                    _instance = go.AddComponent<RoomTransitionManager>();
                }
            }
            return _instance;
        }
    }

    [SerializeField] private float messageDisplayDuration = 2.0f;
    public string LastError { get; private set; }

    private float currentAlpha = 0f;
    private bool isTransitioning = false;
    private bool showingEnding;
    private string currentMessage = string.Empty;

    /// <summary>
    /// True while a fade or the ending card is on screen. Reads the field
    /// directly so asking never spawns a manager as a side effect.
    /// </summary>
    public static bool IsBusy
    {
        get
        {
            return _instance != null &&
                   (_instance.isTransitioning || _instance.showingEnding);
        }
    }
    private Texture2D blackTexture;
    private GUIStyle messageStyle;
    private GUIStyle hintStyle;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        blackTexture = new Texture2D(1, 1);
        blackTexture.SetPixel(0, 0, Color.black);
        blackTexture.Apply();
    }

    public void TransitionToRoom(
        string targetSceneName,
        string transitionMessage = "คุณใช้กุญแจเปิดประตูสำเร็จ...\nก้าวเดินเข้าสู่ห้องถัดไป (Room 02)",
        float fadeDuration = 1.0f)
    {
        TryTransitionToRoom(targetSceneName, transitionMessage, fadeDuration);
    }

    public static bool CanLoadRoom(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
        var game = GameDefinition.Current;
        return game == null || (game.rooms != null && game.rooms.Exists(room =>
            room != null && room.roomId == sceneName));
    }

    /// <summary>False means no transition or state change was started.</summary>
    public bool TryTransitionToRoom(string sceneName, string message, float fadeDuration = 1f,
        System.Action onFailure = null)
    {
        LastError = null;
        if (IsBusy) { LastError = "กำลังเปลี่ยนห้องอยู่ กรุณารอก่อน"; return false; }
        if (!CanLoadRoom(sceneName))
        {
            LastError = "ไม่พบห้องที่ต้องการโหลด: " + sceneName;
            Debug.LogWarning(LastError);
            return false;
        }
        StartCoroutine(DoTransition(sceneName, message, Mathf.Max(0f, fadeDuration), onFailure));
        return true;
    }

    public static void CloseRoomOverlays()
    {
        AiSettingsPanel.Close();
        if (DialogueManager.Instance != null) DialogueManager.Instance.HideDialogue();
        KeypadLockUI.Close();
        ItemPopupUI.CancelAll();
        JournalUI.Close();
    }

    private IEnumerator DoTransition(
        string targetSceneName,
        string transitionMessage,
        float fadeDuration,
        System.Action onFailure)
    {
        isTransitioning = true;
        currentMessage = transitionMessage;
        bool loaded = false;
        try
        {
            CloseRoomOverlays();
            float timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                currentAlpha = Mathf.Clamp01(timer / fadeDuration);
                yield return null;
            }
            currentAlpha = 1f;
            yield return new WaitForSecondsRealtime(messageDisplayDuration);

            AsyncOperation asyncLoad = null;
            try { asyncLoad = SceneManager.LoadSceneAsync(targetSceneName); }
            catch (System.Exception ex) { LastError = "โหลดห้องไม่สำเร็จ: " + ex.GetType().Name; }
            if (asyncLoad == null)
            {
                if (string.IsNullOrEmpty(LastError)) LastError = "โหลดห้องไม่สำเร็จ: " + targetSceneName;
                Debug.LogWarning(LastError);
                yield break;
            }
            while (!asyncLoad.isDone) yield return null;
            if (SceneManager.GetActiveScene().name != targetSceneName)
            {
                LastError = "โหลดห้องไม่สำเร็จ: " + targetSceneName;
                Debug.LogWarning(LastError);
                yield break;
            }
            loaded = true;
            // Never create a checkpoint for a scene that did not load.
            if (GameState.Instance != null)
            {
                GameState.Instance.SetCurrentScene(targetSceneName);
                GameState.Instance.AddHistory("Entered " + targetSceneName);
            }
            yield return new WaitForSecondsRealtime(0.3f);
            SaveSystem.Save();

            currentMessage = string.Empty;
            timer = 0f;
            while (timer < fadeDuration)
            {
                timer += Time.unscaledDeltaTime;
                currentAlpha = Mathf.Clamp01(1f - timer / fadeDuration);
                yield return null;
            }
        }
        finally
        {
            currentMessage = string.Empty;
            currentAlpha = 0f;
            isTransitioning = false;
            if (!loaded && onFailure != null)
            {
                try { onFailure(); }
                catch (System.Exception ex) { Debug.LogWarning("Load recovery failed: " + ex.GetType().Name); }
            }
        }
    }

    /// <summary>
    /// Fades out on the last door of the demo and holds the closing card
    /// until the player quits.
    /// </summary>
    public void PlayEnding(string endingMessage)
    {
        if (isTransitioning || showingEnding)
        {
            return;
        }

        StartCoroutine(DoEnding(endingMessage));
    }

    private IEnumerator DoEnding(string endingMessage)
    {
        isTransitioning = true;
        currentMessage = endingMessage;
        CloseRoomOverlays();

        if (GameState.Instance != null)
        {
            GameState.Instance.AddHistory("Reached the end of the demo");
        }
        SaveSystem.Save();

        float timer = 0f;
        while (timer < 2.5f)
        {
            timer += Time.unscaledDeltaTime;
            currentAlpha = Mathf.Clamp01(timer / 2.5f);
            yield return null;
        }

        currentAlpha = 1f;
        isTransitioning = false;
        showingEnding = true;
    }

    private void Update()
    {
        if (showingEnding && Input.GetKeyDown(KeyCode.Escape))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void OnGUI()
    {
        UiScale.Apply();
        if (currentAlpha <= 0f && !isTransitioning)
        {
            return;
        }

        EnsureStyles();

        // Draw black overlay
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, currentAlpha);
        GUI.DrawTexture(
            new Rect(0f, 0f, UiScale.Width, UiScale.Height),
            blackTexture,
            ScaleMode.StretchToFill
        );
        GUI.color = oldColor;

        // Draw transition message if present and mostly black
        if (!string.IsNullOrEmpty(currentMessage) && currentAlpha >= 0.8f)
        {
            float msgAlpha = Mathf.Clamp01((currentAlpha - 0.8f) / 0.2f);
            Color textOldColor = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.85f, 0.4f, msgAlpha);

            float panelWidth = Mathf.Min(700f, UiScale.Width - 60f);
            float panelHeight = 160f;
            Rect msgRect = new Rect(
                (UiScale.Width - panelWidth) * 0.5f,
                (UiScale.Height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight
            );

            GUI.Label(msgRect, currentMessage, messageStyle);

            GUI.contentColor = new Color(0.8f, 0.85f, 0.95f, msgAlpha * 0.7f);
            Rect hintRect = new Rect(
                (UiScale.Width - panelWidth) * 0.5f,
                msgRect.yMax + 10f,
                panelWidth,
                40f
            );
            GUI.Label(
                hintRect,
                showingEnding
                    ? "— จบเดโม บทที่ 1 · ขอบคุณที่เล่น · กด Esc เพื่อออกจากเกม —"
                    : "— กำลังโหลดข้อมูลห้อง —",
                hintStyle);

            GUI.contentColor = textOldColor;
        }
    }

    private void EnsureStyles()
    {
        if (messageStyle != null)
        {
            return;
        }

        messageStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Italic,
            wordWrap = true
        };
    }

    private void OnDestroy()
    {
        if (blackTexture != null)
        {
            Destroy(blackTexture);
        }
    }
}
