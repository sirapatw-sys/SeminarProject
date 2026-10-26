using MysteryGame.Core;
using MysteryGame.Knowledge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A reusable, scrollable picker showing only verified current-room evidence.</summary>
public class EvidencePickerUI : MonoBehaviour
{
    private DialogueManager owner;
    private TMP_FontAsset font;
    private Button toggle;
    private TMP_Text toggleLabel;
    private GameObject panel;
    private Transform rows;
    private TMP_Text status;

    public bool IsOpen { get { return panel != null && panel.activeSelf; } }

    public void Configure(DialogueManager manager, Transform composer, Transform dialoguePanel, TMP_FontAsset textFont)
    {
        owner = manager;
        font = textFont;
        toggle = Button(composer, "EvidenceButton", "หลักฐาน", out toggleLabel);
        var rect = (RectTransform)toggle.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-120f, 0f);
        rect.sizeDelta = new Vector2(148f, -12f);
        toggle.onClick.AddListener(Open);

        panel = Box(dialoguePanel, "EvidencePicker");
        rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 18f);
        rect.sizeDelta = new Vector2(660f, 286f);
        var title = Text(panel.transform, "Title", "นำหลักฐานให้ดู", 25f);
        Stretch((RectTransform)title.transform, new Vector2(20f, -52f), new Vector2(-100f, -8f), true);
        TMP_Text closeText;
        var close = Button(panel.transform, "CloseEvidencePicker", "×", out closeText);
        rect = (RectTransform)close.transform;
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-12f, -10f);
        rect.sizeDelta = new Vector2(52f, 38f);
        close.onClick.AddListener(Close);

        status = Text(panel.transform, "Status", "", 19f);
        rect = (RectTransform)status.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = new Vector2(20f, 10f);
        rect.offsetMax = new Vector2(-20f, 54f);
        status.enableWordWrapping = true;

        var viewport = Box(panel.transform, "EvidenceViewport");
        Stretch((RectTransform)viewport.transform, new Vector2(16f, 62f), new Vector2(-16f, -60f));
        viewport.AddComponent<RectMask2D>();
        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        var content = new GameObject("EvidenceRows", typeof(RectTransform));
        content.layer = panel.layer;
        content.transform.SetParent(viewport.transform, false);
        rows = content.transform;
        rect = (RectTransform)rows;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = Vector2.zero;
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = rect;
        scroll.viewport = (RectTransform)viewport.transform;
        panel.SetActive(false);
        RefreshAvailability();
    }

    public void RefreshAvailability()
    {
        if (toggle == null) return;
        toggle.interactable = owner != null && owner.CanShareEvidence;
        int count = EvidenceSharing.Available(GameState.Instance, owner != null ? owner.ActiveNpcId : null).Count;
        toggleLabel.text = count > 0 ? "หลักฐาน (" + count + ")" : "หลักฐาน";
        if (!toggle.interactable) Close();
    }

    public void Open()
    {
        if (owner == null || !owner.CanShareEvidence) return;
        // Remove obsolete rows immediately from layout; Destroy is deferred in Play Mode.
        for (int i = rows.childCount - 1; i >= 0; i--)
        {
            var old = rows.GetChild(i);
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }
        var state = GameState.Instance;
        var room = KnowledgeLibrary.GetRoom(state.GetCurrentScene());
        var npc = KnowledgeLibrary.GetNpc(owner.ActiveNpcId);
        var available = EvidenceSharing.Available(state, owner.ActiveNpcId);
        status.text = available.Count == 0
            ? "ยังไม่มีหลักฐานให้ดู ลองสำรวจห้องก่อนนะ"
            : "เลือกสิ่งที่พบแล้ว • ไม่ใช้ไอเท็มหมด • คุยซ้ำไม่เพิ่มคะแนน";
        int session = owner.ConversationVersion;
        string npcId = owner.ActiveNpcId;
        foreach (var fact in available)
        {
            string factId = fact.factId;
            bool shared = EvidenceSharing.HasBeenShared(state, npc.npcId, room.roomId, factId);
            TMP_Text label;
            var row = Button(rows, "Evidence_" + factId,
                (shared ? "[คุยแล้ว] " : "") + fact.EvidenceTitle, out label);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 23f;
            label.enableWordWrapping = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;
            row.onClick.AddListener(() =>
            {
                if (!owner.IsCurrentConversation(session, npcId)) { Close(); return; }
                if (owner.TryShareEvidence(factId)) Close();
                else status.text = "หลักฐานนี้ใช้ไม่ได้ในสถานะปัจจุบัน กรุณาเปิดรายการใหม่";
            });
        }
        panel.SetActive(true);
        panel.transform.SetAsLastSibling();
    }

    public void Close() { if (panel != null) panel.SetActive(false); }

    private GameObject Box(Transform parent, string objectName)
    {
        var box = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.layer = parent.gameObject.layer;
        box.transform.SetParent(parent, false);
        box.GetComponent<Image>().color = new Color(0.035f, 0.065f, 0.105f, 0.99f);
        return box;
    }

    private TMP_Text Text(Transform parent, string objectName, string value, float size)
    {
        var obj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        var text = obj.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    private Button Button(Transform parent, string objectName, string value, out TMP_Text label)
    {
        var obj = Box(parent, objectName);
        obj.GetComponent<Image>().color = new Color(0.13f, 0.31f, 0.39f);
        var button = obj.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.78f, 0.95f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f);
        button.colors = colors;
        label = Text(obj.transform, "Label", value, 22f);
        label.alignment = TextAlignmentOptions.Center;
        Stretch((RectTransform)label.transform, new Vector2(10f, 3f), new Vector2(-10f, -3f));
        return button;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, bool topOnly = false)
    {
        rect.anchorMin = topOnly ? new Vector2(0f, 1f) : Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
}
