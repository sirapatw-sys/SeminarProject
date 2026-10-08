using System.Collections.Generic;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;

/// <summary>
/// The player's notebook: every object description and item card they have
/// read, kept in GameState and shown with J. A clue skipped past too quickly,
/// or one inside a book the player already carried off, can be read again.
/// </summary>
public class JournalUI : MonoBehaviour
{
    public const KeyCode ToggleKey = KeyCode.J;

    public static bool IsOpen { get; private set; }

    private const float PopupMagnify = 1.25f;
    private const float ToastSeconds = 3.5f;

    private static JournalUI instance;
    private static float toastUntil;

    private Vector2 scroll;
    private Texture2D dimTexture;
    private Texture2D panelTexture;
    private Texture2D entryTexture;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle roomStyle;
    private GUIStyle entryTitleStyle;
    private GUIStyle entryTextStyle;
    private GUIStyle toastStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("JournalUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<JournalUI>();
    }

    /// <summary>Shows the "written down" toast after a new entry.</summary>
    public static void NotifyNewEntry()
    {
        toastUntil = Time.unscaledTime + ToastSeconds;
    }

    private void Update()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(ToggleKey) || Input.GetKeyDown(KeyCode.Escape))
            {
                IsOpen = false;
            }

            return;
        }

        if (Input.GetKeyDown(ToggleKey) && !InputGate.IsBlocked && !ItemPopupUI.IsBusy)
        {
            IsOpen = true;
            scroll = Vector2.zero;
            toastUntil = 0f;
            SfxPlayer.Play(SfxPlayer.Cue.Interact);
        }
    }

    private void OnGUI()
    {
        if (TitleMenu.IsOpen || IntroSequence.IsPlaying)
        {
            return;
        }

        UiScale.Apply(PopupMagnify, 560f);
        EnsureStyles();

        if (!IsOpen)
        {
            if (Time.unscaledTime < toastUntil && !ItemPopupUI.IsBusy)
            {
                GUI.Box(new Rect((UiScale.Width - 440f) * 0.5f, 18f, 440f, 40f),
                        "จดลงสมุดบันทึกแล้ว  •  กด J เพื่ออ่านอีกครั้ง", toastStyle);
            }

            return;
        }

        GUI.DrawTexture(new Rect(0f, 0f, UiScale.Width, UiScale.Height), dimTexture);

        float width = Mathf.Min(760f, UiScale.Width - 30f);
        float height = Mathf.Min(560f, UiScale.Height - 30f);
        Rect panel = new Rect((UiScale.Width - width) * 0.5f, (UiScale.Height - height) * 0.5f, width, height);
        GUI.DrawTexture(panel, panelTexture);
        DrawOutline(panel, new Color(0.85f, 0.68f, 0.28f, 0.9f));

        GUI.Label(new Rect(panel.x, panel.y + 14f, width, 32f), "สมุดบันทึก", titleStyle);
        GUI.Label(new Rect(panel.x, panel.y + 46f, width, 22f),
                  "สิ่งที่อ่านเจอระหว่างทาง  •  กด J หรือ Esc เพื่อปิด", subtitleStyle);

        Rect view = new Rect(panel.x + 18f, panel.y + 78f, width - 36f, height - 96f);
        float innerWidth = view.width - 20f;
        List<Row> rows = BuildRows(innerWidth);

        float contentHeight = 0f;
        foreach (Row row in rows)
        {
            contentHeight += row.Height;
        }

        scroll = GUI.BeginScrollView(view, scroll, new Rect(0f, 0f, innerWidth, Mathf.Max(contentHeight, view.height)));
        float y = 0f;
        foreach (Row row in rows)
        {
            row.Draw(this, new Rect(0f, y, innerWidth, row.Height));
            y += row.Height;
        }
        GUI.EndScrollView();
    }

    // ------------------------------------------------------------ layout

    private struct Row
    {
        public string Room;
        public JournalEntry Entry;
        public float TitleHeight;
        public float Height;

        public void Draw(JournalUI ui, Rect rect)
        {
            if (Entry == null)
            {
                GUI.Label(new Rect(rect.x, rect.y + 6f, rect.width, rect.height - 6f), Room, ui.roomStyle);
                return;
            }

            Rect card = new Rect(rect.x, rect.y, rect.width, rect.height - 10f);
            GUI.DrawTexture(card, ui.entryTexture);
            GUI.Label(new Rect(card.x + 14f, card.y + 8f, card.width - 28f, TitleHeight), Entry.Title, ui.entryTitleStyle);
            GUI.Label(new Rect(card.x + 14f, card.y + 10f + TitleHeight, card.width - 28f, card.height - TitleHeight - 16f),
                      Entry.Text, ui.entryTextStyle);
        }
    }

    private List<Row> BuildRows(float width)
    {
        List<Row> rows = new List<Row>();
        GameState state = GameState.Instance;
        IReadOnlyList<JournalEntry> journal = state != null ? state.GetJournal() : null;
        if (journal == null || journal.Count == 0)
        {
            rows.Add(new Row { Room = "ยังไม่มีอะไรในสมุด ลองสำรวจสิ่งของในห้องดูก่อน", Height = 40f });
            return rows;
        }

        // The room the player is in first, then the rooms before it; newest
        // entries at the top of each room.
        List<string> rooms = new List<string>();
        string current = state.GetCurrentScene();
        rooms.Add(current ?? string.Empty);
        for (int i = journal.Count - 1; i >= 0; i--)
        {
            if (!rooms.Contains(journal[i].RoomId ?? string.Empty))
            {
                rooms.Add(journal[i].RoomId ?? string.Empty);
            }
        }

        foreach (string room in rooms)
        {
            bool headerAdded = false;
            for (int i = journal.Count - 1; i >= 0; i--)
            {
                JournalEntry entry = journal[i];
                if ((entry.RoomId ?? string.Empty) != room)
                {
                    continue;
                }

                if (!headerAdded)
                {
                    rows.Add(new Row { Room = RoomTitle(room, room == current), Height = 34f });
                    headerAdded = true;
                }

                float textWidth = width - 28f;
                float titleHeight = entryTitleStyle.CalcHeight(new GUIContent(entry.Title), textWidth);
                float textHeight = entryTextStyle.CalcHeight(new GUIContent(entry.Text), textWidth);
                rows.Add(new Row
                {
                    Entry = entry,
                    TitleHeight = titleHeight,
                    Height = titleHeight + textHeight + 30f,
                });
            }
        }

        return rows;
    }

    private static string RoomTitle(string roomId, bool isCurrent)
    {
        RoomKnowledgeData room = KnowledgeLibrary.GetRoom(roomId);
        string name = room != null && !string.IsNullOrWhiteSpace(room.roomName) ? room.roomName : roomId;
        return isCurrent ? name + "  (ห้องนี้)" : name;
    }

    // ------------------------------------------------------------ styles

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        dimTexture = MakeTexture(new Color(0.008f, 0.015f, 0.025f, 0.82f));
        panelTexture = MakeTexture(new Color(0.035f, 0.055f, 0.085f, 0.98f));
        entryTexture = MakeTexture(new Color(0.07f, 0.1f, 0.14f, 1f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
        };
        titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            fontStyle = FontStyle.Italic,
        };
        subtitleStyle.normal.textColor = new Color(0.6f, 0.68f, 0.78f);

        roomStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
        };
        roomStyle.normal.textColor = new Color(0.72f, 0.82f, 0.95f);

        entryTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
        };
        entryTitleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

        entryTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true,
        };
        entryTextStyle.normal.textColor = new Color(0.86f, 0.9f, 0.96f);

        toastStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
        };
        toastStyle.normal.background = entryTexture;
        toastStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
    }

    private static void DrawOutline(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsOpen = false;
        }

        if (dimTexture != null) Destroy(dimTexture);
        if (panelTexture != null) Destroy(panelTexture);
        if (entryTexture != null) Destroy(entryTexture);
    }
}
