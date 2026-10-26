using UnityEngine;

namespace MysteryGame.Core
{
    // State publishes facts; this replaceable adapter owns presentation.
    public class GamePresentationBridge : MonoBehaviour
    {
        private GameState observed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindObjectOfType<GamePresentationBridge>() == null)
                new GameObject("GamePresentationBridge").AddComponent<GamePresentationBridge>();
        }
        private void Awake() { DontDestroyOnLoad(gameObject); Subscribe(); }
        private void Update() { Subscribe(); }
        private void Subscribe()
        {
            if (observed == GameState.Instance) return;
            Unsubscribe();
            observed = GameState.Instance;
            if (observed != null) observed.ItemAdded += ShowItem;
        }
        private void ShowItem(string id) { ItemPopupUI.ShowItem(id); }
        private void Unsubscribe()
        {
            if (observed != null) observed.ItemAdded -= ShowItem;
            observed = null;
        }
        private void OnDestroy() { Unsubscribe(); }
    }
}
