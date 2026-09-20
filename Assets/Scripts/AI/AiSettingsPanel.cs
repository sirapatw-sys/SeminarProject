using UnityEngine;

public class AiSettingsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static string InteractionPrompt { get; private set; }

    private readonly string[] providerLabels =
        { "OpenAI", "KKU IntelSphere", "Google Gemini", "Custom" };

    private readonly string[] openAiModels =
        { "gpt-4o-mini", "gpt-4.1-mini", "gpt-4.1" };

    private readonly string[] geminiModels =
        { "gemini-3.6-flash", "gemini-3.6-pro", "gemini-3.5-flash" };

    private bool isOpen;
    private Rect windowRect = new Rect(0f, 0f, 640f, 610f);
    private int providerIndex;
    private int previousProviderIndex;
    private int modelPresetIndex;
    private int geminiPresetIndex;
    private string model = "gpt-5.6-luna";
    private string endpoint = string.Empty;
    private string apiKey = string.Empty;
    private string status = "Settings not applied yet";
    private bool testInProgress;
    private bool modelFetchInProgress;
    private string[] fetchedModels;
    private int fetchedModelIndex;
    private GUIStyle windowStyle;
    private GUIStyle titleStyle;
    private GUIStyle noteStyle;
    private GUIStyle labelStyle;
    private GUIStyle fieldStyle;
    private GUIStyle tabStyle;
    private GUIStyle actionButtonStyle;
    private GUIStyle launcherStyle;
    private GUIStyle hudStyle;
    private Texture2D panelTexture;
    private Texture2D dimTexture;
    private Texture2D fieldTexture;
    private Texture2D buttonTexture;
    private Texture2D accentTexture;

    private void Start()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null)
        {
            return;
        }

        providerIndex = (int)generator.Provider;
        previousProviderIndex = providerIndex;
        model = generator.Model;
        endpoint = generator.ApiUrl;

        for (int index = 0; index < openAiModels.Length; index++)
        {
            if (openAiModels[index] == model)
            {
                modelPresetIndex = index;
                break;
            }
        }

        for (int index = 0; index < geminiModels.Length; index++)
        {
            if (geminiModels[index] == model)
            {
                geminiPresetIndex = index;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = generator.GetApiKey();
        }

        status = generator.CanGenerate
            ? "AI is ready"
            : "Fallback dialogue active - enter an API key";
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            isOpen = !isOpen;
            IsOpen = isOpen;
        }
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (!isOpen && !DialogueManager.IsDialogueOpen)
        {
            GUI.Box(
                new Rect(22f, 22f, 285f, 70f),
                "WASD / ลูกศร  •  เดิน\nE  •  สำรวจหรือคุย",
                hudStyle
            );
            if (!string.IsNullOrEmpty(InteractionPrompt))
            {
                GUI.Box(
                    new Rect((Screen.width - 420f) * 0.5f, Screen.height - 118f, 420f, 54f),
                    InteractionPrompt,
                    hudStyle
                );
            }
        }

        Rect buttonRect = new Rect(Screen.width - 178f, 22f, 156f, 44f);
        if (GUI.Button(buttonRect, "⚙  ตั้งค่า AI", launcherStyle))
        {
            isOpen = !isOpen;
            IsOpen = isOpen;
        }

        if (!isOpen)
        {
            return;
        }

        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            dimTexture,
            ScaleMode.StretchToFill
        );
        windowRect.width = Mathf.Min(620f, Screen.width - 40f);
        windowRect.height = Mathf.Min(590f, Screen.height - 40f);
        windowRect.x = (Screen.width - windowRect.width) * 0.5f;
        windowRect.y = (Screen.height - windowRect.height) * 0.5f;
        windowRect = GUI.Window(9182, windowRect, DrawWindow, string.Empty, windowStyle);
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Space(10f);
        GUILayout.Label("ตั้งค่าบทสนทนา AI", titleStyle);
        GUILayout.Space(12f);

        GUILayout.Label("ผู้ให้บริการ", labelStyle);
        providerIndex = GUILayout.SelectionGrid(
            providerIndex,
            providerLabels,
            4,
            tabStyle
        );
        if (providerIndex != previousProviderIndex)
        {
            ApplyProviderDefaults();
            previousProviderIndex = providerIndex;
        }
        GUILayout.Space(10f);

        if (providerIndex == (int)AiProviderType.OpenAiResponses)
        {
            GUILayout.Label("เลือกโมเดล OpenAI แบบรวดเร็ว", labelStyle);
            int selected = GUILayout.SelectionGrid(
                modelPresetIndex,
                openAiModels,
                3,
                tabStyle
            );
            if (selected != modelPresetIndex)
            {
                modelPresetIndex = selected;
                model = openAiModels[selected];
            }

            endpoint = AiDialogueGenerator.OpenAiResponsesUrl;
        }
        else if (providerIndex == (int)AiProviderType.KkuIntelsphere)
        {
            endpoint = AiDialogueGenerator.KkuChatCompletionsUrl;
            GUILayout.Label("KKU endpoint (ตั้งค่าอัตโนมัติ)", labelStyle);
            GUILayout.Label(endpoint, noteStyle);
            GUILayout.Label(
                "Enter a model name or numeric model ID available to your KKU account.",
                noteStyle
            );
        }
        else if (providerIndex == (int)AiProviderType.Gemini)
        {
            GUILayout.Label("เลือกโมเดล Google Gemini แบบรวดเร็ว", labelStyle);
            int selected = GUILayout.SelectionGrid(
                geminiPresetIndex,
                geminiModels,
                3,
                tabStyle
            );
            if (selected != geminiPresetIndex)
            {
                geminiPresetIndex = selected;
                model = geminiModels[selected];
            }

            endpoint = AiDialogueGenerator.GeminiChatCompletionsUrl;
            GUILayout.Label("Gemini OpenAI-compatible endpoint (ตั้งค่าอัตโนมัติ)", labelStyle);
            GUILayout.Label(endpoint, noteStyle);
            GUILayout.Label(
                "รองรับ API Key จาก Google AI Studio (รวมถึง Key รูปแบบใหม่ 'AQ...')",
                noteStyle
            );
        }
        else
        {
            GUILayout.Label("OpenAI-compatible chat endpoint", labelStyle);
            endpoint = GUILayout.TextField(endpoint, fieldStyle);
            GUILayout.Label(
                "Example format: https://provider.example/v1/chat/completions",
                noteStyle
            );
        }

        GUILayout.Space(10f);
        GUILayout.Label("Model name / ID", labelStyle);
        model = GUILayout.TextField(model, fieldStyle);

        if (SupportsModelDiscovery())
        {
            GUI.enabled = !modelFetchInProgress;
            if (GUILayout.Button(
                    modelFetchInProgress
                        ? "กำลังโหลดรายชื่อโมเดล..."
                        : "โหลดรายชื่อโมเดลที่บัญชีนี้ใช้ได้",
                    tabStyle,
                    GUILayout.Height(32f)))
            {
                FetchModels();
            }
            GUI.enabled = true;

            if (fetchedModels != null && fetchedModels.Length > 0)
            {
                int picked = GUILayout.SelectionGrid(
                    fetchedModelIndex,
                    fetchedModels,
                    2,
                    tabStyle
                );
                if (picked != fetchedModelIndex)
                {
                    fetchedModelIndex = picked;
                    model = fetchedModels[picked];
                    status = "เลือกโมเดล " + model + " แล้ว — กด APPLY เพื่อใช้งาน";
                }
            }
        }

        GUILayout.Space(10f);
        GUILayout.Label("API key — ใช้เฉพาะรอบนี้", labelStyle);
        apiKey = GUILayout.PasswordField(apiKey, '*', fieldStyle);
        GUILayout.Label(
            "The key is kept in memory and is not written into the Unity scene or PlayerPrefs.",
            noteStyle
        );

        GUILayout.Space(16f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("บันทึกและใช้งาน", actionButtonStyle, GUILayout.Height(44f)))
        {
            ApplySettings();
        }

        if (GUILayout.Button("ล้าง key", tabStyle, GUILayout.Height(44f)))
        {
            apiKey = string.Empty;
            if (AiDialogueGenerator.Instance != null)
            {
                AiDialogueGenerator.Instance.ClearSessionApiKey();
            }
            status = "Session key cleared";
        }
        GUILayout.EndHorizontal();

        GUI.enabled = !testInProgress;
        if (GUILayout.Button(
            testInProgress ? "กำลังทดสอบ..." : "ทดสอบการเชื่อมต่อ",
            tabStyle,
            GUILayout.Height(40f)))
        {
            TestConnection();
        }
        GUI.enabled = true;

        GUILayout.Space(12f);
        GUILayout.Label(status, noteStyle);
        GUILayout.Label(
            "Provider presets set the protocol and endpoint. A different service may also require a different model ID.",
            noteStyle
        );
        GUILayout.Label("กด F10 หรือปุ่มตั้งค่า AI เพื่อปิด", noteStyle);
        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 45f));
    }

    private void ApplyProviderDefaults()
    {
        // A model list only ever belongs to the provider it came from.
        fetchedModels = null;
        fetchedModelIndex = 0;

        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        switch ((AiProviderType)providerIndex)
        {
            case AiProviderType.OpenAiResponses:
                endpoint = AiDialogueGenerator.OpenAiResponsesUrl;
                if (string.IsNullOrWhiteSpace(model) ||
                    long.TryParse(model, out _))
                {
                    model = openAiModels[0];
                    modelPresetIndex = 0;
                }
                apiKey = generator != null ? generator.GetApiKey() : string.Empty;
                status = "OpenAI โหลดค่าเริ่มต้นแล้ว";
                break;
            case AiProviderType.KkuIntelsphere:
                endpoint = AiDialogueGenerator.KkuChatCompletionsUrl;
                model = "gpt-5.6-luna";
                apiKey = generator != null ? generator.GetApiKey() : string.Empty;
                status = "KKU IntelSphere (gpt-5.6-luna) โหลดค่าเริ่มต้นแล้ว — พร้อมใช้งาน";
                break;
            case AiProviderType.Gemini:
                endpoint = AiDialogueGenerator.GeminiChatCompletionsUrl;
                model = geminiModels[geminiPresetIndex];
                apiKey = generator != null ? generator.GetApiKey() : string.Empty;
                status = "Google Gemini โหลดค่าเริ่มต้นแล้ว — พร้อมใช้งาน";
                break;
            default:
                endpoint = "https://provider.example/v1/chat/completions";
                model = string.Empty;
                apiKey = generator != null ? generator.GetApiKey() : string.Empty;
                status = "Custom endpoint preset";
                break;
        }
    }

    private void ApplySettings()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null)
        {
            status = "AI generator was not found";
            return;
        }

        generator.Configure(
            (AiProviderType)providerIndex,
            endpoint,
            model,
            apiKey
        );

        status = generator.CanGenerate
            ? "บันทึกแล้ว — กดทดสอบการเชื่อมต่อเพื่อยืนยัน key และ model"
            : "Missing key, endpoint, or model; fallback remains active";
    }

    private bool SupportsModelDiscovery()
    {
        AiProviderType selected = (AiProviderType)providerIndex;
        return selected == AiProviderType.KkuIntelsphere ||
               selected == AiProviderType.OpenAiCompatible ||
               selected == AiProviderType.Gemini;
    }

    private void FetchModels()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null)
        {
            status = "ยังไม่พร้อมใช้งาน";
            return;
        }

        // The key has to be live on the generator before we can ask with it.
        ApplySettings();

        modelFetchInProgress = true;
        status = "กำลังขอรายชื่อโมเดลจากผู้ให้บริการ...";
        StartCoroutine(
            generator.FetchAvailableModels((models, error) =>
            {
                modelFetchInProgress = false;
                if (models == null)
                {
                    fetchedModels = null;
                    status = "โหลดรายชื่อโมเดลไม่สำเร็จ: " + error;
                    return;
                }

                fetchedModels = models;
                fetchedModelIndex = System.Array.IndexOf(models, model);
                if (fetchedModelIndex < 0)
                {
                    fetchedModelIndex = 0;
                }

                status = "พบโมเดลที่ใช้ได้ " + models.Length +
                         " รายการ — เลือกจากรายการด้านบนแล้วกด APPLY";
            })
        );
    }

    private void TestConnection()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null || !generator.CanGenerate)
        {
            status = "ยังกรอก key, model หรือ endpoint ไม่ครบ";
            return;
        }

        testInProgress = true;
        status = "กำลังติดต่อผู้ให้บริการ AI...";
        StartCoroutine(
            generator.GenerateReply(
                "connection_test",
                "ระบบทดสอบ",
                "ตอบเพื่อยืนยันการเชื่อมต่อเท่านั้น",
                "ตอบคำว่า พร้อมใช้งาน",
                reply =>
                {
                    testInProgress = false;
                    status = reply != null
                        ? "เชื่อมต่อสำเร็จ — AI พร้อมตอบบทสนทนา"
                        : "เชื่อมต่อไม่สำเร็จ: " + generator.LastError;
                }
            )
        );
    }

    private void EnsureStyles()
    {
        if (windowStyle != null)
        {
            return;
        }

        panelTexture = new Texture2D(1, 1);
        panelTexture.SetPixel(0, 0, new Color(0.025f, 0.045f, 0.075f, 0.99f));
        panelTexture.Apply();

        dimTexture = CreateColorTexture(new Color(0.005f, 0.01f, 0.02f, 0.72f));
        fieldTexture = CreateColorTexture(new Color(0.055f, 0.09f, 0.13f, 1f));
        buttonTexture = CreateColorTexture(new Color(0.08f, 0.2f, 0.28f, 1f));
        accentTexture = CreateColorTexture(new Color(0.78f, 0.55f, 0.2f, 1f));

        windowStyle = new GUIStyle(GUI.skin.window);
        windowStyle.normal.background = panelTexture;
        windowStyle.padding = new RectOffset(24, 24, 20, 22);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 22;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        titleStyle.normal.textColor = new Color(1f, 0.82f, 0.35f);

        noteStyle = new GUIStyle(GUI.skin.label);
        noteStyle.wordWrap = true;
        noteStyle.normal.textColor = new Color(0.88f, 0.92f, 0.98f);
        noteStyle.fontSize = 14;

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 15;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = new Color(0.9f, 0.94f, 0.98f);

        fieldStyle = new GUIStyle(GUI.skin.textField);
        fieldStyle.normal.background = fieldTexture;
        fieldStyle.focused.background = fieldTexture;
        fieldStyle.normal.textColor = new Color(0.92f, 0.95f, 1f);
        fieldStyle.focused.textColor = Color.white;
        fieldStyle.padding = new RectOffset(12, 12, 9, 9);
        fieldStyle.fixedHeight = 38f;

        tabStyle = new GUIStyle(GUI.skin.button);
        tabStyle.normal.background = buttonTexture;
        tabStyle.hover.background = accentTexture;
        tabStyle.active.background = accentTexture;
        tabStyle.onNormal.background = accentTexture;
        tabStyle.onHover.background = accentTexture;
        tabStyle.onActive.background = accentTexture;
        tabStyle.normal.textColor = new Color(0.9f, 0.94f, 0.98f);
        tabStyle.onNormal.textColor = new Color(0.04f, 0.06f, 0.08f);
        tabStyle.padding = new RectOffset(8, 8, 8, 8);

        actionButtonStyle = new GUIStyle(tabStyle);
        actionButtonStyle.normal.background = accentTexture;
        actionButtonStyle.normal.textColor = new Color(0.04f, 0.06f, 0.08f);
        actionButtonStyle.fontStyle = FontStyle.Bold;

        launcherStyle = new GUIStyle(actionButtonStyle);
        launcherStyle.fontSize = 15;

        hudStyle = new GUIStyle(GUI.skin.box);
        hudStyle.normal.background = fieldTexture;
        hudStyle.normal.textColor = new Color(0.9f, 0.94f, 0.98f);
        hudStyle.fontSize = 16;
        hudStyle.alignment = TextAnchor.MiddleCenter;
        hudStyle.padding = new RectOffset(14, 14, 8, 8);
    }

    public static void SetInteractionPrompt(string prompt)
    {
        InteractionPrompt = prompt ?? string.Empty;
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
        IsOpen = false;
        InteractionPrompt = string.Empty;
        if (panelTexture != null)
        {
            Destroy(panelTexture);
        }
        if (dimTexture != null) Destroy(dimTexture);
        if (fieldTexture != null) Destroy(fieldTexture);
        if (buttonTexture != null) Destroy(buttonTexture);
        if (accentTexture != null) Destroy(accentTexture);
    }
}
