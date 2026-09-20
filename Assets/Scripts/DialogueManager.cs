using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysteryGame.Core;
using MysteryGame.Knowledge;
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
        activeDialogueId = data.dialogueId;
        activeNpcId = data.speakerId;
        activeSpeakerName = data.speakerName;
        activePersonalityPrompt = data.personalityPrompt;
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
                    reply => CompleteTypedReply(playerMessage, reply),
                    activePersonalityPrompt
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
        int relationshipDelta = Mathf.Clamp(reply.relationshipDelta, -15, 5);

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

        // Check if player answered Sena's riddle correctly
        bool isSena = (activeSpeakerName != null && activeSpeakerName.ToLowerInvariant().Contains("sena")) ||
                      (activeNpcId != null && activeNpcId.ToLowerInvariant().Contains("sena"));
        if (isSena &&
            SenaInteraction.IsListeningForAnswer &&
            SenaInteraction.IsCorrectRiddleAnswer(playerMessage))
        {
            if (SenaInteraction.Instance != null)
            {
                SenaInteraction.Instance.HandleRiddleSolved();
            }
        }

        chatRequestInProgress = false;
        chatInput.interactable = true;
        sendButton.interactable = true;
        chatInput.ActivateInputField();
    }

    private GeneratedChatReply BuildFallbackReply(string playerMessage)
    {
        string normalized = playerMessage.ToLowerInvariant();
        bool isSena = (activeSpeakerName != null && activeSpeakerName.ToLowerInvariant().Contains("sena")) ||
                      (activeNpcId != null && activeNpcId.ToLowerInvariant().Contains("sena"));
        bool isAlice = (activeSpeakerName != null && activeSpeakerName.ToLowerInvariant().Contains("alice")) ||
                       (activeNpcId != null && activeNpcId.ToLowerInvariant().Contains("alice"));
        string currentScene = GameState.Instance != null ? GameState.Instance.GetCurrentScene() : string.Empty;
        bool isRoom02 = currentScene == "Room02" || (GameState.Instance != null && GameState.Instance.HasFlag("room02_entered"));

        // Route the actual walkthrough through the authored room data, so the
        // offline reply and the AI prompt always agree on which step comes
        // next and on how explicit this NPC is allowed to be.
        NpcKnowledgeContext knowledge =
            AiDialogueGenerator.BuildKnowledgeContext(activeNpcId, playerMessage);
        if (knowledge.HasData)
        {
            string authored =
                NpcKnowledgeContextBuilder.BuildOfflineReply(knowledge);
            if (!string.IsNullOrWhiteSpace(authored))
            {
                return new GeneratedChatReply
                {
                    reply = authored,
                    relationshipDelta =
                        knowledge.AllowedHintLevel == HintLevel.None ? -5 : 2,
                };
            }
        }


        if (isSena)
        {
            return BuildSenaFallbackReply(normalized);
        }

        // ==================== ALICE IN ROOM02 ====================
        if (isAlice && isRoom02)
        {
            if (ContainsAny(
                normalized,
                "ทำยังไง", "ทำอย่างไร", "ทางออก", "เบาะแส", "ต่อไป", "ใบ้", "ช่วย", "เซนะ", "นางฟ้า", "riddle", "คำถาม", "หนังสือ", "จารึก", "ของถวาย", "ทะเบียน", "ชั้น", "โต๊ะ", "ตู้"))
            {
                return BuildAliceRiddleHint();
            }
        }

        // ==================== STANDARD / ALICE IN ROOM01 ====================
        // 1. Hostile / Rude / Dismissive player messages
        if (ContainsAny(
            normalized,
            "อย่ามายุ่ง", "ไม่ยุ่ง", "ไปไกลๆ", "ไปให้พ้น", "หุบปาก", "รำคาญ",
            "เงียบ", "เสือก", "น่ารำคาญ", "เกะกะ", "ออกไป", "ไม่ต้องช่วย",
            "ไม่ต้องพูด", "ช่างหัว", "กวนใจ", "ด่า", "บ้า", "ไปตาย", "shut up", "go away"))
        {
            return new GeneratedChatReply
            {
                reply = "...ถ้าไม่อยากให้ยุ่ง ก็จัดการเองแล้วกันนะ ฉันจะไม่กวนเธออีก",
                relationshipDelta = -10
            };
        }

        // 2. Apology / Kind words
        if (ContainsAny(
            normalized,
            "ขอโทษ", "ขออภัย", "ช่วยหน่อยนะ", "ดีกันนะ", "ไม่ได้ตั้งใจ"))
        {
            return new GeneratedChatReply
            {
                reply = "...เฮ้อ ช่างเถอะ ฉันก็ไม่ได้โกรธขนาดนั้นหรอก มาช่วยกันหาทางออกต่อเถอะ",
                relationshipDelta = 5
            };
        }

        // 3. Greeting / Normal conversation (No hints!)
        if (ContainsAny(
            normalized,
            "สวัสดี", "หวัดดี", "ดีครับ", "ดีค่ะ", "ดีจ้า", "hello", "hi", "hey"))
        {
            return new GeneratedChatReply
            {
                reply = "...สวัสดี มีอะไรหรือเปล่า? ถ้าไม่มีอะไรก็อย่าชวนคุยเรื่อยเปื่อยเลย",
                relationshipDelta = 0
            };
        }

        // 4. Low relationship refusal when asking for hints
        bool isLowRel = GameState.Instance != null &&
                        !string.IsNullOrWhiteSpace(activeNpcId) &&
                        GameState.Instance.GetRelationship(activeNpcId) < 45;

        if (ContainsAny(
            normalized,
            "ทำยังไง", "ทำอย่างไร", "ทางออก", "เบาะแส", "ต่อไป", "ใบ้", "ช่วย"))
        {
            if (isLowRel)
            {
                return new GeneratedChatReply
                {
                    reply = "ก็บอกว่าอย่ามายุ่งไม่ใช่เหรอ... แล้วจะมาถามฉันทำไมล่ะ ลองหาดูเองแล้วกันนะ",
                    relationshipDelta = 0
                };
            }

            return new GeneratedChatReply
            {
                reply = "ลองทบทวนสิ่งที่เราเพิ่งตรวจพบก่อนนะ บางอย่างในห้องอาจเชื่อมโยงกันมากกว่าที่เห็น",
                relationshipDelta = 0
            };
        }

        if (ContainsAny(
            normalized,
            "ขอบคุณ", "ไม่เป็นไร", "เป็นห่วง", "เข้าใจ", "โอเค"))
        {
            return new GeneratedChatReply
            {
                reply = "ขอบคุณนะ อย่างน้อยฉันก็รู้ว่าไม่ได้ต้องรับมือกับเรื่องนี้คนเดียว",
                relationshipDelta = 1
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

    /// <summary>
    /// Offline replies for Sena. She never hints, and what she says depends on
    /// which stage of the gate trial the player has reached.
    /// </summary>
    private static GeneratedChatReply BuildSenaFallbackReply(string normalized)
    {
        GameState state = GameState.Instance;
        bool offeringGiven =
            state != null && state.HasFlag(SenaInteraction.OfferingGivenFlag);
        bool carryingOffering =
            state != null && state.HasItem(SenaInteraction.OfferingItemId);

        bool askingForHelp = ContainsAny(
            normalized,
            "ช่วย", "ใบ้", "ทำยังไง", "hint", "help", "ขอคำใบ้", "บอกหน่อย", "แก้ยังไง");
        bool greeting = ContainsAny(
            normalized,
            "สวัสดี", "หวัดดี", "ดีครับ", "ดีค่ะ", "hi", "hello", "hey");
        bool hostile = ContainsAny(
            normalized,
            "อย่ามายุ่ง", "ไม่ยุ่ง", "ไปไกลๆ", "หุบปาก", "รำคาญ", "เงียบ", "เสือก", "บ้า", "shut up");

        if (hostile)
        {
            return new GeneratedChatReply
            {
                reply = "มนุษย์ชั้นต่ำผู้ไร้มารยาท... ข้าเห็นผู้เช่นเจ้ามาแล้วนับร้อย และทุกผู้ล้วนผุพังอยู่หน้าประตูนี้ทั้งสิ้น",
                relationshipDelta = -10
            };
        }

        if (askingForHelp)
        {
            return new GeneratedChatReply
            {
                reply = "คำใบ้ฤๅ? เจ้าสิ้นไร้ปัญญาถึงเพียงนั้นเชียวหรือ ข้ามิใช่พี่เลี้ยงของเจ้าดอก จงใช้ตาที่ติดหัวมานั่นเสาะหาเอาเองเถิด",
                relationshipDelta = -5
            };
        }

        // ---------------- Stage 1: she has not posed the riddle yet
        if (!offeringGiven)
        {
            if (carryingOffering)
            {
                return new GeneratedChatReply
                {
                    reply = "เจ้าถือจารึกนั้นมาแล้วฤๅ... งั้นก็จงยื่นมันให้ข้าเสียสิ (กด E คุยกับข้าอีกครั้ง)",
                    relationshipDelta = 1
                };
            }

            if (greeting)
            {
                return new GeneratedChatReply
                {
                    reply = "อย่ามาตีสนิทกับข้า มนุษย์ เจ้ายังมาด้วยมืออันว่างเปล่าอยู่เลย",
                    relationshipDelta = 0
                };
            }

            return new GeneratedChatReply
            {
                reply = "ข้าจะมิเอ่ยปริศนาแก่ผู้ที่มิมีของถวาย จงเสาะหา 'จารึกที่ยังเขียนมิจบ' ในหอสมุดแห่งนี้มาให้ข้าเสียก่อนเถิด",
                relationshipDelta = 0
            };
        }

        // ---------------- Stage 2: the riddle is on the table
        if (SenaInteraction.IsCorrectRiddleAnswer(normalized))
        {
            return new GeneratedChatReply
            {
                reply = "...หึ เจ้าตอบถูกจนได้ฤๅ น่าประทับใจนักสำหรับมนุษย์ตัวจ้อยเช่นเจ้า ข้ายอมรับในปัญญาของเจ้าในครานี้",
                relationshipDelta = 5
            };
        }

        if (greeting)
        {
            return new GeneratedChatReply
            {
                reply = "อย่ามาเสียเวลากับถ้อยคำไร้สาระ จงตอบปริศนาของข้ามาเถิด",
                relationshipDelta = 0
            };
        }

        return new GeneratedChatReply
        {
            reply = "ปริศนาของข้าแจ่มชัดอยู่แล้ว: 'สิ่งที่วิ่งนำหน้าเจ้าเสมอ ทว่ามิเคยมาถึง เจ้าเฝ้ารอมันทั้งชีวิต แต่ครานที่มันมาถึง ชื่อของมันก็เปลี่ยนไปเสียแล้ว'... จงตอบมา หรือจะยอมแพ้",
            relationshipDelta = 0
        };
    }

    /// <summary>
    /// Alice walks the player through Room02 one gate at a time, and only
    /// starts unpacking the riddle itself after Sena has actually posed it.
    /// The AI prompt reads the same flags, so both paths agree.
    /// </summary>
    private static GeneratedChatReply BuildAliceRiddleHint()
    {
        GameState state = GameState.Instance;
        bool wantsOffering =
            state != null && state.HasFlag(SenaInteraction.WantsOfferingFlag);
        bool offeringGiven =
            state != null && state.HasFlag(SenaInteraction.OfferingGivenFlag);
        bool readLedger = state != null && state.HasFlag("read_ledger");
        bool hasTome =
            state != null && state.HasItem(SenaInteraction.OfferingItemId);

        if (!wantsOffering)
        {
            return new GeneratedChatReply
            {
                reply = "...ยังไม่ได้คุยกับนางฟ้าที่ขวางประตูอยู่เลยนี่ ไปฟังดูก่อนสิว่านางต้องการอะไร",
                relationshipDelta = 1
            };
        }

        if (!offeringGiven)
        {
            if (!readLedger)
            {
                return new GeneratedChatReply
                {
                    reply = "หนังสือที่ยังเขียนไม่จบ... ในหอนี้มีหนังสือเป็นพัน จะไล่หาทีละเล่มคงไม่ไหว ลองดูโต๊ะอ่านหนังสือฝั่งขวาสิ มีสมุดทะเบียนเปิดค้างอยู่",
                    relationshipDelta = 2
                };
            }

            if (!hasTome)
            {
                return new GeneratedChatReply
                {
                    reply = "ทะเบียนบอกชั้นไว้แล้วนี่ — ชั้นล่างสุดของตู้ฝั่งซ้าย ไปหยิบมาเลย",
                    relationshipDelta = 2
                };
            }

            return new GeneratedChatReply
            {
                reply = "ได้มาแล้วนี่ ...เอาไปให้นางเถอะ ฉันอยากรู้แล้วว่านางจะถามอะไร",
                relationshipDelta = 2
            };
        }

        int cluesFound = CountRoom02CluesFound();

        if (cluesFound == 0)
        {
            return new GeneratedChatReply
            {
                reply = "คำถามของนาง... สิ่งที่อยู่ข้างหน้าเราเสมอ แต่พอข้ามผ่านเที่ยงคืนไป มันก็กลายเป็น 'วันนี้' เสียแล้ว... ลองไปสำรวจนาฬิกาโบราณ ชั้นหนังสือ และแท่นไฟดูก่อนสิ",
                relationshipDelta = 2
            };
        }

        if (cluesFound < 3)
        {
            return new GeneratedChatReply
            {
                reply = "...มันไม่ใช่สิ่งของหรอกนะ มันเป็น 'วัน' ที่เราเฝ้ารอ แต่ไม่ว่าจะรอนานแค่ไหน ก็ไม่มีวันไปถึง เพราะพอถึงตอนเที่ยงคืน เราก็เรียกมันด้วยชื่ออื่นไปแล้ว",
                relationshipDelta = 2
            };
        }

        return new GeneratedChatReply
        {
            reply = "เฮ้อ... เบาะแสครบหมดแล้วนี่ คำตอบคือ 'พรุ่งนี้' (Tomorrow) ไปพิมพ์ตอบนางได้แล้ว",
            relationshipDelta = 1
        };
    }

    /// <summary>
    /// How many of Room02's three written clues about the riddle the player
    /// has read: the clock, the poem on the middle shelf, and the brazier.
    /// </summary>
    public static int CountRoom02CluesFound()
    {
        GameState state = GameState.Instance;
        if (state == null)
        {
            return 0;
        }

        int found = 0;
        if (state.HasFlag("inspected_clock"))
        {
            found++;
        }
        if (state.HasFlag("inspected_bookshelf_r2"))
        {
            found++;
        }
        if (state.HasFlag("inspected_altar"))
        {
            found++;
        }

        return found;
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
        activePersonalityPrompt = string.Empty;
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
