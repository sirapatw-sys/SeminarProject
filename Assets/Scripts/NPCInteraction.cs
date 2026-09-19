using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    private bool playerInRange = false;

    private void Update()
    {
        if (playerInRange && Input.GetKeyDown(KeyCode.E))
        {
            string[] aliceDialogue =
            {
                "ฉันจำได้ว่ากุญแจยังอยู่ในห้องนี้...",
                "แต่ฉันจำไม่ได้ว่าเก็บไว้ที่ไหน",
                "ช่วยฉันหามันหน่อยได้ไหม?"
            };

            DialogueManager.Instance.StartDialogue(
                "Alice",
                aliceDialogue
            );
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log("Press E to talk");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}