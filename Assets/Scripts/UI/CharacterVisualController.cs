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

    [Header("4-Directional Walk Animation (Optional)")]
    // Off means worldSprite is drawn with the procedural bounce instead of
    // the 8-frame cycle, which is what a character with no walk sheet wants.
    [SerializeField] private bool useWalkFrames = true;
    [SerializeField] private Sprite[] walkRightFrames;
    [SerializeField] private Sprite[] walkLeftFrames;
    [SerializeField] private Sprite[] walkDownFrames;
    [SerializeField] private Sprite[] walkUpFrames;
    [SerializeField] private Sprite idleSprite;
    [SerializeField, Min(1f)] private float animationFps = 10f;
    [SerializeField] private bool initialFacingLeft = false;

    private Transform visualTransform;
    private SpriteRenderer visualRenderer;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private Vector3 lastWorldPosition;
    private Vector3 lastMoveDelta;
    private float movingTimer;
    private float animationTime;
    private float frameTimer;
    private int currentFrameIndex;
    private PlayerMovement playerMovement;

    public enum Direction { Down, Up, Left, Right }
    private Direction currentDirection = Direction.Down;

    public bool HasWalkFrames
    {
        get
        {
            if (!useWalkFrames)
            {
                return false;
            }

            return (walkRightFrames != null && walkRightFrames.Length > 0) ||
                   (walkLeftFrames != null && walkLeftFrames.Length > 0) ||
                   (walkDownFrames != null && walkDownFrames.Length > 0) ||
                   (walkUpFrames != null && walkUpFrames.Length > 0);
        }
    }

    private void Start()
    {
        TryAutoLoadPlayerFrames();

        playerMovement = GetComponent<PlayerMovement>();

        Sprite refSprite = null;
        if (HasWalkFrames && walkDownFrames != null && walkDownFrames.Length > 0 && walkDownFrames[0] != null)
        {
            refSprite = walkDownFrames[0];
        }
        else if (worldSprite != null)
        {
            refSprite = worldSprite;
        }
        else if (idleSprite != null)
        {
            refSprite = idleSprite;
        }

        if (refSprite == null)
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

        float parentScaleX = Mathf.Abs(transform.lossyScale.x);
        float parentScaleY = Mathf.Abs(transform.lossyScale.y);
        float localScaleX = targetWorldHeight /
                           (refSprite.bounds.size.y * Mathf.Max(parentScaleX, 0.01f));
        float localScaleY = targetWorldHeight /
                           (refSprite.bounds.size.y * Mathf.Max(parentScaleY, 0.01f));
        visualObject.transform.localScale = new Vector3(localScaleX, localScaleY, 1f);

        visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        visualRenderer.sprite = refSprite;
        visualRenderer.sortingOrder = sortingOrder;

        visualTransform = visualObject.transform;
        baseLocalPosition = visualTransform.localPosition;
        baseLocalScale = visualTransform.localScale;
        lastWorldPosition = transform.position;
    }

    private void TryAutoLoadPlayerFrames()
    {
        if (!useWalkFrames)
        {
            return;
        }

        if (HasWalkFrames)
        {
            return;
        }

        if (!gameObject.name.ToLowerInvariant().Contains("player"))
        {
            return;
        }

        walkRightFrames = LoadFrames("Right");
        walkLeftFrames = LoadFrames("Left");
        walkDownFrames = LoadFrames("Down");
        walkUpFrames = LoadFrames("Up");
    }

    private static Sprite[] LoadFrames(string direction)
    {
        Sprite[] frames = new Sprite[8];
        bool anyLoaded = false;
        for (int i = 0; i < 8; i++)
        {
            frames[i] = Resources.Load<Sprite>($"Player_Walk/Player_{direction}_{i}");
            if (frames[i] != null)
            {
                anyLoaded = true;
            }
        }
        return anyLoaded ? frames : null;
    }

    private Sprite[] GetFramesForDirection(Direction dir)
    {
        switch (dir)
        {
            case Direction.Right:
                return walkRightFrames ?? walkDownFrames;
            case Direction.Left:
                return walkLeftFrames ?? walkDownFrames;
            case Direction.Up:
                return walkUpFrames ?? walkDownFrames;
            case Direction.Down:
            default:
                return walkDownFrames ?? walkRightFrames ?? walkLeftFrames;
        }
    }

    private void Update()
    {
        if (visualTransform == null || visualRenderer == null)
        {
            return;
        }

        Vector3 worldDelta = transform.position - lastWorldPosition;
        lastWorldPosition = transform.position;

        Vector2 moveVector = Vector2.zero;
        bool isMoving = false;

        if (playerMovement != null)
        {
            moveVector = playerMovement.Movement;
            isMoving = playerMovement.IsMoving;
        }
        else
        {
            if (worldDelta.sqrMagnitude > 0.00001f)
            {
                lastMoveDelta = worldDelta;
                movingTimer = 0.08f;
            }
            else
            {
                movingTimer -= Time.deltaTime;
            }
            isMoving = movingTimer > 0f;
            moveVector = new Vector2(lastMoveDelta.x, lastMoveDelta.y);
        }

        if (HasWalkFrames)
        {
            UpdateWalkAnimation(moveVector, isMoving);
        }
        else
        {
            UpdateProceduralMotion(worldDelta, isMoving);
        }

        // Lower characters render in front, matching a top-down room.
        visualRenderer.sortingOrder =
            sortingOrder - Mathf.RoundToInt(transform.position.y * 10f);
    }

    private void UpdateWalkAnimation(Vector2 moveVector, bool isMoving)
    {
        if (isMoving)
        {
            if (Mathf.Abs(moveVector.x) > Mathf.Abs(moveVector.y))
            {
                currentDirection = moveVector.x > 0f ? Direction.Right : Direction.Left;
            }
            else if (Mathf.Abs(moveVector.y) > 0.0001f)
            {
                currentDirection = moveVector.y > 0f ? Direction.Up : Direction.Down;
            }

            frameTimer += Time.deltaTime * animationFps;
            if (frameTimer >= 1f)
            {
                frameTimer -= 1f;
                currentFrameIndex++;
            }

            Sprite[] activeFrames = GetFramesForDirection(currentDirection);
            if (activeFrames != null && activeFrames.Length > 0)
            {
                visualRenderer.sprite = activeFrames[currentFrameIndex % activeFrames.Length];
            }
        }
        else
        {
            currentFrameIndex = 0;
            frameTimer = 0f;

            Sprite[] activeFrames = GetFramesForDirection(currentDirection);
            if (activeFrames != null && activeFrames.Length > 0)
            {
                visualRenderer.sprite = activeFrames[0];
            }
            else if (idleSprite != null)
            {
                visualRenderer.sprite = idleSprite;
            }
        }

        visualTransform.localScale = baseLocalScale;
        visualTransform.localPosition = baseLocalPosition;
    }

    private void UpdateProceduralMotion(Vector3 worldDelta, bool isMoving)
    {
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
            if (initialFacingLeft)
            {
                facing = worldDelta.x > 0f ? -1f : 1f;
            }
            else
            {
                facing = worldDelta.x > 0f ? 1f : -1f;
            }
        }

        visualTransform.localScale = new Vector3(
            Mathf.Abs(baseLocalScale.x) * facing * (1f + squash),
            baseLocalScale.y * (1f - squash),
            baseLocalScale.z
        );
    }
}
