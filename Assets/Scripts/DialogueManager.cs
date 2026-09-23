using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    public static bool IsDialogueOpen
    {
        get
        {
            return Instance != null &&
                   Instance.dialoguePanel != null &&
                   Instance.dialoguePanel.activeSelf;
        }
    }

    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button continueButton;

    [Header("Choice UI")]
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button choiceButton1;
    [SerializeField] private Button choiceButton2;
    [SerializeField] private Button choiceButton3;

    [Header("Character Portraits")]
    [SerializeField] private Sprite defaultPlayerPortrait;

    private Image speakerPortraitImage;
    private Image playerPortraitImage;
    private Vector2 speakerPortraitBasePosition;
    private Vector2 playerPortraitBasePosition;
    private float speakerTalkingUntil;
    private float playerTalkingUntil;
    private float speakerTalkAmount;
    private float playerTalkAmount;

    // The talking portrait bobs gently: under one bob a second, a few pixels
    // high, easing in and out instead of hopping and snapping back.
    private const float TalkBobPerSecond = 0.9f;
    private const float TalkBobPixels = 6f;
    private TMP_InputField chatInput;
    private Button sendButton;
    private Button closeButton;
    private GameObject chatComposerObject;
    private bool chatRequestInProgress;

    // A typed request that never calls back (a crashed parser, a hung
    // socket) must not leave the chat box locked, so each request carries a
    // serial and a deadline; a late answer to an abandoned request is dropped.
    private const float ChatRequestTimeoutSeconds = 30f;
    private int chatRequestSerial;
    private float chatRequestDeadline;
    private string pendingPlayerMessage;

    // The emotion box: a small face card that pops up beside the text when a
    // line carries [:emotion] or the AI reply names one.
    private RectTransform emotionBoxRect;
    private Image emotionBoxImage;
    private float emotionBoxShownAt;

    private Button choiceButton4;
    private Sprite activeSpeakerSprite;

    private static readonly Regex LineTag = new Regex(
        @"^\s*\[(?<who>[A-Za-z0-9_]*)(?::(?<emo>[A-Za-z0-9_]+))?\]\s*");

    // =========================================================
    // Dialogue Data
    // =========================================================

    private string[] dialogueLines;
    private int currentLine = 0;

    // กำลังแสดงคำตอบหลังจากเลือก Choice
    private bool showingChoiceResponse = false;

    private List<DialogueChoiceData> activeChoices;
    private string activeDialogueId;
    private string activeNpcId;
    private string activeSpeakerName;
    private string activePersonalityPrompt;
    private string activeDialogueContext;

    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildVisualNovelLayout();
    }

    private void Update()
    {
        if (IsDialogueOpen && !AiSettingsPanel.IsOpen &&
            Input.GetKeyDown(KeyCode.Escape))
        {
            HideDialogue();
            return;
        }

        if (chatRequestInProgress && Time.unscaledTime > chatRequestDeadline)
        {
            Debug.LogWarning("Typed reply timed out; answering with the fallback.");
            chatRequestSerial++; // the late answer, if any, is now stale
            CompleteTypedReply(pendingPlayerMessage, null, chatRequestSerial);
        }

        AnimateEmotionBox();

        AnimatePortrait(
            speakerPortraitImage,
            speakerPortraitBasePosition,
            speakerTalkingUntil,
            ref speakerTalkAmount
        );
        AnimatePortrait(
            playerPortraitImage,
            playerPortraitBasePosition,
            playerTalkingUntil,
            ref playerTalkAmount
        );
    }

    // =========================================================
    // Start Dialogue
    // =========================================================

    public void StartDialogue(
        string characterName,
        string[] lines,
        bool showChoicesAfterDialogue = true)
    {
        StartDialogue(characterName, lines, showChoicesAfterDialogue, null);
    }

    public void StartDialogue(
        string characterName,
        string[] lines,
        bool showChoicesAfterDialogue,
        Sprite speakerPortrait)
    {
        if (IsDialogueOpen)
        {
            return;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("Dialogue has no lines.");
            return;
        }

        dialoguePanel.SetActive(true);
        choicePanel.SetActive(false);
        continueButton.gameObject.SetActive(true);

        showingChoiceResponse = false;

        activeChoices = null;
        activeDialogueId = string.Empty;
        activeNpcId = characterName;
        activeSpeakerName = characterName;
        activePersonalityPrompt = string.Empty;
        activeDialogueContext = string.Join(" ", lines);
        SetPortraits(speakerPortrait);
        if (chatComposerObject != null)
        {
            chatComposerObject.SetActive(false);
        }

        nameText.text = characterName;

        dialogueLines = lines;
        currentLine = 0;

        ShowCurrentLine();

        // Player History
        if (GameState.Instance != null)
        {
            GameState.Instance.AddHistory(
                "Started dialogue with " + characterName
            );
        }
    }

    public void StartDialogue(DialogueData data)
    {
        StartDialogue(data, null);
    }

    public void StartDialogue(
        DialogueData data,
        GeneratedDialogueContent generated)
    {
        if (data == null)
        {
            Debug.LogError("DialogueData is null.");
            return;
        }

        if (IsDialogueOpen || data.lines.Count == 0)
        {
            return;
        }

        dialoguePanel.SetActive(true);
        choicePanel.SetActive(false);
        continueButton.gameObject.SetActive(true);

        showingChoiceResponse = false;
        activeChoices = BuildRuntimeChoices(data, generated);
        // Per dialogue, not per speaker: Alice's Room02 choices must still
        // appear the first time even though she was met in Room01.
        string metFlag = "dialogue." + data.dialogueId + ".met";
        if (GameState.Instance != null)
        {
            if (data.firstMeetingChoicesOnly && GameState.Instance.HasFlag(metFlag))
            {
                activeChoices = new List<DialogueChoiceData>();
            }
            GameState.Instance.SetFlag(metFlag);
        }
        activeDialogueId = data.dialogueId;
        activeNpcId = data.speakerId;
        activeSpeakerName = data.speakerName;
        activePersonalityPrompt = data.personalityPrompt;
        if (chatComposerObject != null)
        {
            chatComposerObject.SetActive(true);
        }

        nameText.text = data.speakerName;
        Sprite portrait = data.speakerPortrait;
        NpcProfileData profile = KnowledgeLibrary.GetNpc(data.speakerId);
        if (portrait == null && profile != null)
        {
            portrait = profile.portrait;
        }
        SetPortraits(portrait);
        dialogueLines = generated != null && generated.IsValid(data.choices.Count)
            ? generated.lines
            : BuildContextualLines(data);
        activeDialogueContext = StripLineTags(string.Join(" ", dialogueLines));
        currentLine = 0;

        if (GameState.Instance != null)
        {
            GameState.Instance.RecordConversation(data.speakerId);
            GameState.Instance.AddConversationTurn(
                data.speakerId, data.speakerId, activeDialogueContext);
        }

        ShowCurrentLine();
        if (chatInput != null)
        {
            chatInput.ActivateInputField();
        }

        if (GameState.Instance != null)
        {
            GameState.Instance.AddHistory(
                "Started dialogue: " + data.dialogueId
            );
        }
    }

    private void BuildVisualNovelLayout()
    {
        if (dialoguePanel == null)
        {
            return;
        }

        RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 175f);
        panelRect.sizeDelta = new Vector2(-48f, 310f);

        Image panelImage = dialoguePanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0.025f, 0.045f, 0.075f, 0.975f);
        }

        speakerPortraitImage = CreatePortraitImage(
            "SpeakerPortrait",
            new Vector2(0f, 0f),
            new Vector2(175f, 0f),
            false
        );
        playerPortraitImage = CreatePortraitImage(
            "PlayerPortrait",
            new Vector2(1f, 0f),
            new Vector2(-175f, 0f),
            true
        );

        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.21f, 1f);
        nameRect.anchorMax = new Vector2(0.79f, 1f);
        nameRect.anchoredPosition = new Vector2(0f, -30f);
        nameRect.sizeDelta = new Vector2(0f, 46f);
        nameText.fontSize = 31f;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Left;
        nameText.color = new Color(0.94f, 0.73f, 0.28f);

        RectTransform textRect = dialogueText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.21f, 0f);
        textRect.anchorMax = new Vector2(0.79f, 1f);
        textRect.offsetMin = new Vector2(0f, 92f);
        textRect.offsetMax = new Vector2(-150f, -72f);
        dialogueText.fontSize = 25f;
        // Long replies shrink to fit instead of spilling past the box.
        dialogueText.enableAutoSizing = true;
        dialogueText.fontSizeMax = 25f;
        dialogueText.fontSizeMin = 15f;
        dialogueText.enableWordWrapping = true;
        dialogueText.overflowMode = TextOverflowModes.Truncate;
        dialogueText.lineSpacing = 5f;
        dialogueText.color = new Color(1f, 1f, 1f, 1f);

        RectTransform continueRect =
            continueButton.GetComponent<RectTransform>();
        continueRect.anchorMin = new Vector2(0.79f, 0f);
        continueRect.anchorMax = new Vector2(0.79f, 0f);
        continueRect.anchoredPosition = new Vector2(-72f, 118f);
        continueRect.sizeDelta = new Vector2(132f, 48f);
        StyleButton(continueButton, new Color(0.12f, 0.38f, 0.48f));
        SetButtonText(continueButton, "ถัดไป  ›");

        CreateChatComposer();
        CreateCloseButton();
        CreateEmotionBox();

        if (choicePanel != null)
        {
            RectTransform choiceRect = choicePanel.GetComponent<RectTransform>();
            choiceRect.anchorMin = new Vector2(0.5f, 0f);
            choiceRect.anchorMax = new Vector2(0.5f, 0f);
            choiceRect.pivot = new Vector2(0.5f, 0f);
            choiceRect.anchoredPosition = new Vector2(0f, 345f);

            Image choiceImage = choicePanel.GetComponent<Image>();
            if (choiceImage != null)
            {
                choiceImage.color = new Color(0.035f, 0.065f, 0.1f, 0.975f);
            }

            // The fourth option exists for events such as a quarrel, where
            // the player can listen, mediate, or side with either person.
            choiceButton4 = CreateRuntimeButton(
                choicePanel.transform,
                "ChoiceButton4",
                string.Empty,
                new Color(0.08f, 0.2f, 0.28f)
            );
            choiceButton4.onClick.AddListener(() => SelectChoice(3));

            LayoutChoices(3);
        }
    }

    private const float ChoiceSpacing = 70f;

    /// <summary>Stacks the visible choice buttons and sizes the panel to fit.</summary>
    private void LayoutChoices(int count)
    {
        if (choicePanel == null)
        {
            return;
        }

        count = Mathf.Clamp(count, 1, 4);
        RectTransform choiceRect = choicePanel.GetComponent<RectTransform>();
        choiceRect.sizeDelta = new Vector2(760f, count * ChoiceSpacing + 34f);

        Button[] buttons = { choiceButton1, choiceButton2, choiceButton3, choiceButton4 };
        float top = (count - 1) * 0.5f * ChoiceSpacing;
        for (int index = 0; index < buttons.Length; index++)
        {
            LayoutChoiceButton(buttons[index], top - index * ChoiceSpacing);
        }
    }

    private void CreateEmotionBox()
    {
        GameObject frame = new GameObject(
            "EmotionBox",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        frame.layer = dialoguePanel.layer;
        frame.transform.SetParent(dialoguePanel.transform, false);
        emotionBoxRect = frame.GetComponent<RectTransform>();
        emotionBoxRect.anchorMin = new Vector2(0.21f, 1f);
        emotionBoxRect.anchorMax = new Vector2(0.21f, 1f);
        emotionBoxRect.pivot = new Vector2(0f, 0f);
        emotionBoxRect.anchoredPosition = new Vector2(0f, 12f);
        emotionBoxRect.sizeDelta = new Vector2(196f, 196f);
        // No frame: the faces have transparent backgrounds and float on
        // their own above the dialogue box.
        Image frameImage = frame.GetComponent<Image>();
        frameImage.enabled = false;
        frameImage.raycastTarget = false;

        GameObject face = new GameObject(
            "EmotionFace",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        face.layer = dialoguePanel.layer;
        face.transform.SetParent(frame.transform, false);
        RectTransform faceRect = face.GetComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = Vector2.zero;
        faceRect.offsetMax = Vector2.zero;
        emotionBoxImage = face.GetComponent<Image>();
        emotionBoxImage.preserveAspect = true;
        emotionBoxImage.raycastTarget = false;

        frame.SetActive(false);
    }

    private void ShowEmotion(string speakerId, string emotionId)
    {
        if (emotionBoxRect == null)
        {
            return;
        }

        NpcProfileData profile = KnowledgeLibrary.GetNpc(speakerId);
        EmotionPortrait emotion = profile != null
            ? profile.FindEmotion(emotionId)
            : null;
        if (emotion == null)
        {
            HideEmotion();
            return;
        }

        emotionBoxImage.sprite = emotion.sprite;
        emotionBoxRect.gameObject.SetActive(true);
        emotionBoxShownAt = Time.unscaledTime;
    }

    private void HideEmotion()
    {
        if (emotionBoxRect != null)
        {
            emotionBoxRect.gameObject.SetActive(false);
        }
    }

    private void AnimateEmotionBox()
    {
        if (emotionBoxRect == null || !emotionBoxRect.gameObject.activeInHierarchy)
        {
            return;
        }

        // A quick overshoot so the face "pops" like a reaction bubble.
        float t = Mathf.Clamp01((Time.unscaledTime - emotionBoxShownAt) / 0.22f);
        float scale = t < 1f
            ? Mathf.Lerp(0.55f, 1.08f, t)
            : 1f + 0.08f * Mathf.Exp(-(Time.unscaledTime - emotionBoxShownAt - 0.22f) * 18f);
        emotionBoxRect.localScale = new Vector3(scale, scale, 1f);
    }

    /// <summary>
    /// Applies a line's leading tag and returns the text to show. [Stelle]
    /// switches the name and portrait to that NPC for this line, [:shock]
    /// pops the emotion box, and [Stelle:shock] does both.
    /// </summary>
    private string ApplyLineTag(string line)
    {
        string speakerId = activeNpcId;
        string emotionId = null;
        string text = line ?? string.Empty;

        Match match = LineTag.Match(text);
        if (match.Success)
        {
            text = text.Substring(match.Length);
            string who = match.Groups["who"].Value;
            if (!string.IsNullOrEmpty(who))
            {
                speakerId = who;
            }
            emotionId = match.Groups["emo"].Success
                ? match.Groups["emo"].Value
                : null;
        }

        ShowSpeaker(speakerId);
        if (string.IsNullOrEmpty(emotionId))
        {
            HideEmotion();
        }
        else
        {
            ShowEmotion(speakerId, emotionId);
        }

        return text;
    }

    private void ShowSpeaker(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId) || speakerId == activeNpcId)
        {
            nameText.text = activeSpeakerName;
            if (speakerPortraitImage != null)
            {
                speakerPortraitImage.sprite = activeSpeakerSprite;
                speakerPortraitImage.gameObject.SetActive(activeSpeakerSprite != null);
            }
            return;
        }

        NpcProfileData profile = KnowledgeLibrary.GetNpc(speakerId);
        nameText.text = profile != null && !string.IsNullOrWhiteSpace(profile.displayName)
            ? profile.displayName
            : speakerId;
        if (speakerPortraitImage != null && profile != null && profile.portrait != null)
        {
            speakerPortraitImage.sprite = profile.portrait;
            speakerPortraitImage.gameObject.SetActive(true);
        }
    }

    /// <summary>Line text for logs and prompts: [Who] becomes "Who: ", faces vanish.</summary>
    public static string StripLineTags(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return Regex.Replace(
            text,
            @"\[(?<who>[A-Za-z0-9_]*)(?::[A-Za-z0-9_]+)?\]\s*",
            m => string.IsNullOrEmpty(m.Groups["who"].Value)
                ? string.Empty
                : m.Groups["who"].Value + ": ");
    }

    private void CreateCloseButton()
    {
        closeButton = CreateRuntimeButton(
            dialoguePanel.transform,
            "CloseDialogueButton",
            "ปิด  ×",
            new Color(0.32f, 0.12f, 0.15f)
        );
        closeButton.transform.SetAsLastSibling();
        RectTransform rect = closeButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-14f, -14f);
        rect.sizeDelta = new Vector2(112f, 42f);
        closeButton.onClick.AddListener(HideDialogue);
    }

    private void CreateChatComposer()
    {
        GameObject composer = new GameObject(
            "ChatComposer",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        composer.layer = dialoguePanel.layer;
        chatComposerObject = composer;
        composer.transform.SetParent(dialoguePanel.transform, false);
        RectTransform composerRect = composer.GetComponent<RectTransform>();
        composerRect.anchorMin = new Vector2(0.21f, 0f);
        composerRect.anchorMax = new Vector2(0.79f, 0f);
        composerRect.anchoredPosition = new Vector2(0f, 24f);
        composerRect.sizeDelta = new Vector2(0f, 56f);
        Image composerImage = composer.GetComponent<Image>();
        composerImage.color = new Color(0.055f, 0.09f, 0.13f, 1f);

        GameObject inputObject = new GameObject(
            "TypedDialogueInput",
            typeof(RectTransform),
            typeof(TMP_InputField)
        );
        inputObject.layer = dialoguePanel.layer;
        inputObject.transform.SetParent(composer.transform, false);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = Vector2.zero;
        inputRect.anchorMax = Vector2.one;
        inputRect.offsetMin = new Vector2(18f, 6f);
        inputRect.offsetMax = new Vector2(-126f, -6f);

        TMP_Text inputText = CreateInputText(
            inputObject.transform,
            "Text",
            "",
            Color.white
        );
        TMP_Text placeholder = CreateInputText(
            inputObject.transform,
            "Placeholder",
            "พิมพ์สิ่งที่อยากพูดกับ NPC...",
            new Color(0.78f, 0.84f, 0.9f, 1f)
        );
        placeholder.fontStyle = FontStyles.Italic;

        chatInput = inputObject.GetComponent<TMP_InputField>();
        chatInput.textComponent = inputText;
        chatInput.placeholder = placeholder;
        chatInput.textViewport = inputRect;
        chatInput.characterLimit = 400;
        chatInput.lineType = TMP_InputField.LineType.SingleLine;
        chatInput.onEndEdit.AddListener(HandleChatEndEdit);

        sendButton = CreateRuntimeButton(
            composer.transform,
            "SendButton",
            "ส่ง",
            new Color(0.78f, 0.55f, 0.2f)
        );
        TMP_Text sendLabel = sendButton.GetComponentInChildren<TMP_Text>();
        if (sendLabel != null)
        {
            sendLabel.color = new Color(0.03f, 0.045f, 0.06f);
        }
        RectTransform sendRect = sendButton.GetComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(1f, 0f);
        sendRect.anchorMax = new Vector2(1f, 1f);
        sendRect.pivot = new Vector2(1f, 0.5f);
        sendRect.anchoredPosition = new Vector2(-6f, 0f);
        sendRect.sizeDelta = new Vector2(108f, -12f);
        sendButton.onClick.AddListener(SendTypedMessage);
    }

    private TMP_Text CreateInputText(
        Transform parent,
        string objectName,
        string value,
        Color color)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );
        textObject.layer = dialoguePanel.layer;
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 0f);
        rect.offsetMax = new Vector2(-4f, 0f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = 22f;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        return text;
    }

    private Button CreateRuntimeButton(
        Transform parent,
        string objectName,
        string label,
        Color color)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.layer = dialoguePanel.layer;
        buttonObject.transform.SetParent(parent, false);
        Button button = buttonObject.GetComponent<Button>();
        StyleButton(button, color);

        TMP_Text text = CreateInputText(
            buttonObject.transform,
            "Label",
            label,
            Color.white
        );
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private Image CreatePortraitImage(
        string objectName,
        Vector2 anchor,
        Vector2 position,
        bool flipHorizontally)
    {
        GameObject portraitObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        portraitObject.layer = dialoguePanel.layer;
        portraitObject.transform.SetParent(dialoguePanel.transform, false);
        portraitObject.transform.SetAsFirstSibling();

        RectTransform portraitRect = portraitObject.GetComponent<RectTransform>();
        portraitRect.anchorMin = anchor;
        portraitRect.anchorMax = anchor;
        portraitRect.pivot = new Vector2(0.5f, 0f);
        portraitRect.anchoredPosition = position;
        portraitRect.sizeDelta = new Vector2(330f, 500f);
        portraitRect.localScale = new Vector3(flipHorizontally ? -1f : 1f, 1f, 1f);

        Image portrait = portraitObject.GetComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portraitObject.SetActive(false);

        if (!flipHorizontally)
        {
            speakerPortraitBasePosition = position;
        }
        else
        {
            playerPortraitBasePosition = position;
        }
        return portrait;
    }

    private static void LayoutChoiceButton(Button button, float y)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(700f, 58f);
        StyleButton(button, new Color(0.08f, 0.2f, 0.28f));
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.fontSize = 22f;
            text.enableAutoSizing = true;
            text.fontSizeMax = 22f;
            text.fontSizeMin = 14f;
            text.enableWordWrapping = true;
            text.color = new Color(0.92f, 0.95f, 1f);
        }
    }

    private static void StyleButton(Button button, Color normal)
    {
        if (button == null)
        {
            return;
        }

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = normal;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = normal;
        colors.highlightedColor = Color.Lerp(normal, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(normal, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.4f);
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.color = Color.white;
        }
    }

    private void SetPortraits(Sprite speakerPortrait)
    {
        if (speakerPortraitImage == null || playerPortraitImage == null)
        {
            return;
        }

        activeSpeakerSprite = speakerPortrait;
        speakerPortraitImage.sprite = speakerPortrait;
        speakerPortraitImage.gameObject.SetActive(speakerPortrait != null);

        playerPortraitImage.sprite = defaultPlayerPortrait;
        playerPortraitImage.gameObject.SetActive(defaultPlayerPortrait != null);
    }

    private List<DialogueChoiceData> BuildRuntimeChoices(
        DialogueData data,
        GeneratedDialogueContent generated)
    {
        if (generated == null || !generated.IsValid(data.choices.Count))
        {
            return data.choices;
        }

        List<DialogueChoiceData> runtimeChoices =
            new List<DialogueChoiceData>();

        for (int index = 0; index < data.choices.Count; index++)
        {
            DialogueChoiceData authoredChoice = data.choices[index];
            GeneratedDialogueChoice generatedChoice = generated.choices[index];

            runtimeChoices.Add(
                new DialogueChoiceData
                {
                    optionText = generatedChoice.optionText,
                    responseText = generatedChoice.responseText,
                    actions = authoredChoice.actions
                }
            );
        }

        return runtimeChoices;
    }

    private string[] BuildContextualLines(DialogueData data)
    {
        GameState state = GameState.Instance;

        // Authored repeat lines are tracked per dialogue, not per speaker:
        // Sena owns several dialogues and each stage needs its own
        // "you are back" variant, and none of them may fall through to the
        // generic friendly greeting.
        if (data.overrideReturnLines && data.returnLines.Count > 0)
        {
            if (state == null)
            {
                return data.lines.ToArray();
            }

            string seenFlag = "dialogue." + data.dialogueId + ".seen";
            bool alreadySeen = state.HasFlag(seenFlag);
            state.SetFlag(seenFlag);

            return alreadySeen
                ? data.returnLines.ToArray()
                : data.lines.ToArray();
        }

        if (state == null || state.GetConversationCount(data.speakerId) == 0)
        {
            return data.lines.ToArray();
        }

        // Repeat visits open with the NPC's own authored greeting, chosen by
        // relationship and by whether the room changed since the last talk.
        NpcProfileData profile = KnowledgeLibrary.GetNpc(data.speakerId);
        string greeting = NpcOfflineReplies.ReturnGreeting(profile, state);
        if (string.IsNullOrWhiteSpace(greeting))
        {
            return data.lines.ToArray();
        }

        List<string> lines = new List<string> { greeting };

        // Show that she remembers: the last thing the player actually typed.
        string lastPlayerLine = LastPlayerLine(state, data.speakerId);
        if (!string.IsNullOrEmpty(lastPlayerLine))
        {
            string name = profile != null && !string.IsNullOrWhiteSpace(profile.displayName)
                ? profile.displayName
                : data.speakerName;
            name = Regex.Replace(name, @"\s*\([^)]*\)", string.Empty);   // "สเตล (Stelle)" -> "สเตล"
            lines.Add("(" + name + " ยังจำได้ว่าครั้งก่อนคุณพูดว่า \"" +
                      lastPlayerLine + "\")");
        }
        else if (data.lines.Count > 0)
        {
            lines.Add(data.lines[data.lines.Count - 1]);
        }

        return lines.ToArray();
    }

    private static string LastPlayerLine(GameState state, string npcId)
    {
        IReadOnlyList<ConversationTurn> log = state.GetConversationLog(npcId);
        for (int i = log.Count - 1; i >= 0; i--)
        {
            if (log[i] != null && log[i].SpeakerId == ConversationTurn.Player &&
                !log[i].Text.StartsWith(ChoicePrefix))
            {
                string text = log[i].Text;
                return text.Length > 60 ? text.Substring(0, 60) + "…" : text;
            }
        }

        return null;
    }

    private const string ChoicePrefix = "(เลือก) ";

    // =========================================================
    // Show Current Line
    // =========================================================

    private void ShowCurrentLine()
    {
        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            HideDialogue();
            return;
        }

        if (currentLine < 0 || currentLine >= dialogueLines.Length)
        {
            HideDialogue();
            return;
        }

        dialogueText.text = ApplyLineTag(dialogueLines[currentLine]);
        AnimateSpeakerForText(dialogueText.text);
        SfxPlayer.Play(SfxPlayer.Cue.Talk);
    }

    // =========================================================
    // Next Line
    // =========================================================

    public void NextLine()
    {
        // ถ้ากำลังแสดงคำตอบหลัง Choice
        if (showingChoiceResponse)
        {
            HideDialogue();
            return;
        }

        currentLine++;

        // ยังมีบทสนทนาเหลือ
        if (currentLine < dialogueLines.Length)
        {
            ShowCurrentLine();
            return;
        }

        // Dialogue จบแล้ว
        continueButton.gameObject.SetActive(false);

        if (activeChoices == null || activeChoices.Count == 0)
        {
            // A conversation with a chat box stays open on its last line so
            // the player can type; plain descriptions simply close.
            if (chatComposerObject != null && chatComposerObject.activeSelf)
            {
                currentLine = dialogueLines.Length - 1;
                if (chatInput != null)
                {
                    chatInput.ActivateInputField();
                }
                return;
            }

            HideDialogue();
            return;
        }

        ShowChoices();
    }

    // =========================================================
    // Show Choices
    // =========================================================

    private void ShowChoices()
    {
        dialoguePanel.SetActive(true);
        choicePanel.SetActive(true);

        continueButton.gameObject.SetActive(false);
        HideEmotion();

        LayoutChoices(activeChoices != null ? activeChoices.Count : 1);
        ConfigureChoiceButton(choiceButton1, 0);
        ConfigureChoiceButton(choiceButton2, 1);
        ConfigureChoiceButton(choiceButton3, 2);
        ConfigureChoiceButton(choiceButton4, 3);
    }

    private void ConfigureChoiceButton(Button button, int index)
    {
        if (button == null)
        {
            return;
        }

        bool isAvailable =
            activeChoices != null && index < activeChoices.Count;

        button.gameObject.SetActive(isAvailable);

        if (isAvailable)
        {
            SetButtonText(button, activeChoices[index].optionText);
        }
    }

    // =========================================================
    // Set Choice Button Text
    // =========================================================

    private void SetButtonText(Button button, string text)
    {
        if (button == null)
        {
            Debug.LogError("Choice button is not assigned.");
            return;
        }

        TMP_Text buttonText =
            button.GetComponentInChildren<TMP_Text>();

        if (buttonText == null)
        {
            Debug.LogError(
                "TMP Text not found inside button: " +
                button.name
            );

            return;
        }

        buttonText.text = text;
    }

    // =========================================================
    // Select Choice
    // =========================================================

    public void SelectChoice(int choiceIndex)
    {
        // ซ่อน Choice
        choicePanel.SetActive(false);

        // แสดงปุ่มต่อ
        continueButton.gameObject.SetActive(true);

        // ตอนนี้กำลังแสดงคำตอบหลัง Choice
        showingChoiceResponse = true;

        if (activeChoices == null ||
            choiceIndex < 0 ||
            choiceIndex >= activeChoices.Count)
        {
            Debug.LogError("Dialogue choice index is invalid.");
            HideDialogue();
            return;
        }

        DialogueChoiceData selectedChoice =
            activeChoices[choiceIndex];

        dialogueText.text = ApplyLineTag(selectedChoice.responseText);
        AnimateSpeakerForText(dialogueText.text);
        SfxPlayer.Play(SfxPlayer.Cue.Talk);

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(activeNpcId))
        {
            GameState.Instance.AddConversationTurn(
                activeNpcId, ConversationTurn.Player,
                ChoicePrefix + selectedChoice.optionText);
            GameState.Instance.AddConversationTurn(
                activeNpcId, activeNpcId,
                StripLineTags(selectedChoice.responseText));
        }

        // ---------------------------------------------
        // Relationship
        // ---------------------------------------------

        if (GameState.Instance == null)
        {
            Debug.LogError("GameState.Instance is null.");
        }
        else
        {
            foreach (ActionCommand action in selectedChoice.actions)
            {
                if (action != null)
                {
                    action.Execute(GameState.Instance);
                }
            }

            GameState.Instance.AddHistory(
                "Selected " + activeDialogueId + " choice " +
                (choiceIndex + 1)
            );
        }

        Debug.Log(
            "Dialogue choice selected: " +
            activeDialogueId + "[" + choiceIndex + "]"
        );
    }

    public void SendTypedMessage()
    {
        if (chatInput == null || chatRequestInProgress)
        {
            return;
        }

        string playerMessage = chatInput.text.Trim();
        if (string.IsNullOrEmpty(playerMessage))
        {
            chatInput.ActivateInputField();
            return;
        }

        chatInput.text = string.Empty;
        chatRequestInProgress = true;
        chatRequestSerial++;
        chatRequestDeadline = Time.unscaledTime + ChatRequestTimeoutSeconds;
        pendingPlayerMessage = playerMessage;
        int serial = chatRequestSerial;
        chatInput.interactable = false;
        sendButton.interactable = false;
        choicePanel.SetActive(false);
        continueButton.gameObject.SetActive(false);
        HideEmotion();
        ShowSpeaker(activeNpcId);
        playerTalkingUntil = Time.unscaledTime +
                             Mathf.Clamp(playerMessage.Length * 0.025f, 0.5f, 2f);

        dialogueText.text =
            "<color=#69D0D8><b>คุณ:</b></color> " +
            EscapeRichText(playerMessage) +
            "\n\n<color=#E8B950><b>" +
            EscapeRichText(activeSpeakerName) +
            ":</b></color> กำลังคิด...";

        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator != null && generator.CanGenerate)
        {
            StartCoroutine(
                generator.GenerateReply(
                    activeNpcId,
                    activeSpeakerName,
                    activeDialogueContext,
                    playerMessage,
                    reply => CompleteTypedReply(playerMessage, reply, serial),
                    activePersonalityPrompt
                )
            );
            return;
        }

        CompleteTypedReply(playerMessage, null, serial);
    }

    private void HandleChatEndEdit(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            (Input.GetKeyDown(KeyCode.Return) ||
             Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SendTypedMessage();
        }
    }

    private void CompleteTypedReply(
        string playerMessage,
        GeneratedChatReply generated,
        int serial)
    {
        // A reply to a request that timed out or belongs to a conversation
        // the player already closed.
        if (serial != chatRequestSerial || !chatRequestInProgress)
        {
            return;
        }

        chatRequestInProgress = false;
        if (!IsDialogueOpen)
        {
            return;
        }

        bool usedFallback = generated == null;
        GeneratedChatReply reply = generated ?? BuildFallbackReply(playerMessage);
        int relationshipDelta = Mathf.Clamp(reply.relationshipDelta, -15, 5);

        GameState state = GameState.Instance;
        if (state != null && !string.IsNullOrWhiteSpace(activeNpcId))
        {
            if (relationshipDelta != 0)
            {
                state.ChangeRelationship(activeNpcId, relationshipDelta);
            }
            state.AddConversationTurn(
                activeNpcId, ConversationTurn.Player, playerMessage);
            state.AddConversationTurn(activeNpcId, activeNpcId, reply.reply);
            state.AddHistory(
                "Typed dialogue with " + activeNpcId +
                " (relationship " + relationshipDelta + ")"
            );
            RememberToldSecrets(state, reply);
        }

        string serviceNotice = string.Empty;
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (usedFallback && generator != null && generator.CanGenerate &&
            !string.IsNullOrWhiteSpace(generator.LastError))
        {
            serviceNotice =
                "\n\n<color=#FFB4A2><size=18>AI ใช้งานไม่ได้: " +
                EscapeRichText(generator.LastError) +
                " — จึงใช้คำตอบสำรอง</size></color>";
        }

        dialogueText.text =
            "<color=#69D0D8><b>คุณ:</b></color> " +
            EscapeRichText(playerMessage) +
            "\n\n<color=#E8B950><b>" +
            EscapeRichText(activeSpeakerName) +
            ":</b></color> " + EscapeRichText(reply.reply) +
            serviceNotice;
        AnimateSpeakerForText(reply.reply);
        if (string.IsNullOrWhiteSpace(reply.emotion))
        {
            HideEmotion();
        }
        else
        {
            ShowEmotion(activeNpcId, reply.emotion);
        }

        // Check if player answered Sena's riddle correctly
        bool isSena = activeNpcId != null &&
                      activeNpcId.ToLowerInvariant().Contains("sena");
        if (isSena &&
            SenaInteraction.IsListeningForAnswer &&
            SenaInteraction.IsCorrectRiddleAnswer(playerMessage))
        {
            if (SenaInteraction.Instance != null)
            {
                SenaInteraction.Instance.HandleRiddleSolved();
            }
        }

        chatInput.interactable = true;
        sendButton.interactable = true;
        chatInput.ActivateInputField();
    }

    /// <summary>
    /// A secret the NPC actually told becomes a lasting flag
    /// (secret.&lt;npc&gt;.&lt;id&gt;.told) and a memory, so later dialogue and
    /// conditions can react to the player knowing it.
    /// </summary>
    private void RememberToldSecrets(GameState state, GeneratedChatReply reply)
    {
        if (reply.referencedFactIds == null)
        {
            return;
        }

        NpcProfileData profile = KnowledgeLibrary.GetNpc(activeNpcId);
        if (profile == null)
        {
            return;
        }

        foreach (string factId in reply.referencedFactIds)
        {
            if (string.IsNullOrWhiteSpace(factId) || !profile.IsSecret(factId))
            {
                continue;
            }

            string flag = "secret." + profile.npcId + "." + factId + ".told";
            if (!state.HasFlag(flag))
            {
                state.SetFlag(flag);
                state.AddNpcMemory(
                    profile.npcId,
                    "เล่าความลับเรื่อง " + factId + " ให้ผู้เล่นฟังแล้ว");
            }
        }
    }

    /// <summary>
    /// Offline reply, entirely from data: the NPC's own reply rules, then the
    /// room's hint ladder, then her neutral lines. See NpcOfflineReplies.
    /// </summary>
    private GeneratedChatReply BuildFallbackReply(string playerMessage)
    {
        NpcKnowledgeContext knowledge =
            AiDialogueGenerator.BuildKnowledgeContext(activeNpcId, playerMessage);
        return NpcOfflineReplies.Build(knowledge, playerMessage, GameState.Instance);
    }

    private static string EscapeRichText(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    // =========================================================
    // Hide Dialogue
    // =========================================================

    public void HideDialogue()
    {
        dialoguePanel.SetActive(false);
        choicePanel.SetActive(false);
        continueButton.gameObject.SetActive(false);

        showingChoiceResponse = false;
        activeChoices = null;
        activeDialogueId = string.Empty;
        activeNpcId = string.Empty;
        activeSpeakerName = string.Empty;
        activePersonalityPrompt = string.Empty;
        activeDialogueContext = string.Empty;
        speakerTalkingUntil = 0f;
        playerTalkingUntil = 0f;
        chatRequestInProgress = false;
        pendingPlayerMessage = string.Empty;
        HideEmotion();
        if (chatInput != null)
        {
            chatInput.text = string.Empty;
            chatInput.interactable = true;
        }
        if (sendButton != null)
        {
            sendButton.interactable = true;
        }
    }

    private void AnimateSpeakerForText(string text)
    {
        int length = string.IsNullOrEmpty(text) ? 0 : text.Length;
        speakerTalkingUntil = Time.unscaledTime +
                              Mathf.Clamp(length * 0.035f, 0.7f, 2.8f);
    }

    private static void AnimatePortrait(
        Image portrait,
        Vector2 basePosition,
        float talkingUntil,
        ref float talkAmount)
    {
        if (portrait == null || !portrait.gameObject.activeInHierarchy)
        {
            talkAmount = 0f;
            return;
        }

        float target = Time.unscaledTime < talkingUntil ? 1f : 0f;
        talkAmount = Mathf.MoveTowards(talkAmount, target, Time.unscaledDeltaTime * 2.5f);

        float wave = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * TalkBobPerSecond * 2f * Mathf.PI);
        float bob = wave * TalkBobPixels * Mathf.SmoothStep(0f, 1f, talkAmount);
        portrait.GetComponent<RectTransform>().anchoredPosition =
            basePosition + new Vector2(0f, bob);
    }
}
