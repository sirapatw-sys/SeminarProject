using UnityEngine;

namespace MysteryGame.Companions
{
    public readonly struct CompanionInspectionPose
    {
        public readonly float Ready, Bend, Sweep, Reach;
        public CompanionInspectionPose(float ready, float bend, float sweep, float reach)
        { Ready = ready; Bend = bend; Sweep = sweep; Reach = reach; }
    }

    // World-space arms show actual grip/search work without moving the collision body.
    public class CompanionWorkVisuals : MonoBehaviour
    {
        private NpcCompanionController actor;
        private CharacterVisualController artwork;
        private Material material;
        private LineRenderer[] arms, hands;
        private LineRenderer lens, lensHandle;
        public bool IsWorking { get; private set; }
        public Vector2 RightHand { get; private set; }
        public Vector2 InspectionFocus { get; private set; }
        private LineRenderer Line(string name, float width, Color color, int points = 2)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = points; line.numCapVertices = 8; line.numCornerVertices = 4;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
            line.enabled = false; return line;
        }
        private void Awake()
        {
            actor = GetComponent<NpcCompanionController>(); artwork = GetComponent<CharacterVisualController>();
            material = new Material(Shader.Find("Sprites/Default"));
            arms = new[] { Line("HelpingArmLeft", 0.09f, new Color(0.38f, 0.46f, 0.56f), 3), Line("HelpingArmRight", 0.09f, new Color(0.38f, 0.46f, 0.56f), 3) };
            hands = new[] { Line("HelpingHandLeft", 0.13f, new Color(0.98f, 0.82f, 0.68f)), Line("HelpingHandRight", 0.13f, new Color(0.98f, 0.82f, 0.68f)) };
            lens = Line("InspectionLens", 0.016f, new Color(0.92f, 0.8f, 0.52f, 0.75f), 24); lens.loop = true;
            lensHandle = Line("InspectionLensHandle", 0.023f, new Color(0.92f, 0.8f, 0.52f, 0.75f));
        }
        // Shared by the sprite and hands. Time comes from the verified task, so a
        // paused inspection freezes its pose; no animation ever moves the physics root.
        public static CompanionInspectionPose EvaluateInspection(float seconds)
        {
            float time = Mathf.Max(0f, seconds);
            float ready = Mathf.SmoothStep(0f, 1f, time / 0.28f);
            float phase = Mathf.Repeat(time, 2.6f);
            float bend = ready * Mathf.SmoothStep(0f, 1f, (phase - 0.35f) / 0.5f) *
                (1f - Mathf.SmoothStep(0f, 1f, (phase - 1.65f) / 0.55f));
            float sweep = bend * Mathf.Sin((phase - 0.85f) * Mathf.PI * 2f / 1.6f);
            return new CompanionInspectionPose(ready, bend, sweep, ready * (0.12f + 0.38f * bend));
        }
        private void LateUpdate()
        {
            IsWorking = actor != null && actor.isActiveAndEnabled && (actor.IsInspecting ||
                (actor.Mode == CompanionMode.Helping && actor.ActiveStation != null));
            bool inspecting = IsWorking && actor.IsInspecting;
            for (int i = 0; i < 2; i++) { arms[i].enabled = hands[i].enabled = IsWorking && !inspecting; }
            lens.enabled = lensHandle.enabled = inspecting;
            if (!IsWorking) return;
            Vector2 target = actor.IsInspecting ? actor.InspectionPoint :
                (actor.ActiveStation.companionPoint + actor.ActiveStation.playerPoint) * 0.5f;
            Vector2 direction = (target - actor.Feet).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
            float wave = Mathf.Sin(actor.WorkAnimationTime * 5.5f);
            float reach = 0.72f + 0.035f * wave;
            var pose = EvaluateInspection(actor.WorkAnimationTime);
            int order = Mathf.Max(artwork != null ? artwork.ArtworkSortingOrder + 2 : 25,
                18 - Mathf.RoundToInt(actor.Feet.y * 10f));
            if (inspecting)
            {
                // The artwork already contains arms. A small working-only lens and the
                // shared body pose communicate inspection without drawing extra limbs.
                Bounds bounds = artwork != null ? artwork.ArtworkBounds : new Bounds(actor.Feet + Vector2.up, Vector3.one);
                InspectionFocus = new Vector2(actor.Feet.x + Mathf.Clamp(bounds.size.x * 0.28f, 0.26f, 0.44f) + pose.Sweep * 0.065f,
                    bounds.max.y - 0.14f - pose.Bend * 0.04f);
                float radius = 0.065f + pose.Reach * 0.06f;
                for (int i = 0; i < lens.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / lens.positionCount;
                    lens.SetPosition(i, InspectionFocus + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
                lensHandle.SetPosition(0, InspectionFocus + new Vector2(radius * 0.7f, -radius * 0.7f));
                lensHandle.SetPosition(1, InspectionFocus + new Vector2(radius * 1.7f, -radius * 1.7f));
                lens.sortingOrder = lensHandle.sortingOrder = order;
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector2 shoulder = actor.Feet + Vector2.up * 0.72f + Vector2.right * (side * 0.13f);
                Vector2 grip = target - direction * 0.35f + Vector2.up *
                    (actor.ActiveStation != null && actor.ActiveStation.useSceneProp ? 0.55f : 0.15f);
                Vector2 hand = Vector2.MoveTowards(shoulder, grip + Vector2.up * (side * 0.08f), reach);
                hand += Vector2.right * (wave * 0.025f);
                Vector2 elbow = Vector2.Lerp(shoulder, hand, 0.52f) + Vector2.down * 0.055f;
                arms[i].SetPosition(0, shoulder); arms[i].SetPosition(1, elbow); arms[i].SetPosition(2, hand);
                hands[i].SetPosition(0, hand); hands[i].SetPosition(1, hand + direction * 0.035f);
                arms[i].sortingOrder = hands[i].sortingOrder = order;
                if (i == 1) RightHand = hand;
            }
        }
        private void OnDisable()
        {
            IsWorking = false;
            if (arms != null) for (int i = 0; i < 2; i++) { arms[i].enabled = hands[i].enabled = false; }
            if (lens != null) lens.enabled = false; if (lensHandle != null) lensHandle.enabled = false;
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
