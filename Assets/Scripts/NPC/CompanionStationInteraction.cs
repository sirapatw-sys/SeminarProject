using MysteryGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysteryGame.Companions
{
    // The prop answers E. Only a typed request to the NPC starts cooperation.
    public class CompanionStationInteraction : MonoBehaviour, IFocusable
    {
        private NpcCompanionController actor;
        private CompanionStationData station;
        private Sprite shape;
        private Transform lid, paper;
        public CompanionStationData Station { get { return station; } }
        public static void Install(NpcCompanionController owner)
        {
            foreach (var existing in FindObjectsOfType<CompanionStationInteraction>())
                if (existing.actor == owner && !owner.Scenario.stations.Contains(existing.station)) Destroy(existing.gameObject);
            foreach (var data in owner.Scenario.stations)
            {
                if (data.useSceneProp) continue;
                bool installed = false;
                foreach (var existing in FindObjectsOfType<CompanionStationInteraction>())
                    if (existing.actor == owner && existing.station == data) { installed = true; break; }
                if (installed) continue;
                var prop = new GameObject("CompanionProp_" + data.id);
                SceneManager.MoveGameObjectToScene(prop, owner.gameObject.scene);
                prop.transform.position = (data.companionPoint + data.playerPoint) * 0.5f;
                var circle = prop.AddComponent<CircleCollider2D>(); circle.isTrigger = true; circle.radius = 1.35f;
                var interaction = prop.AddComponent<CompanionStationInteraction>(); interaction.actor = owner; interaction.station = data;
                interaction.BuildProp();
            }
        }
        public bool CanFocus { get { return actor != null && actor.isActiveAndEnabled && actor.Scenario != null &&
            station != null && actor.Scenario.stations.Contains(station) && actor.RoomId == GameState.Instance?.GetCurrentScene() &&
            (actor.IsCompleted(station) || ConditionRule.AllHold(station.conditions, GameState.Instance)); } }
        public string FocusPrompt { get { return "กด E เพื่อ" + (actor.IsCompleted(station) ? "อ่านสิ่งที่พบใน " : "สำรวจ ") + station.title; } }
        public Vector2 FocusPoint { get { return transform.position; } }
        public void Interact()
        {
            if (!CanFocus || InputGate.IsBlocked || ItemPopupUI.IsBusy ||
                Vector2.Distance(actor.PlayerFeet, FocusPoint) > 1.65f || DialogueManager.Instance == null) return;
            string text = actor.IsCompleted(station) ? station.journalText :
                (!string.IsNullOrWhiteSpace(station.propDescription) ? station.propDescription :
                "มีจุดจับคนละด้านของ «" + station.title + "» ลองชวน " + actor.Scenario.displayName + " มาช่วยอีกด้าน");
            DialogueManager.Instance.StartDialogue(station.title, new[] { text }, false);
        }
        private void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Player")) InteractionFocus.Enter(this); }
        private void OnTriggerExit2D(Collider2D other) { if (other.CompareTag("Player")) InteractionFocus.Exit(this); }
        private void OnDisable() { InteractionFocus.Exit(this); }
        private void Update() { if (actor == null) Destroy(gameObject); }
        private Transform Piece(string name, Vector2 offset, Vector2 size, Color color, int layer)
        {
            var piece = new GameObject(name); piece.transform.SetParent(transform, false);
            piece.transform.localPosition = offset; piece.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = piece.AddComponent<SpriteRenderer>(); renderer.sprite = shape; renderer.color = color;
            renderer.sortingOrder = 10 - Mathf.RoundToInt(transform.position.y * 10f) + layer;
            return piece.transform;
        }
        private void BuildProp()
        {
            var texture = Texture2D.whiteTexture;
            shape = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, texture.width);
            Piece("BoxShadow", new Vector2(0, -0.25f), new Vector2(1.05f, 0.14f), new Color(0, 0, 0, 0.4f), 0);
            Piece("BoxBody", Vector2.zero, new Vector2(0.9f, 0.5f), new Color(0.37f, 0.23f, 0.13f), 1);
            Piece("BoxFront", new Vector2(0, -0.08f), new Vector2(0.82f, 0.25f), new Color(0.52f, 0.34f, 0.18f), 2);
            Piece("LeftGrip", new Vector2(-0.51f, 0.08f), new Vector2(0.13f, 0.27f), new Color(0.93f, 0.71f, 0.3f), 3);
            Piece("RightGrip", new Vector2(0.51f, 0.08f), new Vector2(0.13f, 0.27f), new Color(0.93f, 0.71f, 0.3f), 3);
            lid = Piece("BoxLid", new Vector2(0, 0.24f), new Vector2(1f, 0.16f), new Color(0.68f, 0.45f, 0.24f), 4);
            Piece("BoxClasp", Vector2.zero, new Vector2(0.12f, 0.37f), new Color(0.94f, 0.75f, 0.35f), 5);
            paper = Piece("BoxNote", new Vector2(0, 0.1f), new Vector2(0.34f, 0.22f), new Color(0.96f, 0.92f, 0.76f), 6);
            paper.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            if (lid == null || actor == null) return;
            bool completed = actor.IsCompleted(station);
            bool working = actor.ActiveStation == station && actor.Mode == CompanionMode.Helping;
            float motion = working ? Mathf.Sin(actor.WorkAnimationTime * 5.5f) * 0.025f : 0f;
            lid.localPosition = new Vector3(0, completed ? 0.55f : 0.24f + motion, 0);
            lid.localRotation = Quaternion.Euler(0, 0, completed ? -14f : motion * 80f);
            paper.gameObject.SetActive(completed);
        }
        private void OnDestroy() { if (shape != null) Destroy(shape); }
    }
}
