using System;
using System.Collections.Generic;
using UnityEngine;

namespace MysteryGame.Companions
{
    /// <summary>Small bounded-room path finder. No teleports, diagonal corner cuts or AI-authored destinations.</summary>
    public static class CompanionNavigator
    {
        // Physics queries on a shortened segment need not agree with the full
        // cast. Validate the actual stopping point, then search the grid for
        // another reachable point on the circle if the shortcut is unsafe.
        public static bool TryFollowPath(Vector2 start, Vector2 player, float distance, Rect bounds, float cell,
            Func<Vector2, bool> free, Func<Vector2, Vector2, bool> clear, List<Vector2> path)
        {
            if (path == null) return false;
            path.Clear();
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f ||
                !TryPath(start, player, bounds, cell, free, clear, path)) return false;
            if (Vector2.Distance(start, player) <= distance) { path.Clear(); return true; }
            if (TrimFollowPath(start, player, distance, free, clear, path)) return true;
            path.Clear();
            return TryGridPath(start, player, bounds, cell, free, clear, path, distance) &&
                TrimFollowPath(start, player, distance, free, clear, path);
        }

        private static bool TrimFollowPath(Vector2 start, Vector2 player, float distance,
            Func<Vector2, bool> free, Func<Vector2, Vector2, bool> clear, List<Vector2> path)
        {
            Vector2 from = start;
            for (int i = 0; i < path.Count; i++)
            {
                Vector2 segment = path[i] - from, relative = from - player;
                float lengthSquared = segment.sqrMagnitude;
                float dot = Vector2.Dot(relative, segment);
                float discriminant = dot * dot - lengthSquared * (relative.sqrMagnitude - distance * distance);
                if (lengthSquared > 0f && discriminant >= 0f)
                {
                    float t = (-dot - Mathf.Sqrt(discriminant)) / lengthSquared;
                    if (t >= 0f && t <= 1f)
                    {
                        Vector2 endpoint = from + segment * t;
                        if (!free(endpoint) || !clear(from, endpoint)) { path.Clear(); return false; }
                        path[i] = endpoint;
                        path.RemoveRange(i + 1, path.Count - i - 1);
                        return true;
                    }
                }
                if (!free(path[i]) || !clear(from, path[i])) { path.Clear(); return false; }
                from = path[i];
            }
            // Floating point rounding can put a boundary endpoint just outside.
            if (path.Count > 0 && Mathf.Abs(Vector2.Distance(path[path.Count - 1], player) - distance) < 0.001f)
                return true;
            path.Clear(); return false;
        }

        public static bool TryPath(Vector2 start, Vector2 goal, Rect bounds, float cell,
            Func<Vector2, bool> free, Func<Vector2, Vector2, bool> clear, List<Vector2> path)
        {
            if (path == null) return false;
            path.Clear();
            if (free == null || clear == null || float.IsNaN(cell) || float.IsInfinity(cell) ||
                float.IsNaN(start.x + start.y + goal.x + goal.y + bounds.x + bounds.y + bounds.width + bounds.height) ||
                float.IsInfinity(start.x + start.y + goal.x + goal.y + bounds.x + bounds.y + bounds.width + bounds.height) ||
                cell <= 0f || bounds.width <= 0f || bounds.height <= 0f ||
                !bounds.Contains(start) || !bounds.Contains(goal) || !free(goal)) return false;
            if ((double)(bounds.width / cell + 1f) * (bounds.height / cell + 1f) > 5000d) return false;
            if (clear(start, goal)) { path.Add(goal); return true; }
            return TryGridPath(start, goal, bounds, cell, free, clear, path);
        }

        private static bool TryGridPath(Vector2 start, Vector2 goal, Rect bounds, float cell,
            Func<Vector2, bool> free, Func<Vector2, Vector2, bool> clear, List<Vector2> path, float stoppingDistance = 0f)
        {
            int width = Mathf.FloorToInt(bounds.width / cell) + 1;
            int height = Mathf.FloorToInt(bounds.height / cell) + 1;
            if ((long)width * height > 5000) return false;
            Vector2 origin = bounds.min;
            Func<Vector2, Vector2Int> index = p => new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt((p.x - origin.x) / cell), 0, width - 1),
                Mathf.Clamp(Mathf.RoundToInt((p.y - origin.y) / cell), 0, height - 1));
            Func<Vector2Int, Vector2> point = p => origin + new Vector2(p.x * cell, p.y * cell);
            // A free off-grid endpoint can round into furniture. Connect each
            // endpoint to nearby free cells instead of rejecting a valid room
            // route because of that one rounded cell (at most 9 per endpoint).
            Func<Vector2, bool, List<Vector2Int>> connections = (endpoint, fromStart) => {
                var cells = new List<Vector2Int>(); Vector2Int center = index(endpoint);
                for (int y = center.y - 1; y <= center.y + 1; y++)
                    for (int x = center.x - 1; x <= center.x + 1; x++)
                    {
                        if (x < 0 || y < 0 || x >= width || y >= height) continue;
                        var candidate = new Vector2Int(x, y); Vector2 position = point(candidate);
                        if (bounds.Contains(position) && free(position) &&
                            (fromStart ? clear(endpoint, position) : clear(position, endpoint))) cells.Add(candidate);
                    }
                cells.Sort((a, b) => (point(a) - endpoint).sqrMagnitude.CompareTo((point(b) - endpoint).sqrMagnitude));
                return cells;
            };
            var firstCells = connections(start, true);
            var lastCells = stoppingDistance > 0f ? null : new HashSet<Vector2Int>(connections(goal, false));
            if (firstCells.Count == 0 || (lastCells != null && lastCells.Count == 0)) return false;
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var first in firstCells) { queue.Enqueue(first); previous[first] = first; }
            var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            Vector2Int last = default(Vector2Int); bool found = false;
            Vector2 endpoint = goal;
            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                if (lastCells != null ? lastCells.Contains(current) :
                    Vector2.Distance(point(current), goal) >= stoppingDistance)
                {
                    endpoint = stoppingDistance > 0f ? goal + (point(current) - goal).normalized * stoppingDistance : goal;
                    if (bounds.Contains(endpoint) && free(endpoint) && clear(point(current), endpoint))
                    { last = current; found = true; break; }
                }
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height || previous.ContainsKey(next)) continue;
                    Vector2 target = point(next);
                    if (!bounds.Contains(target) || !free(target) || !clear(point(current), target)) continue;
                    previous[next] = current; queue.Enqueue(next);
                }
            }
            if (!found) return false;
            Vector2Int cursor = last;
            while (previous[cursor] != cursor) { path.Add(point(cursor)); cursor = previous[cursor]; }
            path.Add(point(cursor)); path.Reverse(); path.Add(endpoint);
            return true;
        }
    }
}
