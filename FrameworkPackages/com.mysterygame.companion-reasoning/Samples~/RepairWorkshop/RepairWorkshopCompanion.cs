using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CompanionFramework.Samples
{
    public sealed class RepairWorkshopCompanion : MonoBehaviour, IReasoningWorld
    {
        public ReasoningProfile profile;
        [Range(0, 3)] public int allowedDetail = 2;
        public List<string> observedFacts = new List<string>();
        public List<string> worldFlags = new List<string>();
        public int repairedDevices;
        private readonly CompanionEffectRegistry effects = new CompanionEffectRegistry();
        private string message = "";
        private string answer = "Inspect the device, then type an idea to your companion.";
        public int AllowedDetail => allowedDetail;
        public bool KnowsFact(string id) => observedFacts.Contains(id);
        public bool HasFlag(string id) => worldFlags.Contains(id);
        private void Awake()
        {
            effects.Register("repair-device", () => {
                if (!KnowsFact("loose_cable") || !KnowsFact("power_off") || HasFlag("repaired")) return false;
                worldFlags.Add("repaired"); repairedDevices++; return true;
            });
        }
        public string Reply(string text)
        {
            if (profile == null) return "Assign a Reasoning Profile in the Inspector.";
            var rules = ReasoningQueryMatcher.Read(profile, text).Rules;
            if (rules.Count != 1) return profile.clarifyLine;
            var result = profile.Evaluate(rules[0], this);
            return result.Outcome == ReasoningOutcome.Unavailable ? profile.unavailableLine :
                string.IsNullOrWhiteSpace(result.Line) ? profile.insufficientEvidenceLine : result.Line;
        }
        // Host-owned physical interaction, separate from any dialogue response.
        public bool RepairDevice() => effects.TryExecute("repair-device");
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 620, 360), GUI.skin.box);
            GUILayout.Label(profile != null ? profile.npcId + " - Repair Workshop" : "Repair Workshop");
            GUILayout.Label("Observed: " + string.Join(", ", observedFacts));
            if (GUILayout.Button("Inspect cable") && !KnowsFact("loose_cable")) observedFacts.Add("loose_cable");
            if (GUILayout.Button("Inspect power switch") && !KnowsFact("power_off")) observedFacts.Add("power_off");
            message = GUILayout.TextField(message);
            if (GUILayout.Button("Send")) answer = Reply(message);
            GUILayout.Label(answer);
            if (GUILayout.Button("Try repairing the device yourself")) answer = RepairDevice() ? "Device repaired." : "Missing evidence, or already repaired.";
            GUILayout.EndArea();
        }
    }
}
