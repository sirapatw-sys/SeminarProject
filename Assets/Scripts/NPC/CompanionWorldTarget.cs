using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

namespace MysteryGame.Companions
{
    public enum CompanionWorldVerb { Move, Inspect, Open }

    [Serializable]
    public class CompanionWorldDefinition
    {
        public string id;
        public string title;
        public List<string> aliases = new List<string>();
        [Tooltip("Exact scene object name. Empty = a walkable area, with no interaction.")]
        public string sceneObject;
        public Vector2 approachPoint;
        [Min(0.5f)] public float workSeconds = 1.5f;
        public bool allowInspect;
        public bool allowOpen;
        [TextArea] public string visibleDescription;
        [TextArea] public string inspectLine;
        public List<string> discoveredFactIds = new List<string>();
        public List<ConditionRule> visibleWhen = new List<ConditionRule>();
    }

    /// <summary>Scene binding. Does not expose InteractionData or puzzle answers to the model.</summary>
    public class CompanionWorldTarget : MonoBehaviour
    {
        public CompanionWorldDefinition Definition { get; private set; }
        public ObjectInteraction Interaction { get; private set; }
        public string RoomId => gameObject.scene.name;
        public bool IsVisible(GameState state) => isActiveAndEnabled && Definition != null &&
            state != null && state.GetCurrentScene() == RoomId && ConditionRule.AllHold(Definition.visibleWhen, state);
        public void Configure(CompanionWorldDefinition definition, ObjectInteraction interaction)
        { Definition = definition; Interaction = interaction; }
        public bool Allows(CompanionWorldVerb verb) => Definition != null &&
            (verb == CompanionWorldVerb.Move || ReferenceEquals(Interaction, null) || Interaction != null && Interaction.isActiveAndEnabled) &&
            (verb == CompanionWorldVerb.Move || verb == CompanionWorldVerb.Inspect && Definition.allowInspect ||
             verb == CompanionWorldVerb.Open && Definition.allowOpen);
    }
}
