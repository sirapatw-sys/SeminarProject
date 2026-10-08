using System.Collections;
using MysteryGame.Knowledge;
using TMPro;
using UnityEngine;

/// <summary>
/// Now and then a random room noise (a creak, footsteps) makes an NPC jump:
/// a small hop and a short line above their head. Which NPCs flinch, how
/// often and what they say come from NpcProfileData.startleLines and
/// startleChance, so only Stelle does it today.
/// </summary>
public class NpcStartleReaction : MonoBehaviour
{
    private const float BubbleSeconds = 2.2f;
    private static readonly Vector3 BubbleOffset = new Vector3(0f, 1.3f, 0f);

    private NpcProfileData profile;
    private CharacterVisualController visual;
    private TextMeshPro bubble;
    private float hideAt;

    /// <summary>Adds the reaction to an NPC whose profile has startle lines.</summary>
    public static void AttachIfWanted(GameObject npc, string npcId)
    {
        NpcProfileData data = string.IsNullOrWhiteSpace(npcId) ? null : KnowledgeLibrary.GetNpc(npcId);
        if (data == null || data.startleLines == null || data.startleLines.Count == 0 ||
            data.startleChance <= 0f || npc.GetComponent<NpcStartleReaction>() != null)
        {
            return;
        }

        npc.AddComponent<NpcStartleReaction>().profile = data;
    }

    private void Start()
    {
        visual = GetComponent<CharacterVisualController>();

        GameObject host = new GameObject("StartleBubble");
        host.transform.SetParent(transform, false);
        // BubbleOffset is in world units: undo the NPC's own scale.
        Vector3 scale = transform.lossyScale;
        float sx = Mathf.Max(Mathf.Abs(scale.x), 0.01f);
        float sy = Mathf.Max(Mathf.Abs(scale.y), 0.01f);
        host.transform.localPosition = new Vector3(BubbleOffset.x / sx, BubbleOffset.y / sy, 0f);
        host.transform.localScale = new Vector3(1f / sx, 1f / sy, 1f);

        bubble = host.AddComponent<TextMeshPro>();
        bubble.rectTransform.sizeDelta = new Vector2(8f, 1.5f);   // default box is 20 x 5
        bubble.fontSize = 3f;
        bubble.alignment = TextAlignmentOptions.Center;
        bubble.enableWordWrapping = false;
        bubble.color = new Color(1f, 0.93f, 0.8f);
        bubble.outlineWidth = 0.25f;
        bubble.outlineColor = new Color32(10, 12, 20, 255);
        bubble.GetComponent<Renderer>().sortingOrder = 101;
        host.SetActive(false);
    }

    private void OnEnable()
    {
        SfxPlayer.RoomNoisePlayed += HandleRoomNoise;
    }

    private void OnDisable()
    {
        SfxPlayer.RoomNoisePlayed -= HandleRoomNoise;
    }

    private void Update()
    {
        if (bubble != null && bubble.gameObject.activeSelf &&
            (Time.time >= hideAt || DialogueManager.IsDialogueOpen))
        {
            bubble.gameObject.SetActive(false);
        }
    }

    private void HandleRoomNoise()
    {
        if (profile == null || InputGate.IsBlocked || Random.value > profile.startleChance)
        {
            return;
        }

        StartCoroutine(React());
    }

    private IEnumerator React()
    {
        // A beat after the sound, like a real flinch.
        yield return new WaitForSeconds(Random.Range(0.2f, 0.45f));
        if (InputGate.IsBlocked || bubble == null)
        {
            yield break;
        }

        if (visual != null)
        {
            visual.Startle();
        }

        bubble.text = profile.startleLines[Random.Range(0, profile.startleLines.Count)];
        bubble.gameObject.SetActive(true);
        hideAt = Time.time + BubbleSeconds;
    }
}
