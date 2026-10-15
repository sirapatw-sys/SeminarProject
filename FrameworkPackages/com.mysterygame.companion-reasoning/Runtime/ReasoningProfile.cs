using System.Collections.Generic;
using UnityEngine;
namespace CompanionFramework
{
    [CreateAssetMenu(menuName = "Companion Framework/Reasoning Profile")]
    public sealed class ReasoningProfile : ScriptableObject
    {
        public string npcId;
        [TextArea] public string insufficientEvidenceLine = "I do not have enough evidence to judge that yet.";
        [TextArea] public string clarifyLine = "Which observation supports that idea?";
        public List<ReasoningRule> rules = new List<ReasoningRule>();
    }
}
