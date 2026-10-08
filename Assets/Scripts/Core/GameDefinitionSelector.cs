using UnityEngine;

namespace MysteryGame.Core
{
    [DefaultExecutionOrder(-1000)]
    public class GameDefinitionSelector : MonoBehaviour
    {
        public GameDefinition definition;
        private void Awake()
        {
            GameDefinition.SelectForScene(definition, this);
        }

        private void OnDestroy() { GameDefinition.ReleaseForScene(this); }
    }
}
