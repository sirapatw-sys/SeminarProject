using UnityEngine;
using System.Collections.Generic;

public class RelationshipManager : MonoBehaviour
{
    public static RelationshipManager Instance;

    private Dictionary<string, int> relationships =
        new Dictionary<string, int>();

    private void Awake()
    {
        Instance = this;

        // ค่าเริ่มต้นของ Alice
        if (!relationships.ContainsKey("Alice"))
        {
            relationships.Add("Alice", 50);
        }
    }

    // =====================================================
    // Get Relationship
    // =====================================================

    public int GetRelationship(string npcName)
    {
        if (relationships.ContainsKey(npcName))
        {
            return relationships[npcName];
        }

        relationships[npcName] = 50;

        return 50;
    }

    // =====================================================
    // Change Relationship
    // =====================================================

    public void ChangeRelationship(string npcName, int amount)
    {
        int currentValue = GetRelationship(npcName);

        currentValue += amount;

        // จำกัด 0 - 100
        currentValue = Mathf.Clamp(currentValue, 0, 100);

        relationships[npcName] = currentValue;

        Debug.Log(
            npcName +
            " Relationship = " +
            currentValue
        );
    }
}