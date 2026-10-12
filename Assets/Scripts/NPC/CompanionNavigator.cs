using System;
using System.Collections.Generic;
using UnityEngine;

namespace MysteryGame.Companions
{
    /// <summary>Small bounded-room path finder. No teleports, diagonal corner cuts or AI-authored destinations.</summary>
    public static class CompanionNavigator
    {
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
            int width = Mathf.FloorToInt(bounds.width / cell) + 1;
            int height = Mathf.FloorToInt(bounds.height / cell) + 1;
            if ((long)width * height > 5000) return false;
            Vector2 origin = bounds.min;
            Func<Vector2, Vector2Int> index = p => new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt((p.x - origin.x) / cell), 0, width - 1),
                Mathf.Clamp(Mathf.RoundToInt((p.y - origin.y) / cell), 0, height - 1));
            Func<Vector2Int, Vector2> point = p => origin + new Vector2(p.x * cell, p.y * cell);
            Vector2Int first = index(start), last = index(goal);
            if (!free(point(first)) || !free(point(last)) || !clear(start, point(first)) || !clear(point(last), goal)) return false;
            var queue = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(first); previous[first] = first;
            var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0 && !previous.ContainsKey(last))
            {
                Vector2Int current = queue.Dequeue();
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;
                    if (next.x < 0 || next.y < 0 || next.x >= width || next.y >= height || previous.ContainsKey(next)) continue;
                    Vector2 target = point(next);
                    if (!bounds.Contains(target) || !free(target) || !clear(point(current), target)) continue;
                    previous[next] = current; queue.Enqueue(next);
                }
            }
            if (!previous.ContainsKey(last)) return false;
            for (Vector2Int cursor = last; cursor != first; cursor = previous[cursor]) path.Add(point(cursor));
            path.Add(point(first)); path.Reverse(); path.Add(goal);
            return true;
        }
    }
}
