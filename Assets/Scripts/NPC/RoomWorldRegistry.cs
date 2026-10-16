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
            return UnityEngine.Object.FindObjectsOfType<CompanionWorldTarget>()
                .Where(t => t.RoomId == room && t.IsVisible(state))
                .GroupBy(t => t.Definition.id, StringComparer.Ordinal).Where(g => g.Count() == 1)
                .Select(g => g.First()).OrderBy(t => t.Definition.id, StringComparer.Ordinal).ToList();
        }
        public static CompanionWorldTarget Find(string room, GameState state, string id) =>
            Available(room, state).FirstOrDefault(t => t.Definition.id == id);
    }
}
