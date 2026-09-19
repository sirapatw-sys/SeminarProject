using UnityEngine;
using MysteryGame.Core;

public class GameStateTest : MonoBehaviour
{
    private void Start()
    {
        if (GameState.Instance == null)
        {
            Debug.LogError("GameState.Instance is null.");
            return;
        }

        Debug.Log(
            "Current Scene: " +
            GameState.Instance.GetCurrentScene()
        );

        Debug.Log(
            "Current Goal: " +
            GameState.Instance.GetCurrentGoal()
        );

        if (RelationshipManager.Instance != null)
        {
            Debug.Log(
                "Alice Relationship: " +
                RelationshipManager.Instance.GetRelationship("Alice")
            );
        }
    }
}