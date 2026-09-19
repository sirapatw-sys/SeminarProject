using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysteryGame.Core;
using System.Collections.Generic;

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
    private TMP_InputField chatInput;
    private Button sendButton;
    private Button closeButton;
    private GameObject chatComposerObject;
    private bool chatRequestInProgress;

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

        AnimatePortrait(
            speakerPortraitImage,
            speakerPortraitBasePosition,
            speakerTalkingUntil
        );
        AnimatePortrait(
            playerPortraitImage,
            playerPortraitBasePosition,
            playerTalkingUntil
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
        activeDialogueContext = string.Join(" ", lines);
        SetPortraits(null);
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
        activeDialogueId = data.dialogueId;
        activeNpcId = data.speakerId;
        activeSpeakerName = data.speakerName;
        if (chatComposerObject != null)
        {
            chatComposerObject.SetActive(true);
        }

        nameText.text = data.speakerName;
        SetPortraits(data.speakerPortrait);
        dialogueLines = generated != null && generated.IsValid(data.choices.Count)
            ? generated.lines
            : BuildContextualLines(data);
        activeDialogueContext = string.Join(" ", dialogueLines);
        currentLine = 0;

        if (GameState.Instance != null)
        {
            GameState.Instance.RecordConversation(data.speakerId);
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

        CreateAccentLine();

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

        if (choicePanel != null)
        {
            RectTransform choiceRect = choicePanel.GetComponent<RectTransform>();
            choiceRect.anchorMin = new Vector2(0.5f, 0f);
            choiceRect.anchorMax = new Vector2(0.5f, 0f);
            choiceRect.anchoredPosition = new Vector2(0f, 485f);
            choiceRect.sizeDelta = new Vector2(760f, 244f);

            Image choiceImage = choicePanel.GetComponent<Image>();
            if (choiceImage != null)
            {
                choiceImage.color = new Color(0.035f, 0.065f, 0.1f, 0.975f);
            }

            LayoutChoiceButton(choiceButton1, 72f);
            LayoutChoiceButton(choiceButton2, 0f);
            LayoutChoiceButton(choiceButton3, -72f);
        }
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

    private void CreateAccentLine()
    {
        GameObject accent = new GameObject(
            "DialogueAccent",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        accent.layer = dialoguePanel.layer;
        accent.transform.SetParent(dialoguePanel.transform, false);
        RectTransform rect = accent.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, 4f);
        accent.GetComponent<Image>().color =
            new Color(0.78f, 0.55f, 0.2f, 0.95f);
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
        if (state == null || state.GetConversationCount(data.speakerId) == 0)
        {
            return data.lines.ToArray();
        }

        List<string> lines = new List<string>();
        int relationship = state.GetRelationship(data.speakerId);
        bool worldChanged =
            state.HasWorldChangedSinceConversation(data.speakerId);
        if (worldChanged && relationship >= 70)
        {
            lines.Add("กลับมาแล้วสินะ ฉันเห็นว่าเธอจัดการบางอย่างในห้องไปแล้ว เรามาทบทวนกันเถอะ");
        }
        else if (worldChanged)
        {
            lines.Add("ระหว่างที่เราแยกกัน สถานการณ์ในห้องเปลี่ยนไปแล้วสินะ เธอพบอะไรเพิ่มบ้าง");
        }
        else if (relationship >= 70)
        {
            lines.Add("กลับมาแล้วสินะ ดีเลย ฉันสบายใจขึ้นเมื่อมีเธออยู่ใกล้ ๆ");
        }
        else if (relationship <= 35)
        {
            lines.Add("เธอกลับมาคุยอีกแล้ว... ฉันยังไม่ลืมสิ่งที่เกิดขึ้นหรอกนะ");
        }
        else
        {
            lines.Add("กลับมาแล้วเหรอ ระหว่างนี้สถานการณ์ในห้องเปลี่ยนไปพอสมควรนะ");
        }

        IReadOnlyList<string> memories = state.GetNpcMemory(data.speakerId);
        if (memories.Count > 0)
        {
            string memory = memories[memories.Count - 1];
            memory = memory.Replace("ผู้เล่นพูดว่า:", "ครั้งก่อนเธอบอกว่า");
            lines.Add(memory + " ฉันยังจำได้อยู่");
        }
        else if (worldChanged && state.GetPlayerHistory().Count > 2)
        {
            lines.Add("ดูเหมือนเธอจะตรวจสอบอะไรเพิ่มมาแล้ว เล่าให้ฉันฟังได้นะ");
        }
        else if (data.lines.Count > 0)
        {
            lines.Add(data.lines[data.lines.Count - 1]);
        }

        return lines.ToArray();
    }

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

        dialogueText.text = dialogueLines[currentLine];
        AnimateSpeakerForText(dialogueText.text);
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

        ConfigureChoiceButton(choiceButton1, 0);
        ConfigureChoiceButton(choiceButton2, 1);
        ConfigureChoiceButton(choiceButton3, 2);
    }

    private void ConfigureChoiceButton(Button button, int index)
    {
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

        dialogueText.text = selectedChoice.responseText;
        AnimateSpeakerForText(dialogueText.text);

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
        chatInput.interactable = false;
        sendButton.interactable = false;
        choicePanel.SetActive(false);
        continueButton.gameObject.SetActive(false);
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
                    reply => CompleteTypedReply(playerMessage, reply)
                )
            );
            return;
        }

        CompleteTypedReply(playerMessage, null);
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
        GeneratedChatReply generated)
    {
        if (!IsDialogueOpen)
        {
            chatRequestInProgress = false;
            return;
        }

        bool usedFallback = generated == null;
        GeneratedChatReply reply = generated ?? BuildFallbackReply(playerMessage);
        int relationshipDelta = Mathf.Clamp(reply.relationshipDelta, -3, 3);

        if (GameState.Instance != null && !string.IsNullOrWhiteSpace(activeNpcId))
        {
            if (relationshipDelta != 0)
            {
                GameState.Instance.ChangeRelationship(
                    activeNpcId,
                    relationshipDelta
                );
            }
            GameState.Instance.AddNpcMemory(
                activeNpcId,
                "ผู้เล่นพูดว่า: " + playerMessage
            );
            GameState.Instance.AddHistory(
                "Typed dialogue with " + activeNpcId +
                " (relationship " + relationshipDelta + ")"
            );
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

        chatRequestInProgress = false;
        chatInput.interactable = true;
        sendButton.interactable = true;
        chatInput.ActivateInputField();
    }

    private GeneratedChatReply BuildFallbackReply(string playerMessage)
    {
        string normalized = playerMessage.ToLowerInvariant();
        if (ContainsAny(
            normalized,
            "ขอบคุณ", "ไม่เป็นไร", "ช่วย", "เป็นห่วง", "เข้าใจ", "โอเค"))
        {
            return new GeneratedChatReply
            {
                reply = "ขอบคุณนะ อย่างน้อยฉันก็รู้ว่าไม่ได้ต้องรับมือกับเรื่องนี้คนเดียว",
                relationshipDelta = 1
            };
        }

        if (ContainsAny(
            normalized,
            "รำคาญ", "เงียบ", "ไม่สน", "ไร้สาระ", "โง่"))
        {
            return new GeneratedChatReply
            {
                reply = "เข้าใจแล้ว ฉันจะไม่รบกวน แต่คำพูดนั้นก็เจ็บอยู่เหมือนกัน",
                relationshipDelta = -1
            };
        }

        if (ContainsAny(
            normalized,
            "ทำยังไง", "ทำอย่างไร", "ทางออก", "เบาะแส", "ต่อไป"))
        {
            return new GeneratedChatReply
            {
                reply = "ลองทบทวนสิ่งที่เราเพิ่งตรวจพบก่อนนะ บางอย่างในห้องอาจเชื่อมโยงกันมากกว่าที่เห็น",
                relationshipDelta = 0
            };
        }

        if (ContainsAny(
            normalized,
            "เป็นไง", "เป็นอย่างไร", "รู้สึก", "กลัว", "โอเคไหม"))
        {
            return new GeneratedChatReply
            {
                reply = "ยังไม่ถึงกับสบายใจ แต่การได้คุยกันก็ช่วยให้ฉันตั้งสติได้มากขึ้น",
                relationshipDelta = 1
            };
        }

        int conversationIndex = GameState.Instance != null
            ? GameState.Instance.GetConversationCount(activeNpcId)
            : 0;
        string[] neutralReplies =
        {
            "ฉันฟังอยู่ ลองเล่าต่อสิ เผื่อเราจะเห็นรายละเอียดที่มองข้ามไป",
            "เรื่องนั้นน่าสนใจนะ ฉันจะจำไว้ตอนที่เราตรวจห้องต่อ",
            "เข้าใจแล้ว เราค่อย ๆ แยกสิ่งที่รู้จริงออกจากสิ่งที่เราคาดเดากันเถอะ"
        };

        return new GeneratedChatReply
        {
            reply = neutralReplies[Mathf.Abs(conversationIndex) % neutralReplies.Length],
            relationshipDelta = 0
        };
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        foreach (string value in values)
        {
            if (source.Contains(value))
            {
                return true;
            }
        }

        return false;
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
        activeDialogueContext = string.Empty;
        speakerTalkingUntil = 0f;
        playerTalkingUntil = 0f;
        chatRequestInProgress = false;
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
        float talkingUntil)
    {
        if (portrait == null || !portrait.gameObject.activeInHierarchy)
        {
            return;
        }

        RectTransform portraitRect = portrait.GetComponent<RectTransform>();
        if (Time.unscaledTime < talkingUntil)
        {
            float bounce = Mathf.Abs(
                Mathf.Sin(Time.unscaledTime * 10f)
            ) * 13f;
            portraitRect.anchoredPosition =
                basePosition + new Vector2(0f, bounce);
        }
        else
        {
            portraitRect.anchoredPosition = basePosition;
        }
    }
}
