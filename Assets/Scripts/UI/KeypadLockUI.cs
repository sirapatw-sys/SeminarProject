using System;
using UnityEngine;

public class KeypadLockUI : MonoBehaviour
{
    private static KeypadLockUI _instance;

    public static KeypadLockUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<KeypadLockUI>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("KeypadLockUI");
                    _instance = go.AddComponent<KeypadLockUI>();
                }
            }
            return _instance;
        }
    }

    // Drawn larger than the other overlays so the keys are easy to hit.
    private const float PopupMagnify = 1.4f;

    public static bool IsOpen { get; private set; }

    private string targetCode = "4592";
    private string currentInput = "";
    private string lockTitle = "แม่กุญแจรหัสของลิ้นชัก (Drawer Lock)";
    private string lockHint = "ใส่รหัสตัวเลข 4 หลักเพื่อปลดล็อคลิ้นชัก";
    private string statusMessage = "";
    private Color statusColor = Color.white;
    private Action onUnlockSuccess;

    private Texture2D dimTexture;
    private Texture2D panelTexture;
    private Texture2D digitBoxTexture;
    private Texture2D buttonNormalTexture;
    private Texture2D buttonActiveTexture;
    private Texture2D buttonUnlockTexture;
    private Texture2D buttonUnlockHoverTexture;

    private GUIStyle titleStyle;
    private GUIStyle hintStyle;
    private GUIStyle digitStyle;
    private GUIStyle keyStyle;
    private GUIStyle unlockKeyStyle;
    private GUIStyle statusStyle;
    private GUIStyle closeStyle;

    private float openTime;
    private float shakeTimer;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void Show(
        string targetCode,
        string title = null,
        string hint = null,
        Action onSuccess = null)
    {
        KeypadLockUI ui = Instance;
        ui.targetCode = targetCode ?? "4592";
        if (!string.IsNullOrWhiteSpace(title)) ui.lockTitle = title;
        if (!string.IsNullOrWhiteSpace(hint)) ui.lockHint = hint;
        ui.onUnlockSuccess = onSuccess;
        ui.currentInput = "";
        ui.statusMessage = "";
        ui.openTime = Time.unscaledTime;
        ui.shakeTimer = 0f;
        IsOpen = true;
    }

    public static void Close()
    {
        if (_instance != null)
        {
            _instance.currentInput = "";
            _instance.statusMessage = "";
        }
        IsOpen = false;
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
        }

        // Handle Keyboard input
        if (Time.unscaledTime - openTime > 0.15f)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            // Numeric keys
            for (int i = 0; i <= 9; i++)
            {
                KeyCode alphaKey = KeyCode.Alpha0 + i;
                KeyCode keypadKey = KeyCode.Keypad0 + i;
                if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
                {
                    AppendDigit(i.ToString());
                    return;
                }
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                DeleteDigit();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                TrySubmit();
                return;
            }
        }
    }

    private void AppendDigit(string digit)
    {
        if (currentInput.Length < 4)
        {
            currentInput += digit;
            statusMessage = "";
            if (currentInput.Length == 4)
            {
                // Optional auto-check or wait for enter
            }
        }
    }

    private void DeleteDigit()
    {
        if (currentInput.Length > 0)
        {
            currentInput = currentInput.Substring(0, currentInput.Length - 1);
            statusMessage = "";
        }
    }

    private void TrySubmit()
    {
        if (currentInput.Length < 4)
        {
            statusMessage = "กรุณาใส่รหัสตัวเลขให้ครบ 4 หลัก";
            statusColor = new Color(0.95f, 0.75f, 0.3f);
            shakeTimer = 0.3f;
            return;
        }

        if (currentInput == targetCode)
        {
            statusMessage = "รหัสถูกต้อง! ปลดล็อคสำเร็จ...";
            statusColor = new Color(0.3f, 0.95f, 0.5f);
            IsOpen = false;
            Action callback = onUnlockSuccess;
            onUnlockSuccess = null;
            callback?.Invoke();
        }
        else
        {
            statusMessage = "รหัสไม่ถูกต้อง! สลักยังคงล็อคอยู่";
            statusColor = new Color(0.95f, 0.3f, 0.3f);
            shakeTimer = 0.4f;
            currentInput = "";
        }
    }

    private void OnGUI()
    {
        GUI.depth = ModalGui.FrontDepth;
        UiScale.Apply(PopupMagnify, 540f);
        if (!IsOpen)
        {
            return;
        }

        EnsureStyles();

        // 1. Fullscreen Dim
        GUI.DrawTexture(
            new Rect(0f, 0f, UiScale.Width, UiScale.Height),
            dimTexture,
            ScaleMode.StretchToFill
        );

        // 2. Center Modal Panel. A wrong code shakes the digits only: keys
        // that moved under the mouse could miss the click.
        float shakeOffset = shakeTimer > 0f ? Mathf.Sin(shakeTimer * 50f) * 6f : 0f;
        float panelWidth = Mathf.Min(420f, UiScale.Width - 30f);
        float panelHeight = 520f;
        float panelX = (UiScale.Width - panelWidth) * 0.5f;
        float panelY = (UiScale.Height - panelHeight) * 0.5f;
        Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        // Panel background & gold border
        GUI.DrawTexture(panelRect, panelTexture, ScaleMode.StretchToFill);
        DrawRectOutline(panelRect, new Color(0.78f, 0.58f, 0.22f, 0.95f), 2);

        // Header Title
        Rect headerRect = new Rect(panelX + 20f, panelY + 18f, panelWidth - 40f, 32f);
        GUI.Label(headerRect, lockTitle, titleStyle);

        // Subtitle / Hint
        Rect hintRect = new Rect(panelX + 20f, panelY + 50f, panelWidth - 40f, 22f);
        GUI.Label(hintRect, lockHint, hintStyle);

        // 3. 4-Digit Display
        float boxWidth = 54f;
        float boxHeight = 64f;
        float boxSpacing = 14f;
        float totalBoxWidth = (boxWidth * 4) + (boxSpacing * 3);
        float startBoxX = panelX + (panelWidth - totalBoxWidth) * 0.5f + shakeOffset;
        float boxY = panelY + 84f;

        for (int i = 0; i < 4; i++)
        {
            Rect bRect = new Rect(startBoxX + i * (boxWidth + boxSpacing), boxY, boxWidth, boxHeight);
            GUI.DrawTexture(bRect, digitBoxTexture, ScaleMode.StretchToFill);
            DrawRectOutline(bRect, new Color(0.45f, 0.55f, 0.65f, 0.6f), 1);

            string charToShow = i < currentInput.Length ? currentInput[i].ToString() : "—";
            GUI.Label(bRect, charToShow, digitStyle);
        }

        // Status Message (if any)
        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            Rect sRect = new Rect(panelX + 20f, boxY + boxHeight + 6f, panelWidth - 40f, 22f);
            statusStyle.normal.textColor = statusColor;
            GUI.Label(sRect, statusMessage, statusStyle);
        }

        // 4. Numeric Keypad (3x4 Grid)
        float btnW = 86f;
        float btnH = 50f;
        float btnGap = 10f;
        float totalKeypadW = (btnW * 3) + (btnGap * 2);
        float startKeypadX = panelX + (panelWidth - totalKeypadW) * 0.5f;
        float keypadY = panelY + 185f;

        string[,] keys = new string[,]
        {
            { "1", "2", "3" },
            { "4", "5", "6" },
            { "7", "8", "9" },
            { "ลบ", "0", "ตกลง" }
        };

        for (int r = 0; r < 4; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                Rect kRect = new Rect(
                    startKeypadX + c * (btnW + btnGap),
                    keypadY + r * (btnH + btnGap),
                    btnW,
                    btnH
                );

                string keyVal = keys[r, c];
                GUIStyle currentKeyStyle = keyStyle;
                if (keyVal == "ตกลง")
                {
                    currentKeyStyle = unlockKeyStyle;
                }

                if (ModalGui.Button(kRect, keyVal, currentKeyStyle))
                {
                    if (keyVal == "ลบ")
                    {
                        DeleteDigit();
                    }
                    else if (keyVal == "ตกลง")
                    {
                        TrySubmit();
                    }
                    else
                    {
                        AppendDigit(keyVal);
                    }
                }
            }
        }

        // Close button at bottom
        float closeW = 160f;
        float closeH = 34f;
        Rect closeRect = new Rect(
            panelX + (panelWidth - closeW) * 0.5f,
            panelY + panelHeight - 48f,
            closeW,
            closeH
        );

        if (ModalGui.Button(closeRect, "ปิด  [ Esc ]", closeStyle))
        {
            Close();
        }

        ModalGui.BlockMouse();
    }

    private static void DrawRectOutline(Rect rect, Color color, int thickness)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        dimTexture = CreateColorTexture(new Color(0.01f, 0.015f, 0.025f, 0.82f));
        panelTexture = CreateColorTexture(new Color(0.035f, 0.055f, 0.085f, 0.98f));
        digitBoxTexture = CreateColorTexture(new Color(0.06f, 0.09f, 0.13f, 1f));
        buttonNormalTexture = CreateColorTexture(new Color(0.12f, 0.18f, 0.26f, 1f));
        buttonActiveTexture = CreateColorTexture(new Color(0.18f, 0.28f, 0.38f, 1f));
        buttonUnlockTexture = CreateColorTexture(new Color(0.78f, 0.58f, 0.22f, 1f));
        buttonUnlockHoverTexture = CreateColorTexture(new Color(0.92f, 0.72f, 0.32f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 19,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(0.95f, 0.82f, 0.45f);

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Normal
        };
        hintStyle.normal.textColor = new Color(0.7f, 0.78f, 0.88f);

        digitStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        digitStyle.normal.textColor = new Color(1f, 0.92f, 0.6f);

        keyStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        keyStyle.normal.background = buttonNormalTexture;
        keyStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
        keyStyle.hover.background = buttonActiveTexture;
        keyStyle.hover.textColor = Color.white;
        keyStyle.active.background = buttonActiveTexture;
        keyStyle.active.textColor = Color.white;

        unlockKeyStyle = new GUIStyle(keyStyle);
        unlockKeyStyle.normal.background = buttonUnlockTexture;
        unlockKeyStyle.normal.textColor = new Color(0.05f, 0.05f, 0.05f);
        unlockKeyStyle.hover.background = buttonUnlockHoverTexture;
        unlockKeyStyle.hover.textColor = unlockKeyStyle.normal.textColor;
        unlockKeyStyle.active.background = buttonUnlockHoverTexture;
        unlockKeyStyle.active.textColor = unlockKeyStyle.normal.textColor;

        statusStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold
        };

        closeStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13
        };
        closeStyle.normal.background = buttonNormalTexture;
        closeStyle.normal.textColor = new Color(0.75f, 0.8f, 0.85f);
        closeStyle.hover.background = buttonActiveTexture;
        closeStyle.hover.textColor = Color.white;
        closeStyle.active.background = buttonActiveTexture;
        closeStyle.active.textColor = Color.white;
    }

    private static Texture2D CreateColorTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (dimTexture != null) Destroy(dimTexture);
        if (panelTexture != null) Destroy(panelTexture);
        if (digitBoxTexture != null) Destroy(digitBoxTexture);
        if (buttonNormalTexture != null) Destroy(buttonNormalTexture);
        if (buttonActiveTexture != null) Destroy(buttonActiveTexture);
        if (buttonUnlockTexture != null) Destroy(buttonUnlockTexture);
        if (buttonUnlockHoverTexture != null) Destroy(buttonUnlockHoverTexture);
    }
}
