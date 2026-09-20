using System.Collections.Generic;
using UnityEngine;

public class ItemPopupUI : MonoBehaviour
{
    private static ItemPopupUI _instance;

    public static ItemPopupUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<ItemPopupUI>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("ItemPopupUI");
                    _instance = go.AddComponent<ItemPopupUI>();
                }
            }
            return _instance;
        }
    }

    public struct ItemInfo
    {
        public string itemId;
        public string displayName;
        public string description;
    }

    private static readonly Queue<ItemInfo> pendingItems = new Queue<ItemInfo>();

    private bool isShowing = false;
    private ItemInfo currentItem;
    private Sprite[] currentFrames;
    private float animationFps = 20f;
    private float popupOpenedTime;

    private Texture2D dimTexture;
    private Texture2D panelTexture;
    private Texture2D buttonTexture;
    private GUIStyle titleStyle;
    private GUIStyle nameStyle;
    private GUIStyle descStyle;
    private GUIStyle buttonStyle;
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
    }

    public static void ShowItem(string itemId, string displayName = null, string description = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        bool hasExplicitName = !string.IsNullOrWhiteSpace(displayName);

        // Fill default info if not provided
        if (string.IsNullOrWhiteSpace(displayName))
        {
            if (itemId.Equals("key", System.StringComparison.OrdinalIgnoreCase))
            {
                displayName = "กุญแจทองเหลืองโบราณ (Brass Key)";
                description = "กุญแจทองเหลืองเก่าแก่ที่ซ่อนอยู่ในลิ้นชัก สามารถใช้ไขประตูทางออกของห้องนี้ได้";
            }
            else if (itemId.Equals("paper", System.StringComparison.OrdinalIgnoreCase) ||
                     itemId.Equals("note", System.StringComparison.OrdinalIgnoreCase))
            {
                displayName = "บันทึกของผู้รอดชีวิตคนก่อน (Survivor's Note)";
                description = "\"วันที่เท่าไหร่แล้วก็ไม่รู้... ฉันติดอยู่ในห้องบ้าๆ นี่มานานเกินไป ความเครียดจะบดขยี้สติฉันอยู่แล้ว!\nฉันพยายามทุกวิถีทางเพื่อเปิดลิ้นชักนั่น... ในที่สุดหลังจากการลองสุ่ม Combination ตัวเลข 4 หลักนับร้อยครั้ง... ฉันถอดรหัสมันได้แล้ว!\nรหัสเปิดลิ้นชักคือ  [ 4 5 9 2 ]\n...ใครก็ตามที่มาพบโน้ตนี้ รีบเอากุญแจข้างในแล้วหนีออกไปซะ!\"";
            }
            else
            {
                displayName = "ไอเท็ม: " + itemId;
                description = "คุณได้รับ " + itemId + " เก็บไว้ในช่องเก็บของแล้ว";
            }
        }

        ItemInfo info = new ItemInfo
        {
            itemId = itemId,
            displayName = displayName,
            description = description ?? string.Empty
        };

        // An interaction that hands out an item queues this twice: once from
        // GameState.AddItem, which only knows the id, and once from the
        // interaction, which knows the written-up name. Show one card, and let
        // whichever call carries the real name win.
        bool alreadyQueued = false;
        foreach (ItemInfo queued in pendingItems)
        {
            if (string.Equals(
                    queued.itemId, info.itemId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                alreadyQueued = true;
                break;
            }
        }

        if (alreadyQueued)
        {
            if (hasExplicitName)
            {
                ItemInfo[] queuedItems = pendingItems.ToArray();
                pendingItems.Clear();
                foreach (ItemInfo other in queuedItems)
                {
                    pendingItems.Enqueue(
                        string.Equals(
                            other.itemId, info.itemId,
                            System.StringComparison.OrdinalIgnoreCase)
                            ? info
                            : other
                    );
                }
            }

            _ = Instance;
            return;
        }

        pendingItems.Enqueue(info);
        // Ensure instance exists
        _ = Instance;
    }

    private void Update()
    {
        // If not showing and have pending items, wait until dialogue closes
        if (!isShowing && pendingItems.Count > 0)
        {
            if (!DialogueManager.IsDialogueOpen && !AiSettingsPanel.IsOpen && !KeypadLockUI.IsOpen)
            {
                DisplayNextItem();
            }
        }

        // Close on keyboard input
        if (isShowing)
        {
            if (Time.unscaledTime - popupOpenedTime > 0.25f &&
                (Input.GetKeyDown(KeyCode.Space) ||
                 Input.GetKeyDown(KeyCode.Return) ||
                 Input.GetKeyDown(KeyCode.KeypadEnter) ||
                 Input.GetKeyDown(KeyCode.Escape) ||
                 Input.GetKeyDown(KeyCode.E)))
            {
                ClosePopup();
            }
        }
    }

    private void DisplayNextItem()
    {
        if (pendingItems.Count == 0)
        {
            return;
        }

        currentItem = pendingItems.Dequeue();
        currentFrames = ItemAssetManager.GetItemFrames(currentItem.itemId);
        isShowing = true;
        popupOpenedTime = Time.unscaledTime;
    }

    private void ClosePopup()
    {
        isShowing = false;
        currentFrames = null;
    }

    private void OnGUI()
    {
        if (!isShowing)
        {
            return;
        }

        EnsureStyles();

        // 1. Draw dimmed full-screen background
        GUI.DrawTexture(
            new Rect(0f, 0f, Screen.width, Screen.height),
            dimTexture,
            ScaleMode.StretchToFill
        );

        bool isNote = currentItem.itemId != null &&
                      (currentItem.itemId.IndexOf("paper", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       currentItem.itemId.IndexOf("note", System.StringComparison.OrdinalIgnoreCase) >= 0);

        // 2. Center modal panel
        float panelWidth = Mathf.Min(580f, Screen.width - 30f);
        float panelHeight = isNote ? 520f : 460f;
        float panelX = (Screen.width - panelWidth) * 0.5f;
        float panelY = (Screen.height - panelHeight) * 0.5f;
        Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);

        // Draw panel background
        GUI.DrawTexture(panelRect, panelTexture, ScaleMode.StretchToFill);

        // Gold border outline
        DrawRectOutline(panelRect, new Color(0.85f, 0.68f, 0.28f, 0.9f), 2);

        // Header Title
        Rect headerRect = new Rect(panelX + 20f, panelY + 18f, panelWidth - 40f, 34f);
        string headerTitle = isNote ? "📜 บันทึกที่ค้นพบ" : "✨ ค้นพบไอเท็มใหม่!";
        GUI.Label(headerRect, headerTitle, titleStyle);

        // 3. Center Sprite (Spinning Key or Antique Paper)
        float spriteSize = isNote ? 140f : 130f;
        Rect spriteRect = new Rect(
            (Screen.width - spriteSize) * 0.5f,
            panelY + 62f,
            spriteSize,
            spriteSize
        );

        // Subtle glow / circular backplate for item
        DrawItemBackplate(spriteRect);

        // Draw animated frame
        if (currentFrames != null && currentFrames.Length > 0)
        {
            int frameIndex = Mathf.FloorToInt(
                (Time.unscaledTime * animationFps) % currentFrames.Length
            );
            Sprite activeSprite = currentFrames[frameIndex];
            if (activeSprite != null && activeSprite.texture != null)
            {
                // Calculate UV rect for this sprite slice
                Rect tr = activeSprite.textureRect;
                Rect uvRect = new Rect(
                    tr.x / activeSprite.texture.width,
                    tr.y / activeSprite.texture.height,
                    tr.width / activeSprite.texture.width,
                    tr.height / activeSprite.texture.height
                );

                GUI.DrawTextureWithTexCoords(spriteRect, activeSprite.texture, uvRect, true);
            }
        }
        else
        {
            // Fallback placeholder box
            GUI.Box(spriteRect, currentItem.itemId, hintStyle);
        }

        // 4. Item Name
        Rect nameRect = new Rect(panelX + 20f, panelY + 210f, panelWidth - 40f, 36f);
        GUI.Label(nameRect, currentItem.displayName, nameStyle);

        // 5. Item Description
        float descH = isNote ? 160f : 90f;
        Rect descRect = new Rect(panelX + 25f, panelY + 250f, panelWidth - 50f, descH);
        GUI.Label(descRect, currentItem.description, descStyle);

        // 6. Confirm / Continue Button
        float btnWidth = 220f;
        float btnHeight = 44f;
        Rect btnRect = new Rect(
            (Screen.width - btnWidth) * 0.5f,
            panelY + panelHeight - 70f,
            btnWidth,
            btnHeight
        );

        if (GUI.Button(btnRect, "ตกลง  [ Space ]", buttonStyle))
        {
            ClosePopup();
        }

        // Small dismiss hint
        Rect hintRect = new Rect(panelX, panelY + panelHeight - 24f, panelWidth, 20f);
        GUI.Label(hintRect, "คลิกหรือกด Space / Enter เพื่อดำเนินการต่อ", hintStyle);
    }

    private void DrawItemBackplate(Rect spriteRect)
    {
        float padding = 14f;
        Rect backRect = new Rect(
            spriteRect.x - padding,
            spriteRect.y - padding,
            spriteRect.width + padding * 2,
            spriteRect.height + padding * 2
        );

        // Subtle dark circular plate
        Color oldColor = GUI.color;
        GUI.color = new Color(0.08f, 0.12f, 0.18f, 0.85f);
        GUI.DrawTexture(backRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = oldColor;

        DrawRectOutline(backRect, new Color(0.35f, 0.5f, 0.65f, 0.5f), 1);
    }

    private static void DrawRectOutline(Rect rect, Color color, int thickness)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        // Top
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        // Bottom
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        // Left
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        // Right
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        dimTexture = CreateColorTexture(new Color(0.008f, 0.015f, 0.025f, 0.82f));
        panelTexture = CreateColorTexture(new Color(0.035f, 0.055f, 0.085f, 0.98f));
        buttonTexture = CreateColorTexture(new Color(0.78f, 0.58f, 0.22f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 19,
            fontStyle = FontStyle.Bold
        };
        nameStyle.normal.textColor = new Color(0.95f, 0.97f, 1f);

        descStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            wordWrap = true
        };
        descStyle.normal.textColor = new Color(0.82f, 0.88f, 0.95f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold
        };
        buttonStyle.normal.background = buttonTexture;
        buttonStyle.normal.textColor = new Color(0.04f, 0.06f, 0.08f);

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Italic
        };
        hintStyle.normal.textColor = new Color(0.6f, 0.68f, 0.78f);
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
        if (buttonTexture != null) Destroy(buttonTexture);
    }
}
