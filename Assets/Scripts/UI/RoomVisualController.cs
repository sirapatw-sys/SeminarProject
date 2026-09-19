using UnityEngine;

public class RoomVisualController : MonoBehaviour
{
    [Header("Room Artwork")]
    [SerializeField] private Sprite roomBackground;
    [SerializeField] private bool hideSimpleRoomSprites = true;
    [SerializeField] private Color tint = Color.white;

    private void Start()
    {
        if (roomBackground == null)
        {
            return;
        }

        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            Debug.LogWarning("Room background requires a Main Camera.");
            return;
        }

        GameObject backgroundObject = new GameObject("IllustratedRoomBackground");
        SpriteRenderer renderer = backgroundObject.AddComponent<SpriteRenderer>();
        renderer.sprite = roomBackground;
        renderer.color = tint;
        renderer.sortingOrder = -100;

        backgroundObject.transform.position = new Vector3(
            targetCamera.transform.position.x,
            targetCamera.transform.position.y,
            0f
        );

        float cameraHeight = targetCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * targetCamera.aspect;
        float scale = Mathf.Max(
            cameraWidth / roomBackground.bounds.size.x,
            cameraHeight / roomBackground.bounds.size.y
        );
        backgroundObject.transform.localScale = new Vector3(scale, scale, 1f);

        if (hideSimpleRoomSprites)
        {
            HideSimpleRoomSprites();
        }
    }

    private static void HideSimpleRoomSprites()
    {
        SpriteRenderer[] renderers = FindObjectsOfType<SpriteRenderer>();
        foreach (SpriteRenderer renderer in renderers)
        {
            string objectName = renderer.gameObject.name;
            if (objectName == "Floor" || objectName.StartsWith("Wall_") ||
                objectName == "Desk" || objectName == "Drawer" ||
                objectName == "Painting" || objectName == "Door")
            {
                renderer.enabled = false;
            }
        }
    }
}
