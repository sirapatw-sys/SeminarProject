using UnityEngine;
using MysteryGame.Core;

public class RelationshipManager : MonoBehaviour
{
    public static RelationshipManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // =====================================================
    // Get Relationship
    // =====================================================

    public int GetRelationship(string npcName)
    {
        if (GameState.Instance == null)
        {
            Debug.LogError("GameState.Instance is null.");
            return 0;
        }

        return GameState.Instance.GetRelationship(npcName);
    }

    // =====================================================
    // Change Relationship
    // =====================================================

    public void ChangeRelationship(string npcName, int amount)
    {
        if (GameState.Instance == null)
        {
            Debug.LogError("GameState.Instance is null.");
            return;
        }

        GameState.Instance.ChangeRelationship(npcName, amount);
    }
}
