using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Companions
{
    /// <summary>Resolve semantic places to real reachable ground. Model-authored coordinates never enter navigation.</summary>
    public static class CompanionSpatialResolver
    {
        public static string Describe(CompanionWorldTarget target, string relation, CompanionWorldTarget reference)
        {
            if (relation == "between") return "ระหว่าง " + target.Definition.title + " กับ " + reference.Definition.title;
            string side = relation == "left" ? "ด้านซ้ายของ " : relation == "right" ? "ด้านขวาของ " :
                relation == "front" ? "ด้านหน้าของ " : relation == "behind" ? "ด้านหลังของ " : relation == "near" ? "ใกล้ " : "";
            return side + target.Definition.title;
        }

        public static bool TryResolve(NpcCompanionController actor, CompanionWorldTarget target, CompanionWorldVerb verb,
            string relation, CompanionWorldTarget reference, out Vector2 goal)
        {
            goal = Vector2.zero;
            if (actor == null || actor.Scenario == null || target == null ||
                verb != CompanionWorldVerb.Move && relation != "at" ||
                relation != "between" && reference != null) return false;
            var bounds = target.SpatialBounds;
            Vector2 center = bounds.center, preferred = target.ApproachPoint;
            Vector2 other = Vector2.zero;
            float margin = actor.Scenario.clearance + 0.15f;
            switch (relation)
            {
                case "at": break;
                case "near": break;
                case "left": preferred = center + Vector2.left * (bounds.extents.x + Mathf.Max(0.7f, margin)); break;
                case "right": preferred = center + Vector2.right * (bounds.extents.x + Mathf.Max(0.7f, margin)); break;
                case "front": preferred = center + Vector2.down * (bounds.extents.y + Mathf.Max(0.7f, margin)); break;
                case "behind": preferred = center + Vector2.up * (bounds.extents.y + Mathf.Max(0.7f, margin)); break;
                case "between":
                    if (reference == null || reference == target || !reference.IsVisible(GameState.Instance) ||
                        RoomWorldRegistry.Find(actor.RoomId, GameState.Instance, reference.Definition.id) != reference) return false;
                    other = reference.SpatialBounds.center;
                    if (Vector2.Distance(center, other) < 0.5f) return false;
                    preferred = (center + other) * 0.5f; break;
                default: return false;
            }
            if (!Finite(preferred)) return false;
            var candidates = new List<Vector2> { preferred };
            // Preserve exact legacy authored interaction points. Only spatial/scene-bound goals can project onto nearby floor.
            if (relation != "at" || target.Definition.useSceneGeometry)
            {
                float step = Mathf.Max(0.2f, actor.Scenario.cellSize);
                int radius = Mathf.Min(4, Mathf.CeilToInt(1.2f / step));
                for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                {
                    var p = preferred + new Vector2(x * step, y * step);
                    if ((x != 0 || y != 0) && Vector2.Distance(p, preferred) <= 1.2f) candidates.Add(p);
                }
                candidates.Sort((a, b) => (a - preferred).sqrMagnitude.CompareTo((b - preferred).sqrMagnitude));
            }
            var route = new List<Vector2>(); int attempts = 0;
            foreach (var candidate in candidates)
            {
                if (!actor.Scenario.walkBounds.Contains(candidate) || !actor.IsFree(candidate)) continue;
                if (relation == "left" && candidate.x > bounds.min.x - margin ||
                    relation == "right" && candidate.x < bounds.max.x + margin ||
                    relation == "front" && candidate.y > bounds.min.y - margin ||
                    relation == "behind" && candidate.y < bounds.max.y + margin) continue;
                if (relation == "between")
                {
                    Vector2 axis = other - center;
                    float along = Vector2.Dot(candidate - center, axis) / axis.sqrMagnitude;
                    if (along < 0.2f || along > 0.8f || Vector2.Distance(candidate, center + axis * along) > 1.2f) continue;
                }
                if (CompanionNavigator.TryPath(actor.Feet, candidate, actor.Scenario.walkBounds, actor.Scenario.cellSize,
                    actor.IsFree, actor.IsClear, route)) { goal = candidate; return true; }
                if (++attempts >= 12) break;
            }
            return false;
        }
        static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
    }
}
