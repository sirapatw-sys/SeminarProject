using UnityEngine;

public class CharacterVisualController : MonoBehaviour
{
    [Header("2D Character Artwork")]
    [SerializeField] private Sprite worldSprite;
    [SerializeField, Min(0.1f)] private float targetWorldHeight = 2.4f;
    [SerializeField] private Vector3 visualOffset;
    [SerializeField] private int sortingOrder = 10;
    [Header("Chibi Motion")]
    [SerializeField, Min(0f)] private float walkBounceHeight = 0.09f;
    [SerializeField, Min(0.1f)] private float walkCyclesPerSecond = 5f;
    [SerializeField, Min(0f)] private float idleBounceHeight = 0.015f;

    private Transform visualTransform;
    private SpriteRenderer visualRenderer;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private Vector3 lastWorldPosition;
    private float animationTime;

    private void Start()
    {
        if (worldSprite == null)
        {
            return;
        }

        SpriteRenderer originalRenderer = GetComponent<SpriteRenderer>();
        if (originalRenderer != null)
        {
            originalRenderer.enabled = false;
        }

        GameObject visualObject = new GameObject("ChibiCharacterArtwork");
        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = visualOffset;

        float parentScale = Mathf.Abs(transform.lossyScale.y);
        float localScale = targetWorldHeight /
                           (worldSprite.bounds.size.y * Mathf.Max(parentScale, 0.01f));
        visualObject.transform.localScale = new Vector3(localScale, localScale, 1f);

        visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = worldSprite;
        visualRenderer.sortingOrder = sortingOrder;

        visualTransform = visualObject.transform;
        baseLocalPosition = visualTransform.localPosition;
        baseLocalScale = visualTransform.localScale;
        lastWorldPosition = transform.position;
    }

    private void Update()
    {
        if (visualTransform == null)
        {
            return;
        }

        Vector3 worldDelta = transform.position - lastWorldPosition;
        bool isMoving = worldDelta.sqrMagnitude > 0.000001f;
        lastWorldPosition = transform.position;

        float speed = isMoving ? walkCyclesPerSecond : 1.2f;
        animationTime += Time.deltaTime * speed * Mathf.PI * 2f;

        float bounce = isMoving
            ? Mathf.Abs(Mathf.Sin(animationTime)) * walkBounceHeight
            : Mathf.Sin(animationTime) * idleBounceHeight;
        float squash = isMoving
            ? Mathf.Abs(Mathf.Sin(animationTime)) * 0.06f
            : 0f;

        visualTransform.localPosition =
            baseLocalPosition + new Vector3(0f, bounce, 0f);

        float facing = visualTransform.localScale.x < 0f ? -1f : 1f;
        if (Mathf.Abs(worldDelta.x) > 0.0001f)
        {
            facing = worldDelta.x < 0f ? -1f : 1f;
        }

        visualTransform.localScale = new Vector3(
            Mathf.Abs(baseLocalScale.x) * facing * (1f + squash),
            baseLocalScale.y * (1f - squash),
            baseLocalScale.z
        );

        // Lower characters render in front, matching a top-down room.
        visualRenderer.sortingOrder =
            sortingOrder - Mathf.RoundToInt(transform.position.y * 10f);
    }
}
