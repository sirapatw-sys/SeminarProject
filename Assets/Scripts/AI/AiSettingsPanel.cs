using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The AI settings panel (F10 or the "⚙ ตั้งค่า AI" button), plus the small
/// controls HUD and the interaction prompt.
///
/// The panel is a modal card drawn a size up from the HUD, since it is a
/// form people read and type into. Models are picked from a list instead of
/// typed: for KKU IntelSphere the list ships with the game and can be
/// refreshed from the service (GET /models) for the models this account
/// can use. Typing a model name is still there for anything not listed.
/// </summary>
public class AiSettingsPanel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    public static string InteractionPrompt { get; private set; }

    /// <summary>
    /// Open, or closed by a key press this frame. Other scripts that also
    /// react to Esc check this, so the press that closed the panel does not
    /// also close a dialogue or quit from the title menu.
    /// </summary>
    public static bool HoldsKeyboard
    {
        get { return IsOpen || closedOnFrame == Time.frameCount; }
    }

    private static int closedOnFrame = -1;

    private const float PanelMagnify = 1.25f;
    private const float PanelWidth = 760f;
    private const float PanelHeight = 720f;
    private const float Padding = 28f;

    private static readonly string[] ProviderLabels =
        { "OpenAI", "KKU IntelSphere", "Google Gemini", "กำหนดเอง" };

    /// <summary>
    /// What KKU IntelSphere listed when this was written. "โหลดรายชื่อล่าสุด"
    /// in the model list replaces it with what the service offers today.
    /// </summary>
    private static readonly string[] KnownKkuModels =
    {
        "gpt-5.6-terra", "gpt-5.6-luna",
        "claude-sonnet-5",
        "gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.5-flash-lite",
        "qwen3.7-max", "qwen3.7-plus", "qwen3.6-flash",
        "deepseek-v4-pro", "deepseek-v4-flash",
        "llama-4-maverick", "llama-4-scout",
        "mistral-large-2512", "mistral-medium-3", "mistral-small-2603",
        "kimi-k3", "minimax-m3", "grok-4.5",
        "nova-pro-v1", "nova-2-lite-v1",
    };

    /// <summary>
    /// How each KKU model did with the game's own reply prompt (3 test
    /// messages each, 2026-09-26): slow ones pass the 20 s wait and fall back
    /// to written lines, and some answer in prose instead of the JSON the
    /// game reads. Models not listed here were not tested.
    /// </summary>
    private static readonly Dictionary<string, string> KkuModelWarnings = new Dictionary<string, string>
    {
        { "qwen3.7-max", "ช้า (~23 วิ) เกินเวลารอบ่อย" },
        { "qwen3.7-plus", "ช้า (~26 วิ) เกินเวลารอบ่อย" },
        { "qwen3.6-flash", "ค่อนข้างช้า (~16 วิ)" },
        { "kimi-k3", "ช้า (~28 วิ) เกินเวลารอบ่อย" },
        { "deepseek-v4-pro", "ตอบผิดรูปแบบบ่อย" },
        { "deepseek-v4-flash", "ตอบผิดรูปแบบบ่อย" },
        { "llama-4-maverick", "ตอบผิดรูปแบบบ่อย" },
        { "llama-4-scout", "ตอบผิดรูปแบบบ่อย" },
        { "mistral-large-2512", "ไม่ตอบกลับ" },
        { "mistral-small-2603", "ตอบผิดรูปแบบบ่อย" },
        { "minimax-m3", "ตอบผิดรูปแบบบางครั้ง" },
    };

    private static readonly HashSet<string> KkuModelsThatWorkWell = new HashSet<string>
    {
        "gpt-5.6-terra", "gpt-5.6-luna", "claude-sonnet-5", "gemini-3.8-flash", "gemini-3.7-flash",
        "gemini-3.5-flash-lite", "mistral-medium-3", "grok-4.5", "nova-pro-v1", "nova-2-lite-v1",
    };

    private static readonly string[] OpenAiModels =
        { "gpt-4o-mini", "gpt-4.1-mini", "gpt-4.1" };

    private static readonly string[] GeminiModels =
        { "gemini-3.6-flash", "gemini-3.6-pro", "gemini-3.5-flash" };

    private enum Tone
    {
        Neutral,
        Good,
        Warn,
        Bad
    }

    private bool isOpen;
    private int providerIndex;
    private string model = AiDialogueGenerator.DefaultKkuModel;
    private string endpoint = string.Empty;
    private string apiKey = string.Empty;
    private string status = "ยังไม่ได้บันทึกการตั้งค่า";
    private Tone statusTone = Tone.Neutral;
    private bool testInProgress;
    private bool modelFetchInProgress;
    private string[] fetchedModels;
    private bool pickerOpen;
    private bool typeModelByHand;
    private bool showKey;
    private Vector2 formScroll;
    private Vector2 pickerScroll;

    private GUISkin skin;
    private GUIStyle cardStyle;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle noteStyle;
    private GUIStyle statusStyle;
    private GUIStyle pillStyle;
    private GUIStyle fieldStyle;
    private GUIStyle segmentStyle;
    private GUIStyle dropdownStyle;
    private GUIStyle rowStyle;
    private GUIStyle groupStyle;
    private GUIStyle tagStyle;
    private GUIStyle warnTagStyle;
    private GUIStyle primaryStyle;
    private GUIStyle secondaryStyle;
    private GUIStyle ghostStyle;
    private GUIStyle closeStyle;
    private GUIStyle launcherStyle;
    private GUIStyle hudStyle;
    private Texture2D dimTexture;
    private Texture2D dividerTexture;
    private readonly List<Texture2D> textures = new List<Texture2D>();

    private static readonly Color Text = new Color(0.93f, 0.95f, 0.98f);
    private static readonly Color Muted = new Color(0.62f, 0.68f, 0.76f);
    private static readonly Color Accent = new Color(0.95f, 0.72f, 0.3f);
    private static readonly Color AccentText = new Color(0.08f, 0.07f, 0.05f);
    private static readonly Color GoodColor = new Color(0.45f, 0.86f, 0.58f);
    private static readonly Color WarnColor = new Color(0.98f, 0.76f, 0.35f);
    private static readonly Color BadColor = new Color(1f, 0.5f, 0.47f);

    private static AiSettingsPanel active;

    /// <summary>Opens the panel from elsewhere, e.g. the title menu.</summary>
    public static void Open()
    {
        if (active != null)
        {
            active.SetOpen(true);
        }
    }

    private void OnEnable()
    {
        active = this;
    }

    private void OnDisable()
    {
        if (active == this)
        {
            active = null;
        }
    }

    private void Start()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null)
        {
            return;
        }

        providerIndex = (int)generator.Provider;
        model = generator.Model;
        endpoint = generator.ApiUrl;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = generator.GetApiKey();
        }

        if (generator.CanGenerate)
        {
            SetStatus("AI พร้อมใช้งาน", Tone.Good);
        }
        else
        {
            SetStatus("ยังไม่มี API key — ตัวละครจะใช้บทพูดที่เขียนไว้แทน", Tone.Warn);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            SetOpen(!isOpen);
        }
        else if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            // Esc backs out of the model list first, then closes the panel.
            if (pickerOpen)
            {
                pickerOpen = false;
            }
            else
            {
                SetOpen(false);
            }
        }
    }

    private void SetOpen(bool open)
    {
        if (isOpen && !open)
        {
            closedOnFrame = Time.frameCount;
        }

        isOpen = open;
        IsOpen = open;
        pickerOpen = false;
    }

    private void SetStatus(string message, Tone tone)
    {
        status = message;
        statusTone = tone;
    }

    // ------------------------------------------------------------------ HUD

    private void OnGUI()
    {
        // In front of the game's overlays while open, so it gets the mouse first.
        GUI.depth = isOpen ? ModalGui.FrontDepth + 5 : 0;
        UiScale.Apply();
        EnsureStyles();

        if (!isOpen && !DialogueManager.IsDialogueOpen && !TitleMenu.IsOpen)
        {
            GUI.Box(
                new Rect(22f, 22f, 285f, 94f),
                "WASD / ลูกศร  •  เดิน\nE  •  สำรวจหรือคุย\nJ  •  สมุดบันทึก",
                hudStyle
            );
            if (!string.IsNullOrEmpty(InteractionPrompt))
            {
                GUI.Box(
                    new Rect((UiScale.Width - 420f) * 0.5f, UiScale.Height - 118f, 420f, 54f),
                    InteractionPrompt,
                    hudStyle
                );
            }
        }

        if (!isOpen)
        {
            Rect buttonRect = new Rect(UiScale.Width - 178f, 22f, 156f, 44f);
            if (ModalGui.Button(buttonRect, "⚙  ตั้งค่า AI", launcherStyle))
            {
                SetOpen(true);
            }

            return;
        }

        DrawPanel();
    }

    // ---------------------------------------------------------------- panel

    private void DrawPanel()
    {
        UiScale.Apply(PanelMagnify, PanelHeight);
        GUISkin previousSkin = GUI.skin;
        GUI.skin = skin;

        GUI.DrawTexture(new Rect(0f, 0f, UiScale.Width, UiScale.Height), dimTexture);

        float width = Mathf.Min(PanelWidth, UiScale.Width - 32f);
        float height = Mathf.Min(PanelHeight, UiScale.Height - 32f);
        Rect card = new Rect((UiScale.Width - width) * 0.5f, (UiScale.Height - height) * 0.5f, width, height);
        GUI.Box(card, GUIContent.none, cardStyle);

        Rect inner = new Rect(card.x + Padding, card.y + Padding - 4f, card.width - Padding * 2f, card.height - Padding * 2f + 8f);
        DrawHeader(new Rect(inner.x, inner.y, inner.width, 64f));

        Rect body = new Rect(inner.x, inner.y + 80f, inner.width, inner.height - 80f);
        GUILayout.BeginArea(body);
        if (pickerOpen)
        {
            DrawModelPicker();
        }
        else
        {
            DrawForm();
        }
        GUILayout.EndArea();

        GUI.skin = previousSkin;
        ModalGui.BlockMouse();
    }

    private void DrawHeader(Rect area)
    {
        GUI.Label(new Rect(area.x, area.y, area.width - 260f, 36f), "ตั้งค่า AI", titleStyle);
        GUI.Label(
            new Rect(area.x, area.y + 36f, area.width - 60f, 24f),
            "AI สร้างบทพูดของตัวละคร ถ้าใช้ไม่ได้ เกมจะใช้บทพูดที่เขียนไว้แทน",
            subtitleStyle);

        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        bool ready = generator != null && generator.CanGenerate;
        string pill = ready ? "●  AI พร้อมใช้งาน" : "●  ใช้บทพูดสำรอง";
        pillStyle.normal.textColor = ready ? GoodColor : WarnColor;
        float pillWidth = pillStyle.CalcSize(new GUIContent(pill)).x + 8f;
        GUI.Box(new Rect(area.xMax - 52f - pillWidth, area.y + 4f, pillWidth, 32f), pill, pillStyle);

        if (ModalGui.Button(new Rect(area.xMax - 38f, area.y + 2f, 38f, 36f), "✕", closeStyle))
        {
            SetOpen(false);
        }
    }

    private void DrawForm()
    {
        formScroll = GUILayout.BeginScrollView(formScroll, false, false, GUIStyle.none, skin.verticalScrollbar, GUIStyle.none);

        Section("ผู้ให้บริการ");
        GUILayout.BeginHorizontal();
        for (int index = 0; index < ProviderLabels.Length; index++)
        {
            if (ModalGui.LayoutButton(ProviderLabels[index], segmentStyle, index == providerIndex, GUILayout.Height(44f)) &&
                index != providerIndex)
            {
                providerIndex = index;
                ApplyProviderDefaults();
            }
        }
        GUILayout.EndHorizontal();

        AiProviderType provider = (AiProviderType)providerIndex;
        if (provider == AiProviderType.OpenAiCompatible)
        {
            Section("Endpoint (แบบ OpenAI chat completions)");
            endpoint = GUILayout.TextField(endpoint, fieldStyle);
            Note("เช่น https://provider.example/v1/chat/completions");
        }

        Section("โมเดล");
        if (provider == AiProviderType.OpenAiCompatible || typeModelByHand)
        {
            model = GUILayout.TextField(model, fieldStyle);
        }
        else
        {
            if (ModalGui.LayoutButton(model, dropdownStyle, false, GUILayout.Height(48f)))
            {
                pickerOpen = true;
                pickerScroll = Vector2.zero;
            }

            // Maker on the right, then the arrow, like a native dropdown.
            Rect last = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
            {
                tagStyle.Draw(new Rect(last.x, last.y, last.width - 48f, last.height),
                              new GUIContent(VendorOf(model)), false, false, false, false);
                tagStyle.Draw(new Rect(last.xMax - 36f, last.y, 20f, last.height),
                              new GUIContent("▾"), false, false, false, false);
            }
        }

        bool modelWarning;
        string modelTag = ModelTag(provider, model, out modelWarning);
        if (modelWarning)
        {
            statusStyle.normal.textColor = WarnColor;
            GUILayout.Label("โมเดลนี้" + modelTag + " — ตัวละครจะใช้บทพูดสำรองแทนบ่อยขึ้น", statusStyle);
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(ModelNote(provider), noteStyle, GUILayout.ExpandWidth(true));
        if (provider != AiProviderType.OpenAiCompatible &&
            ModalGui.LayoutButton(typeModelByHand ? "เลือกจากรายการ" : "พิมพ์ชื่อโมเดลเอง", ghostStyle, false,
                                  GUILayout.Height(30f), GUILayout.Width(170f)))
        {
            typeModelByHand = !typeModelByHand;
        }
        GUILayout.EndHorizontal();

        Section("API key");
        GUILayout.BeginHorizontal();
        apiKey = showKey
            ? GUILayout.TextField(apiKey, fieldStyle)
            : GUILayout.PasswordField(apiKey, '•', fieldStyle);
        if (ModalGui.LayoutButton(showKey ? "ซ่อน" : "แสดง", secondaryStyle, false, GUILayout.Width(84f), GUILayout.Height(44f)))
        {
            showKey = !showKey;
        }
        GUILayout.EndHorizontal();
        Note("key ที่พิมพ์ตรงนี้ใช้เฉพาะรอบที่เล่นอยู่ ไม่ถูกบันทึกลงฉากหรือ PlayerPrefs (key ในโฟลเดอร์ UserSettings โหลดให้เองตอนเริ่มเกม)");

        GUILayout.Space(14f);
        Rect divider = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(divider, dividerTexture);
        GUILayout.Space(10f);

        statusStyle.normal.textColor = ToneColor(statusTone);
        GUILayout.Label(status, statusStyle);
        Note(string.IsNullOrEmpty(AiDialogueGenerator.LastQuota)
            ? "โควตาคงเหลือ: จะแสดงหลังคำขอแรก (ถ้าผู้ให้บริการส่งมา)"
            : "โควตาคงเหลือ: " + AiDialogueGenerator.LastQuota);
        if (AiDialogueGenerator.Instance != null && AiDialogueGenerator.Instance.UsingBackupKey)
        {
            statusStyle.normal.textColor = WarnColor;
            GUILayout.Label("กำลังใช้ API key สำรอง เพราะโควตาของ key หลักหมดแล้ว", statusStyle);
        }

        GUILayout.Space(12f);
        GUILayout.BeginHorizontal();
        GUI.enabled = !testInProgress;
        if (ModalGui.LayoutButton(testInProgress ? "กำลังทดสอบ..." : "ทดสอบการเชื่อมต่อ", secondaryStyle, false, GUILayout.Height(48f), GUILayout.Width(200f)))
        {
            TestConnection();
        }
        GUI.enabled = true;

        if (ModalGui.LayoutButton("ล้าง key", ghostStyle, false, GUILayout.Height(48f), GUILayout.Width(110f)))
        {
            apiKey = string.Empty;
            if (AiDialogueGenerator.Instance != null)
            {
                AiDialogueGenerator.Instance.ClearSessionApiKey();
            }
            SetStatus("ล้าง key ของรอบนี้แล้ว", Tone.Neutral);
        }

        GUILayout.FlexibleSpace();
        if (ModalGui.LayoutButton("บันทึกและใช้งาน", primaryStyle, false, GUILayout.Height(48f), GUILayout.Width(200f)))
        {
            ApplySettings();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);
        Note("F10 หรือ Esc เพื่อปิด");
        GUILayout.EndScrollView();
    }

    private void DrawModelPicker()
    {
        AiProviderType provider = (AiProviderType)providerIndex;

        GUILayout.BeginHorizontal();
        if (ModalGui.LayoutButton("‹  กลับ", ghostStyle, false, GUILayout.Height(40f), GUILayout.Width(100f)))
        {
            pickerOpen = false;
        }
        GUILayout.Label("เลือกโมเดล — " + ProviderLabels[providerIndex], sectionStyle, GUILayout.Height(40f));
        GUILayout.FlexibleSpace();
        if (SupportsModelDiscovery(provider))
        {
            GUI.enabled = !modelFetchInProgress;
            if (ModalGui.LayoutButton(modelFetchInProgress ? "กำลังโหลด..." : "โหลดรายชื่อล่าสุด", secondaryStyle, false, GUILayout.Height(40f), GUILayout.Width(190f)))
            {
                FetchModels();
            }
            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();

        statusStyle.normal.textColor = ToneColor(statusTone);
        GUILayout.Label(fetchedModels != null
            ? "รายชื่อล่าสุดจากผู้ให้บริการ " + fetchedModels.Length + " โมเดล"
            : "รายชื่อที่ติดมากับเกม — กด \"โหลดรายชื่อล่าสุด\" เพื่อดูโมเดลที่ key นี้ใช้ได้จริง", noteStyle);
        if (modelFetchInProgress || statusTone == Tone.Bad)
        {
            GUILayout.Label(status, statusStyle);
        }
        GUILayout.Space(6f);

        pickerScroll = GUILayout.BeginScrollView(pickerScroll, false, false, GUIStyle.none, skin.verticalScrollbar, GUIStyle.none);
        foreach (KeyValuePair<string, List<string>> group in GroupByVendor(ModelChoices(provider)))
        {
            GUILayout.Label(group.Key, groupStyle);
            foreach (string id in group.Value)
            {
                bool current = id == model;
                if (ModalGui.LayoutButton(id, rowStyle, current, GUILayout.Height(42f)))
                {
                    model = id;
                    pickerOpen = false;
                    SetStatus("เลือก " + id + " แล้ว — กด \"บันทึกและใช้งาน\" เพื่อเริ่มใช้", Tone.Neutral);
                }

                Rect row = GUILayoutUtility.GetLastRect();
                bool warning;
                string tag = ModelTag(provider, id, out warning);
                if (current)
                {
                    tag = tag.Length > 0 ? "✓  " + tag : "✓ กำลังเลือก";
                }

                if (tag.Length > 0 && Event.current.type == EventType.Repaint)
                {
                    (warning ? warnTagStyle : tagStyle).Draw(
                        new Rect(row.x, row.y, row.width - 16f, row.height), new GUIContent(tag),
                        false, false, current && !warning, false);
                }
            }
            GUILayout.Space(6f);
        }
        GUILayout.EndScrollView();
    }

    /// <summary>The note beside a model in the list: default, tested fine, or a warning.</summary>
    private static string ModelTag(AiProviderType provider, string id, out bool warning)
    {
        warning = false;
        if (provider != AiProviderType.KkuIntelsphere)
        {
            return string.Empty;
        }

        string note;
        if (KkuModelWarnings.TryGetValue(id, out note))
        {
            warning = true;
            return note;
        }

        if (id == AiDialogueGenerator.DefaultKkuModel)
        {
            return "ค่าเริ่มต้น • แนะนำ";
        }

        return KkuModelsThatWorkWell.Contains(id) ? "แนะนำ" : string.Empty;
    }

    private void Section(string text)
    {
        GUILayout.Space(14f);
        GUILayout.Label(text, sectionStyle);
        GUILayout.Space(2f);
    }

    private void Note(string text)
    {
        GUILayout.Label(text, noteStyle);
    }

    private string ModelNote(AiProviderType provider)
    {
        switch (provider)
        {
            case AiProviderType.KkuIntelsphere:
                return "ค่าเริ่มต้น " + AiDialogueGenerator.DefaultKkuModel + "  •  โควตา KKU นับแยกต่อโมเดล";
            case AiProviderType.Gemini:
                return "ใช้ key จาก Google AI Studio ได้ (รวมถึง key แบบใหม่ที่ขึ้นต้นด้วย AQ)";
            case AiProviderType.OpenAiResponses:
                return "OpenAI Responses API";
            default:
                return "ใส่ชื่อหรือ ID ของโมเดลตามที่ผู้ให้บริการกำหนด";
        }
    }

    private Color ToneColor(Tone tone)
    {
        switch (tone)
        {
            case Tone.Good: return GoodColor;
            case Tone.Warn: return WarnColor;
            case Tone.Bad: return BadColor;
            default: return Muted;
        }
    }

    // --------------------------------------------------------------- models

    private string[] ModelChoices(AiProviderType provider)
    {
        string[] list;
        if (fetchedModels != null && fetchedModels.Length > 0)
        {
            list = fetchedModels;
        }
        else if (provider == AiProviderType.KkuIntelsphere)
        {
            list = KnownKkuModels;
        }
        else if (provider == AiProviderType.Gemini)
        {
            list = GeminiModels;
        }
        else if (provider == AiProviderType.OpenAiResponses)
        {
            list = OpenAiModels;
        }
        else
        {
            list = new string[0];
        }

        // A model typed by hand stays pickable.
        if (!string.IsNullOrWhiteSpace(model) && System.Array.IndexOf(list, model) < 0)
        {
            List<string> withCurrent = new List<string>(list) { model };
            return withCurrent.ToArray();
        }

        return list;
    }

    /// <summary>Groups ids by maker; the default model's maker comes first.</summary>
    private static List<KeyValuePair<string, List<string>>> GroupByVendor(IEnumerable<string> ids)
    {
        Dictionary<string, List<string>> groups = new Dictionary<string, List<string>>();
        foreach (string id in ids)
        {
            string vendor = VendorOf(id);
            if (!groups.ContainsKey(vendor))
            {
                groups[vendor] = new List<string>();
            }
            groups[vendor].Add(id);
        }

        string first = VendorOf(AiDialogueGenerator.DefaultKkuModel);
        List<KeyValuePair<string, List<string>>> ordered = new List<KeyValuePair<string, List<string>>>(groups);
        ordered.Sort((a, b) =>
        {
            if (a.Key == b.Key) return 0;
            if (a.Key == first) return -1;
            if (b.Key == first) return 1;
            if (a.Key == "อื่นๆ") return 1;
            if (b.Key == "อื่นๆ") return -1;
            return string.Compare(a.Key, b.Key, System.StringComparison.OrdinalIgnoreCase);
        });
        return ordered;
    }

    public static string VendorOf(string modelId)
    {
        string id = (modelId ?? string.Empty).Trim().ToLowerInvariant();
        if (id.StartsWith("gpt") || id.StartsWith("o3") || id.StartsWith("o4")) return "OpenAI";
        if (id.StartsWith("claude")) return "Anthropic";
        if (id.StartsWith("gemini") || id.StartsWith("gemma")) return "Google";
        if (id.StartsWith("qwen")) return "Qwen";
        if (id.StartsWith("deepseek")) return "DeepSeek";
        if (id.StartsWith("llama")) return "Meta";
        if (id.StartsWith("mistral")) return "Mistral";
        if (id.StartsWith("kimi")) return "Moonshot";
        if (id.StartsWith("minimax")) return "MiniMax";
        if (id.StartsWith("grok")) return "xAI";
        if (id.StartsWith("nova")) return "Amazon";
        if (id.StartsWith("typhoon")) return "SCB 10X";
        return "อื่นๆ";
    }

    private void ApplyProviderDefaults()
    {
        // A model list only ever belongs to the provider it came from.
        fetchedModels = null;
        typeModelByHand = false;

        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        apiKey = generator != null ? generator.GetApiKey() : string.Empty;
        switch ((AiProviderType)providerIndex)
        {
            case AiProviderType.OpenAiResponses:
                endpoint = AiDialogueGenerator.OpenAiResponsesUrl;
                if (System.Array.IndexOf(OpenAiModels, model) < 0)
                {
                    model = OpenAiModels[0];
                }
                SetStatus("โหลดค่าเริ่มต้นของ OpenAI แล้ว", Tone.Neutral);
                break;
            case AiProviderType.KkuIntelsphere:
                endpoint = AiDialogueGenerator.KkuChatCompletionsUrl;
                model = AiDialogueGenerator.DefaultKkuModel;
                SetStatus("โหลดค่าเริ่มต้นของ KKU IntelSphere (" + model + ") แล้ว", Tone.Neutral);
                break;
            case AiProviderType.Gemini:
                endpoint = AiDialogueGenerator.GeminiChatCompletionsUrl;
                if (System.Array.IndexOf(GeminiModels, model) < 0)
                {
                    model = GeminiModels[0];
                }
                SetStatus("โหลดค่าเริ่มต้นของ Google Gemini แล้ว", Tone.Neutral);
                break;
            default:
                endpoint = "https://provider.example/v1/chat/completions";
                model = string.Empty;
                SetStatus("ใส่ endpoint และชื่อโมเดลของผู้ให้บริการ", Tone.Neutral);
                break;
        }
    }

    private void ApplySettings()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null)
        {
            SetStatus("ไม่พบตัวสร้างบทสนทนา AI ในฉากนี้", Tone.Bad);
            return;
        }

        generator.Configure(
            (AiProviderType)providerIndex,
            endpoint,
            model,
            apiKey
        );

        if (generator.CanGenerate)
        {
            SetStatus("บันทึกแล้ว — กด \"ทดสอบการเชื่อมต่อ\" เพื่อยืนยัน key และโมเดล", Tone.Good);
        }
        else
        {
            SetStatus("ยังขาด key, endpoint หรือชื่อโมเดล — ตอนนี้ใช้บทพูดสำรองอยู่", Tone.Warn);
        }
    }

    private static bool SupportsModelDiscovery(AiProviderType selected)
    {
        return selected == AiProviderType.KkuIntelsphere ||
               selected == AiProviderType.OpenAiCompatible ||
               selected == AiProviderType.Gemini;
    }

    private void FetchModels()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null)
        {
            SetStatus("ยังไม่พร้อมใช้งาน", Tone.Bad);
            return;
        }

        // The key has to be live on the generator before we can ask with it.
        ApplySettings();

        modelFetchInProgress = true;
        SetStatus("กำลังขอรายชื่อโมเดลจากผู้ให้บริการ...", Tone.Neutral);
        StartCoroutine(
            generator.FetchAvailableModels((models, error) =>
            {
                modelFetchInProgress = false;
                if (models == null)
                {
                    SetStatus("โหลดรายชื่อโมเดลไม่สำเร็จ: " + error, Tone.Bad);
                    return;
                }

                fetchedModels = models;
                SetStatus("พบโมเดลที่ใช้ได้ " + models.Length + " รายการ", Tone.Good);
            })
        );
    }

    private void TestConnection()
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null || !generator.CanGenerate)
        {
            SetStatus("ยังกรอก key, โมเดล หรือ endpoint ไม่ครบ (กดบันทึกก่อน)", Tone.Warn);
            return;
        }

        testInProgress = true;
        SetStatus("กำลังติดต่อผู้ให้บริการ AI...", Tone.Neutral);
        StartCoroutine(
            generator.GenerateReply(
                "connection_test",
                "ระบบทดสอบ",
                "ตอบเพื่อยืนยันการเชื่อมต่อเท่านั้น",
                "ตอบคำว่า พร้อมใช้งาน",
                reply =>
                {
                    testInProgress = false;
                    if (reply != null)
                    {
                        SetStatus("เชื่อมต่อสำเร็จ — " + generator.Model + " พร้อมตอบบทสนทนา", Tone.Good);
                    }
                    else
                    {
                        SetStatus("เชื่อมต่อไม่สำเร็จ: " + generator.LastError, Tone.Bad);
                    }
                }
            )
        );
    }

    // --------------------------------------------------------------- styles

    private Texture2D Keep(Texture2D texture)
    {
        textures.Add(texture);
        return texture;
    }

    private GUIStyle ButtonStyle(Color fill, Color hover, Color text, int radius, int fontSize, bool bold)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontSize = fontSize,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(radius, radius, radius, radius),
            padding = new RectOffset(14, 14, 8, 8),
            margin = new RectOffset(4, 4, 4, 4),
        };
        Texture2D normal = Keep(ModalGui.Rounded(fill, radius));
        Texture2D lit = Keep(ModalGui.Rounded(hover, radius));
        style.normal.background = normal;
        style.hover.background = lit;
        style.active.background = lit;
        style.focused.background = normal;
        style.onNormal.background = lit;
        style.onHover.background = lit;
        style.onActive.background = lit;
        style.normal.textColor = text;
        style.hover.textColor = text;
        style.active.textColor = text;
        style.focused.textColor = text;
        style.onNormal.textColor = text;
        style.onHover.textColor = text;
        style.onActive.textColor = text;
        return style;
    }

    private void EnsureStyles()
    {
        if (cardStyle != null)
        {
            return;
        }

        Color surface = new Color(0.07f, 0.085f, 0.115f, 0.98f);
        Color raised = new Color(0.12f, 0.145f, 0.19f, 1f);
        Color raisedHover = new Color(0.17f, 0.2f, 0.26f, 1f);
        Color line = new Color(1f, 1f, 1f, 0.09f);

        dimTexture = Keep(ModalGui.Solid(new Color(0.01f, 0.012f, 0.02f, 0.74f)));
        dividerTexture = Keep(ModalGui.Solid(line));

        // A private copy of the skin, so the slim scrollbars here do not
        // change the look of the other overlays.
        skin = Instantiate(GUI.skin);
        skin.hideFlags = HideFlags.HideAndDontSave;
        skin.verticalScrollbar.normal.background = Keep(ModalGui.Rounded(new Color(1f, 1f, 1f, 0.04f), 4));
        skin.verticalScrollbar.border = new RectOffset(4, 4, 4, 4);
        skin.verticalScrollbar.fixedWidth = 8f;
        skin.verticalScrollbar.margin = new RectOffset(6, 0, 0, 0);
        skin.verticalScrollbarThumb.normal.background = Keep(ModalGui.Rounded(new Color(1f, 1f, 1f, 0.22f), 4));
        skin.verticalScrollbarThumb.border = new RectOffset(4, 4, 4, 4);
        skin.verticalScrollbarThumb.fixedWidth = 8f;

        cardStyle = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(18, 18, 18, 18),
        };
        cardStyle.normal.background = Keep(ModalGui.Rounded(surface, 18, new Color(1f, 1f, 1f, 0.1f), 1.2f));

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = Text;

        subtitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
        subtitleStyle.normal.textColor = Muted;

        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };
        sectionStyle.normal.textColor = Text;

        noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        noteStyle.normal.textColor = Muted;

        statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true };

        pillStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(14, 14, 14, 14),
            padding = new RectOffset(14, 14, 4, 4),
        };
        pillStyle.normal.background = Keep(ModalGui.Rounded(new Color(1f, 1f, 1f, 0.06f), 14));

        fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleLeft,
            border = new RectOffset(10, 10, 10, 10),
            padding = new RectOffset(14, 14, 10, 10),
            margin = new RectOffset(4, 4, 4, 4),
            fixedHeight = 44f,
        };
        Texture2D field = Keep(ModalGui.Rounded(new Color(0.035f, 0.045f, 0.065f, 1f), 10, line, 1.2f));
        Texture2D fieldFocus = Keep(ModalGui.Rounded(new Color(0.035f, 0.045f, 0.065f, 1f), 10, Accent, 1.6f));
        fieldStyle.normal.background = field;
        fieldStyle.hover.background = field;
        fieldStyle.focused.background = fieldFocus;
        fieldStyle.normal.textColor = Text;
        fieldStyle.hover.textColor = Text;
        fieldStyle.focused.textColor = Color.white;

        segmentStyle = ButtonStyle(raised, raisedHover, Text, 10, 16, false);
        segmentStyle.onNormal.background = Keep(ModalGui.Rounded(Accent, 10));
        segmentStyle.onHover.background = segmentStyle.onNormal.background;
        segmentStyle.onActive.background = segmentStyle.onNormal.background;
        segmentStyle.onNormal.textColor = AccentText;
        segmentStyle.onHover.textColor = AccentText;
        segmentStyle.onActive.textColor = AccentText;
        segmentStyle.fontStyle = FontStyle.Bold;

        dropdownStyle = ButtonStyle(new Color(0.035f, 0.045f, 0.065f, 1f), raised, Text, 10, 18, true);
        dropdownStyle.alignment = TextAnchor.MiddleLeft;
        dropdownStyle.normal.background = field;
        dropdownStyle.padding = new RectOffset(16, 44, 8, 8);

        rowStyle = ButtonStyle(new Color(1f, 1f, 1f, 0.03f), raisedHover, Text, 8, 17, false);
        rowStyle.alignment = TextAnchor.MiddleLeft;
        rowStyle.padding = new RectOffset(16, 16, 6, 6);
        rowStyle.margin = new RectOffset(0, 0, 2, 2);
        rowStyle.onNormal.background = Keep(ModalGui.Rounded(new Color(Accent.r, Accent.g, Accent.b, 0.2f), 8, Accent, 1.4f));
        rowStyle.onHover.background = rowStyle.onNormal.background;
        rowStyle.onNormal.textColor = Color.white;
        rowStyle.onHover.textColor = Color.white;

        groupStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(4, 4, 6, 2),
        };
        groupStyle.normal.textColor = Accent;

        tagStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleRight,
        };
        tagStyle.normal.textColor = Muted;
        tagStyle.onNormal.textColor = Accent;

        warnTagStyle = new GUIStyle(tagStyle);
        warnTagStyle.normal.textColor = WarnColor;

        primaryStyle = ButtonStyle(Accent, new Color(1f, 0.82f, 0.42f), AccentText, 10, 17, true);
        secondaryStyle = ButtonStyle(raised, raisedHover, Text, 10, 16, false);
        ghostStyle = ButtonStyle(new Color(0f, 0f, 0f, 0f), new Color(1f, 1f, 1f, 0.07f), Muted, 8, 15, false);
        closeStyle = ButtonStyle(new Color(0f, 0f, 0f, 0f), new Color(1f, 1f, 1f, 0.09f), Muted, 18, 20, false);
        closeStyle.padding = new RectOffset(0, 0, 0, 2);

        launcherStyle = ButtonStyle(Accent, new Color(1f, 0.82f, 0.42f), AccentText, 10, 15, true);

        hudStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(12, 12, 12, 12),
            padding = new RectOffset(14, 14, 8, 8),
        };
        hudStyle.normal.background = Keep(ModalGui.Rounded(new Color(0.05f, 0.065f, 0.09f, 0.88f), 12, line, 1f));
        hudStyle.normal.textColor = Text;
    }

    public static void SetInteractionPrompt(string prompt)
    {
        InteractionPrompt = prompt ?? string.Empty;
    }

    /// <summary>
    /// Clears the prompt only if it is still the caller's own, so leaving one
    /// trigger does not wipe the prompt of another the player is still in.
    /// </summary>
    public static void ClearInteractionPrompt(string prompt)
    {
        if (InteractionPrompt == prompt)
        {
            InteractionPrompt = string.Empty;
        }
    }

    private void OnDestroy()
    {
        IsOpen = false;
        InteractionPrompt = string.Empty;
        foreach (Texture2D texture in textures)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }
        if (skin != null)
        {
            Destroy(skin);
        }
    }
}
