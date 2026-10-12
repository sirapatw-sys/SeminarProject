using UnityEngine;

namespace MysteryGame.Companions
{
    /// <summary>Non-modal controls: never pauses play or replaces E/dialogue input.</summary>
    public class CompanionHud : MonoBehaviour
    {
        private NpcCompanionController actor;
        private GUIStyle panel, title, label, button, bubble, bluePad, goldPad, donePad;
        private readonly System.Collections.Generic.List<Texture2D> textures = new System.Collections.Generic.List<Texture2D>();
        private void Awake() { actor = GetComponent<NpcCompanionController>(); }
        private Texture2D Background(Color color, int radius)
        { var texture = ModalGui.Rounded(color, radius); textures.Add(texture); return texture; }
        private GUIStyle Style(Color color, int size, TextAnchor alignment, int radius = 12)
        {
            return new GUIStyle(GUI.skin.box) { fontSize = size, alignment = alignment, wordWrap = true,
                padding = new RectOffset(12, 12, 6, 6), border = new RectOffset(radius, radius, radius, radius),
                normal = { background = Background(color, radius), textColor = new Color(0.95f, 0.97f, 1f) } };
        }
        private void Styles()
        {
            if (panel != null) return;
            panel = Style(new Color(0.025f, 0.045f, 0.065f, 0.95f), 18, TextAnchor.UpperLeft);
            title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.47f, 0.9f, 0.91f) } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = Color.white } };
            button = Style(new Color(0.10f, 0.24f, 0.29f, 1f), 17, TextAnchor.MiddleCenter);
            bubble = Style(new Color(0.03f, 0.08f, 0.12f, 0.96f), 18, TextAnchor.MiddleCenter);
            bluePad = Style(new Color(0.12f, 0.65f, 0.77f, 0.8f), 14, TextAnchor.MiddleCenter, 22);
            goldPad = Style(new Color(0.68f, 0.46f, 0.12f, 0.85f), 14, TextAnchor.MiddleCenter, 22);
            donePad = Style(new Color(0.12f, 0.48f, 0.29f, 0.75f), 14, TextAnchor.MiddleCenter, 22);
        }
        private void OnGUI()
        {
            if (actor == null || actor.Scenario == null || !actor.isActiveAndEnabled || InputGate.IsBlocked || ItemPopupUI.IsBusy) return;
            Matrix4x4 previous = GUI.matrix; Color previousColor = GUI.color; bool previousEnabled = GUI.enabled; int previousDepth = GUI.depth;
            UiScale.Apply(); GUI.depth = -2; Styles();
            var scenario = actor.Scenario;
            Rect card = new Rect(Mathf.Max(8f, UiScale.Width - 402f), 82f, Mathf.Min(380f, UiScale.Width - 16f), 236f);
            GUI.Box(card, GUIContent.none, panel);
            GUI.Label(new Rect(card.x + 14, card.y + 10, card.width - 28, 30), scenario.displayName + " • เพื่อนร่วมสำรวจ", title);
            string status = actor.Mode == CompanionMode.Following ? "เดินไปด้วยกัน" : actor.Mode == CompanionMode.GoingToHelp ? "กำลังเดินไปช่วย" :
                actor.Mode == CompanionMode.Helping ? "จับกลไกอยู่ • ยืนบนจุดสีทองด้วยกัน" : "รอคำชวน • ไม่จำเป็นต้องสนิทก่อนช่วยกัน";
            GUI.Label(new Rect(card.x + 14, card.y + 44, card.width - 28, 42), status, label);
            GUI.enabled = actor.CanCommand;
            float third = (card.width - 36f) / 3f;
            if (ModalGui.Button(new Rect(card.x + 10, card.y + 91, third, 44), "ตามมา", button)) actor.RequestFollow();
            GUI.enabled = actor.CanStop;
            if (ModalGui.Button(new Rect(card.x + 18 + third, card.y + 91, third, 44), "รอตรงนี้", button)) actor.RequestWait();
            CompanionStationData next = scenario.stations.Find(s => s != null && !actor.IsCompleted(s));
            GUI.enabled = actor.CanHelp(next);
            if (ModalGui.Button(new Rect(card.x + 26 + third * 2, card.y + 91, third, 44), "ช่วยกัน", button)) actor.RequestHelp(next);
            GUI.enabled = actor.CanCommand;
            if (ModalGui.Button(new Rect(card.x + 10, card.y + 145, card.width - 20, 38), "อยู่ด้วยกันนะ", button)) actor.RequestComfort();
            GUI.enabled = previousEnabled;
            string note = !actor.CanCommand ? "เข้าใกล้เพื่อนเพื่อชวน • คุยตามปกติด้วย E" :
                next == null ? "เปิดกล่องด้วยกันแล้ว • อ่านความทรงจำในสมุด J" : "กล่องสองมือ: ให้เพื่อนยืนสีฟ้า คุณยืนสีทอง";
            GUI.Label(new Rect(card.x + 12, card.y + 185, card.width - 24, 44), note, label);
            foreach (var station in scenario.stations)
            {
                bool done = actor.IsCompleted(station);
                DrawBox((station.companionPoint + station.playerPoint) * 0.5f, done,
                    actor.ActiveStation == station ? actor.Progress : 0f);
                DrawWorld(station.companionPoint, scenario.displayName, done ? donePad : bluePad, new Vector2(96f, 42f));
                DrawWorld(station.playerPoint, done ? "สำเร็จ" : "คุณ", done ? donePad : goldPad, new Vector2(70f, 42f));
                Vector2 middle = (station.companionPoint + station.playerPoint) * 0.5f + Vector2.up * 0.65f;
                string caption = done ? "กล่องเปิดแล้ว • ทำสำเร็จด้วยกัน" :
                    actor.ActiveStation == station ? station.title + " " + Mathf.RoundToInt(actor.Progress * 100f) + "%" : station.title + " • ชวนเพื่อนช่วย";
                DrawWorld(middle, caption, panel, new Vector2(270f, 36f));
            }
            if (actor.HasBubble) DrawWorld((Vector2)actor.transform.position + Vector2.up * 1.1f, actor.Bubble, bubble, new Vector2(390f, 76f));
            GUI.matrix = previous; GUI.color = previousColor; GUI.enabled = previousEnabled; GUI.depth = previousDepth;
        }
        private static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = previous;
        }
        // A code-drawn 2D prop, with a lifted lid and a visible shared-action meter.
        private static void DrawBox(Vector2 point, bool opened, float progress)
        {
            if (Camera.main == null) return;
            Vector3 pixel = Camera.main.WorldToScreenPoint(point);
            if (pixel.z <= 0f) return;
            float x = pixel.x / UiScale.Factor, y = (Screen.height - pixel.y) / UiScale.Factor;
            Fill(new Rect(x - 40, y + 18, 80, 9), new Color(0, 0, 0, 0.35f));
            Fill(new Rect(x - 34, y - 15, 68, 40), new Color(0.25f, 0.15f, 0.1f));
            Fill(new Rect(x - 30, y - 11, 60, 32), opened ? new Color(0.85f, 0.72f, 0.4f) : new Color(0.48f, 0.3f, 0.17f));
            Fill(new Rect(x - 35, y - (opened ? 32 : 17), 70, 12), new Color(0.62f, 0.43f, 0.23f));
            Fill(new Rect(x - 5, y - (opened ? 32 : 17), 10, opened ? 12 : 36), new Color(0.91f, 0.73f, 0.31f));
            if (progress > 0f)
            {
                Fill(new Rect(x - 38, y + 32, 76, 7), new Color(0.05f, 0.1f, 0.12f));
                Fill(new Rect(x - 37, y + 33, 74 * progress, 5), new Color(0.4f, 0.9f, 0.86f));
            }
        }
        private static void DrawWorld(Vector2 point, string text, GUIStyle style, Vector2 size)
        {
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 pixel = camera.WorldToScreenPoint(point);
            if (pixel.z <= 0f) return;
            float x = pixel.x / UiScale.Factor, y = (Screen.height - pixel.y) / UiScale.Factor;
            GUI.Box(new Rect(Mathf.Clamp(x - size.x * 0.5f, 4f, Mathf.Max(4f, UiScale.Width - size.x - 4f)),
                Mathf.Clamp(y - size.y * 0.5f, 4f, Mathf.Max(4f, UiScale.Height - size.y - 4f)), size.x, size.y), text, style);
        }
        private void OnDestroy() { foreach (var texture in textures) if (texture != null) Destroy(texture); }
    }
}
