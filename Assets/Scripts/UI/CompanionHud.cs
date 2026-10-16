using UnityEngine;
using MysteryGame.Core;

namespace MysteryGame.Companions
{
    public enum CompanionStationDisplay { Hidden, Nearby, Active }

    /// <summary>Read-only status/props. Requests are written by the player in dialogue, never chosen here.</summary>
    public class CompanionHud : MonoBehaviour
    {
        private NpcCompanionController actor;
        private GUIStyle caption, bubble, bluePad, goldPad;
        private readonly System.Collections.Generic.List<Texture2D> textures = new System.Collections.Generic.List<Texture2D>();
        private void Awake() { actor = GetComponent<NpcCompanionController>(); }
        public bool DiscoveryIndicatorVisible
        {
            get
            {
                var events = GetComponent<NpcEventController>();
                return actor != null && actor.HasDiscoveryInvitation && !actor.HasBubble &&
                    !InputGate.IsBlocked && !ItemPopupUI.IsBusy &&
                    (events == null || !events.isActiveAndEnabled || !events.HasPendingEvent);
            }
        }
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
            if (caption != null) return;
            caption = Style(new Color(0.025f, 0.045f, 0.065f, 0.85f), 16, TextAnchor.MiddleCenter);
            bubble = Style(new Color(0.03f, 0.08f, 0.12f, 0.96f), 18, TextAnchor.MiddleCenter);
            bluePad = Style(new Color(0.12f, 0.65f, 0.77f, 0.8f), 14, TextAnchor.MiddleCenter, 14);
            goldPad = Style(new Color(0.68f, 0.46f, 0.12f, 0.85f), 14, TextAnchor.MiddleCenter, 14);
        }
        // The prop stays in the room, but its labels are contextual, not a
        // permanent tutorial card. Finished activities never retain a banner.
        public CompanionStationDisplay StationDisplayFor(CompanionStationData station)
        {
            if (actor == null || actor.Scenario == null || !actor.isActiveAndEnabled || station == null ||
                InputGate.IsBlocked || ItemPopupUI.IsBusy || actor.RoomId != GameState.Instance?.GetCurrentScene() ||
                !actor.Scenario.stations.Contains(station) || actor.IsCompleted(station)) return CompanionStationDisplay.Hidden;
            Vector2 center = (station.companionPoint + station.playerPoint) * 0.5f;
            if (Vector2.Distance(actor.PlayerFeet, center) > actor.Scenario.activityLabelDistance)
                return CompanionStationDisplay.Hidden;
            return actor.ActiveStation == station ? CompanionStationDisplay.Active : CompanionStationDisplay.Nearby;
        }
        private void OnGUI()
        {
            if (actor == null || actor.Scenario == null || !actor.isActiveAndEnabled || InputGate.IsBlocked || ItemPopupUI.IsBusy ||
                actor.RoomId != GameState.Instance?.GetCurrentScene()) return;
            Matrix4x4 previous = GUI.matrix; Color previousColor = GUI.color; bool previousEnabled = GUI.enabled; int previousDepth = GUI.depth;
            UiScale.Apply(); GUI.depth = -2; Styles();
            var scenario = actor.Scenario;
            foreach (var station in scenario.stations)
            {
                var display = StationDisplayFor(station);
                if (station.useSceneProp && !ConditionRule.AllHold(station.conditions, GameState.Instance)) continue;
                if (display == CompanionStationDisplay.Hidden) continue;
                if (display == CompanionStationDisplay.Active)
                {
                    DrawWorld(station.companionPoint, actor.Scenario.displayName, bluePad, new Vector2(70f, 28f));
                    DrawWorld(station.playerPoint, "คุณ", goldPad, new Vector2(55f, 28f));
                }
                Vector2 middle = (station.companionPoint + station.playerPoint) * 0.5f + Vector2.up * 0.65f;
                string text = display == CompanionStationDisplay.Active
                    ? station.title + (string.IsNullOrWhiteSpace(station.holdForInteractionId) ? " " + Mathf.RoundToInt(actor.Progress * 100f) + "%" :
                        actor.Mode == CompanionMode.Helping ? " · จับไว้ให้แล้ว" : " · กำลังไปช่วย") : station.title;
                DrawWorld(middle, text, caption, new Vector2(190f, 32f));
            }
            if (actor.HasBubble)
            {
                float width = Mathf.Min(340f, UiScale.Width - 16f);
                float height = Mathf.Clamp(bubble.CalcHeight(new GUIContent(actor.Bubble), width), 40f, 112f);
                DrawWorld((Vector2)actor.transform.position + Vector2.up * 1.1f, actor.Bubble, bubble, new Vector2(width, height));
            }
            if (DiscoveryIndicatorVisible)
                DrawWorld((Vector2)actor.transform.position + Vector2.up * 1.1f, "!", goldPad, new Vector2(32f, 36f));
            else if (!actor.HasBubble && actor.Invitation != null)
                DrawWorld((Vector2)actor.transform.position + Vector2.up * 1.1f, "?", goldPad, new Vector2(32f, 36f));
            GUI.matrix = previous; GUI.color = previousColor; GUI.enabled = previousEnabled; GUI.depth = previousDepth;
        }
        private static void DrawWorld(Vector2 point, string text, GUIStyle style, Vector2 size)
        {
            var camera = Camera.main;
            if (camera == null) return;
            Vector3 pixel = camera.WorldToScreenPoint(point);
            if (pixel.z <= 0f || pixel.x < 0f || pixel.x > Screen.width || pixel.y < 0f || pixel.y > Screen.height) return;
            float x = pixel.x / UiScale.Factor, y = (Screen.height - pixel.y) / UiScale.Factor;
            GUI.Box(new Rect(Mathf.Clamp(x - size.x * 0.5f, 4f, Mathf.Max(4f, UiScale.Width - size.x - 4f)),
                Mathf.Clamp(y - size.y * 0.5f, 4f, Mathf.Max(4f, UiScale.Height - size.y - 4f)), size.x, size.y), text, style);
        }
        private void OnDestroy() { foreach (var texture in textures) if (texture != null) Destroy(texture); }
    }
}
