using System;
using System.Collections.Generic;
using System.Linq;
using MysteryGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MysteryGame.Companions
{
    public static class RoomWorldRegistry
    {
        static readonly List<CompanionWorldTarget> active = new List<CompanionWorldTarget>();
        static readonly Comparison<CompanionWorldTarget> byId = (a, b) => StringComparer.Ordinal.Compare(a.Definition.id, b.Definition.id);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { active.Clear(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SeedExisting()
        {
            // One bootstrap scan also supports Enter Play with domain/scene reload disabled.
            foreach (var target in UnityEngine.Object.FindObjectsOfType<CompanionWorldTarget>()) Register(target);
        }
        internal static void Register(CompanionWorldTarget target)
        { if (target != null && target.isActiveAndEnabled && !active.Contains(target)) active.Add(target); }
        internal static void Unregister(CompanionWorldTarget target) { active.Remove(target); }
        public static void Install(CompanionScenarioData scenario, Scene scene)
        {
            if (scenario?.worldTargets == null) return;
            // Resolve existing bindings first: Configure can run again after reset/enable.
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in scenario.worldTargets)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.id) || !ids.Add(definition.id)) continue;
                var bindings = all.Select(t => t.GetComponent<CompanionWorldTarget>())
                    .Where(t => t != null && t.Definition?.id == definition.id).ToArray();
                if (bindings.Length == 1) { bindings[0].Configure(definition, bindings[0].GetComponent<ObjectInteraction>()); continue; }
                GameObject target;
                if (!string.IsNullOrWhiteSpace(definition.sceneObject))
                {
                    var matches = all.Where(t => t.name == definition.sceneObject).ToArray();
                    if (matches.Length != 1) { Debug.LogWarning("World target needs one scene object: " + definition.id); continue; }
                    target = matches[0].gameObject;
                    // One definition per prop; distinct targets must not overwrite one another.
                    if (target.GetComponent<CompanionWorldTarget>() != null) continue;
                }
                else
                {
                    target = new GameObject("WorldArea_" + definition.id);
                    SceneManager.MoveGameObjectToScene(target, scene);
                    target.transform.position = definition.approachPoint;
                }
                target.AddComponent<CompanionWorldTarget>().Configure(definition, target.GetComponent<ObjectInteraction>());
            }
        }

        public static List<CompanionWorldTarget> Available(string room, GameState state)
        {
            var result = new List<CompanionWorldTarget>();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var target in active)
            {
                if (target == null || target.RoomId != room || !target.IsVisible(state) || string.IsNullOrWhiteSpace(target.Definition.id)) continue;
                string id = target.Definition.id; int count;
                counts.TryGetValue(id, out count); counts[id] = count + 1; result.Add(target);
            }
            for (int i = result.Count - 1; i >= 0; i--) if (counts[result[i].Definition.id] != 1) result.RemoveAt(i);
            result.Sort(byId); return result;
        }
        public static CompanionWorldTarget Find(string room, GameState state, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            CompanionWorldTarget result = null;
            foreach (var target in active)
            {
                if (target == null || target.Definition == null || target.Definition.id != id || target.RoomId != room || !target.IsVisible(state)) continue;
                if (result != null) return null; // Duplicate live IDs remain ambiguous, never pick one arbitrarily.
                result = target;
            }
            return result;
        }
    }
}
