using UnityEngine;
using TMPro;
using UnityEngine.UI;
using MysteryGame.Core;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

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

    // =========================================================
    // Dialogue Data
    // =========================================================

    private string[] dialogueLines;
    private int currentLine = 0;

    // กำลังแสดงคำตอบหลังจากเลือก Choice
    private bool showingChoiceResponse = false;

    // Dialogue นี้ต้องเปิด Choice ตอนจบหรือไม่
    private bool showChoicesAfterDialogue = true;

    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        Instance = this;
    }

    // =========================================================
    // Start Dialogue
    // =========================================================

    public void StartDialogue(
        string characterName,
        string[] lines,
        bool showChoicesAfterDialogue = true)
    {
        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("Dialogue has no lines.");
            return;
        }

        dialoguePanel.SetActive(true);
        choicePanel.SetActive(false);
        continueButton.gameObject.SetActive(true);

        showingChoiceResponse = false;

        // จำว่า Dialogue นี้ต้องมี Choice หรือไม่
        this.showChoicesAfterDialogue = showChoicesAfterDialogue;

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

        // ---------------------------------------------
        // ถ้าไม่ต้องมี Choice
        // เช่น Painting / Desk / Object
        // ---------------------------------------------
        if (!showChoicesAfterDialogue)
        {
            HideDialogue();
            return;
        }

        // ---------------------------------------------
        // ถ้าต้องมี Choice
        // เช่น Alice
        // ---------------------------------------------
        ShowChoices(
            "ได้ ฉันจะช่วยหาให้",
            "ทำไมฉันต้องช่วยเธอ?",
            "ฉันไม่ว่าง"
        );
    }

    // =========================================================
    // Show Choices
    // =========================================================

    public void ShowChoices(
        string option1,
        string option2,
        string option3)
    {
        dialoguePanel.SetActive(true);
        choicePanel.SetActive(true);

        continueButton.gameObject.SetActive(false);

        SetButtonText(choiceButton1, option1);
        SetButtonText(choiceButton2, option2);
        SetButtonText(choiceButton3, option3);
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

        string npcResponse;
        int relationshipChange;

        switch (choiceIndex)
        {
            case 0:

                npcResponse =
                    "ขอบคุณนะ ฉันคิดว่าเราน่าจะหาเจอด้วยกัน";

                relationshipChange = 5;

                break;

            case 1:

                npcResponse =
                    "ก็ได้... ฉันจะหาต่อเอง";

                relationshipChange = 1;

                break;

            case 2:

                npcResponse =
                    "เข้าใจแล้ว... ฉันจะไม่รบกวนเธอ";

                relationshipChange = -3;

                break;

            default:

                npcResponse =
                    "ไม่เป็นไร...";

                relationshipChange = 0;

                break;
        }

        // แสดงคำตอบ NPC
        nameText.text = "Alice";
        dialogueText.text = npcResponse;

        // ---------------------------------------------
        // Relationship
        // ---------------------------------------------

        if (RelationshipManager.Instance != null)
        {
            RelationshipManager.Instance.ChangeRelationship(
                "Alice",
                relationshipChange
            );
        }
        else
        {
            Debug.LogError(
                "RelationshipManager.Instance is null."
            );
        }

        // ---------------------------------------------
        // Player History
        // ---------------------------------------------

        if (GameState.Instance != null)
        {
            GameState.Instance.AddHistory(
                "Selected dialogue choice " +
                (choiceIndex + 1)
            );

            GameState.Instance.AddHistory(
                "Relationship with Alice changed by " +
                relationshipChange
            );
        }

        Debug.Log(
            "Choice: " +
            (choiceIndex + 1) +
            " | Relationship Change: " +
            relationshipChange
        );
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
        showChoicesAfterDialogue = true;
    }
}