using MysteryGame.Knowledge;
using UnityEngine;

namespace MysteryGame.Core
{
    [DefaultExecutionOrder(-1000)]
    public class GameDefinitionSelector : MonoBehaviour
    {
        public GameDefinition definition;
        private void Awake()
        {
            if (definition == null) return;
            GameDefinition.Override = definition;
            KnowledgeLibrary.ClearCache();
        }
    }
}
