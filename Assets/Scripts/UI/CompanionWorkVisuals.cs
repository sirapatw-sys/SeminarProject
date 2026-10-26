using UnityEngine;

namespace MysteryGame.Companions
{
    // World-space arms show actual grip/search work without moving the collision body.
    public class CompanionWorkVisuals : MonoBehaviour
    {
        private NpcCompanionController actor;
        private CharacterVisualController artwork;
        private Material material;
        private LineRenderer[] arms, hands;
        public bool IsWorking { get; private set; }
        public Vector2 RightHand { get; private set; }
        private LineRenderer Line(string name, float width, Color color)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = 2; line.numCapVertices = 8;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
            line.enabled = false; return line;
        }
        private void Awake()
        {
            actor = GetComponent<NpcCompanionController>(); artwork = GetComponent<CharacterVisualController>();
            material = new Material(Shader.Find("Sprites/Default"));
            arms = new[] { Line("HelpingArmLeft", 0.09f, new Color(0.38f, 0.46f, 0.56f)), Line("HelpingArmRight", 0.09f, new Color(0.38f, 0.46f, 0.56f)) };
            hands = new[] { Line("HelpingHandLeft", 0.13f, new Color(0.98f, 0.82f, 0.68f)), Line("HelpingHandRight", 0.13f, new Color(0.98f, 0.82f, 0.68f)) };
        }
        private void LateUpdate()
        {
            IsWorking = actor != null && actor.isActiveAndEnabled && (actor.IsInspecting ||
                (actor.Mode == CompanionMode.Helping && actor.ActiveStation != null));
            for (int i = 0; i < 2; i++) { arms[i].enabled = hands[i].enabled = IsWorking; }
            if (!IsWorking) return;
            Vector2 target = actor.IsInspecting ? actor.InspectionPoint :
                (actor.ActiveStation.companionPoint + actor.ActiveStation.playerPoint) * 0.5f;
            Vector2 direction = (target - actor.Feet).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            float wave = Mathf.Sin(actor.WorkAnimationTime * 5.5f);
            float reach = actor.IsInspecting ? 0.32f + 0.1f * wave : 0.72f + 0.035f * wave;
            int order = Mathf.Max(artwork != null ? artwork.ArtworkSortingOrder + 2 : 25,
                18 - Mathf.RoundToInt(actor.Feet.y * 10f));
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector2 shoulder = actor.Feet + Vector2.up * 0.72f + Vector2.right * (side * 0.13f);
                Vector2 grip = target - direction * 0.35f + Vector2.up *
                    (actor.ActiveStation != null && actor.ActiveStation.useSceneProp ? 0.55f : 0.15f);
                Vector2 hand = actor.IsInspecting ? shoulder + direction * reach + Vector2.down * 0.3f :
                    Vector2.MoveTowards(shoulder, grip + Vector2.up * (side * 0.08f), reach);
                hand += Vector2.right * (wave * 0.025f);
                arms[i].SetPosition(0, shoulder); arms[i].SetPosition(1, hand);
                hands[i].SetPosition(0, hand); hands[i].SetPosition(1, hand + direction * 0.035f);
                arms[i].sortingOrder = hands[i].sortingOrder = order;
                if (i == 1) RightHand = hand;
            }
        }
        private void OnDisable()
        { IsWorking = false; if (arms != null) for (int i = 0; i < 2; i++) { arms[i].enabled = hands[i].enabled = false; } }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
