using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomVisualController : MonoBehaviour
{
    public static RoomVisualController Instance { get; private set; }

    [Header("Room Artwork")]
    [SerializeField] private Sprite roomBackground;
    [SerializeField] private Sprite room01Background;
    [SerializeField] private Sprite room02Background;
    [SerializeField] private Sprite room03Background;
    [SerializeField] private bool hideSimpleRoomSprites = true;
    [SerializeField] private Color tint = Color.white;

    private GameObject currentBackgroundObject;
    private RoomKnowledgeData currentRoom;

    // A room whose art changes mid-play (Room03 once the ghost is calmed)
    // fades the new picture in over the old one.
    private const float BackgroundFadeSeconds = 2f;
    private SpriteRenderer fadingIn;
    private float fadeStartedAt;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // If another instance exists from previous scene, destroy this duplicate
            // but ensure the existing instance refreshes for this scene
            Destroy(gameObject);
            Instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start()
    {
        SetupRoomVisuals(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupRoomVisuals(scene.name);
    }

    public void SetupRoomVisuals(string sceneName)
    {
        Camera targetCamera = Camera.main;
        if (targetCamera == null)
        {
            return;
        }

        // Clean up previous background object if destroyed with old scene
        if (currentBackgroundObject != null)
        {
            Destroy(currentBackgroundObject);
            currentBackgroundObject = null;
        }

        // A room owns its own background through its knowledge asset. The
        // per-scene fields below are only a fallback, and they are easy to get
        // wrong: this component survives scene loads, so whichever scene ran
        // first would otherwise decide every later room's artwork.
        Sprite selectedSprite = null;
        RoomKnowledgeData knowledge = KnowledgeLibrary.GetRoom(sceneName);
        currentRoom = knowledge;
        fadingIn = null;
        if (knowledge != null && knowledge.background != null)
        {
            selectedSprite = knowledge.BackgroundFor(GameState.Instance);
        }

        if (selectedSprite != null)
        {
            BuildBackground(selectedSprite, targetCamera);
            if (hideSimpleRoomSprites)
            {
                HideSimpleRoomSprites();
            }
            return;
        }

        selectedSprite = roomBackground;
        if (sceneName == "Room01" && room01Background != null)
        {
            selectedSprite = room01Background;
        }
        else if (sceneName == "Room02" && room02Background != null)
        {
            selectedSprite = room02Background;
        }
        else if (sceneName == "Room03" && room03Background != null)
        {
            selectedSprite = room03Background;
        }

        if (selectedSprite == null)
        {
            selectedSprite = roomBackground;
        }

        if (selectedSprite != null)
        {
            BuildBackground(selectedSprite, targetCamera);
        }

        if (hideSimpleRoomSprites)
        {
            HideSimpleRoomSprites();
        }
    }

    private void BuildBackground(Sprite selectedSprite, Camera targetCamera)
    {
        {
            currentBackgroundObject = new GameObject("IllustratedRoomBackground");
            SpriteRenderer renderer = currentBackgroundObject.AddComponent<SpriteRenderer>();
            renderer.sprite = selectedSprite;
            renderer.color = tint;
            renderer.sortingOrder = -100;

            currentBackgroundObject.transform.position = new Vector3(
                targetCamera.transform.position.x,
                targetCamera.transform.position.y,
                0f
            );

            // The room always occupies the same world rectangle, because the
            // colliders are placed in world units against the painting. It
            // used to be scaled to cover the screen instead, which on any
            // non-16:9 window slid the art away from its hitboxes.
            float scale = Mathf.Min(
                RoomWorldWidth / selectedSprite.bounds.size.x,
                RoomWorldHeight / selectedSprite.bounds.size.y
            );
            currentBackgroundObject.transform.localScale = new Vector3(scale, scale, 1f);
        }

        FitCamera(targetCamera);
    }

    /// <summary>World size of every room background (1920x1080 at 108 px per unit).</summary>
    public const float RoomWorldWidth = 17.7778f;
    public const float RoomWorldHeight = 10f;

    private float fittedAspect;

    /// <summary>
    /// Shows the whole room on any window shape: wide screens get dark bars
    /// at the sides, tall ones above and below, and nothing is ever cropped.
    /// </summary>
    private void FitCamera(Camera targetCamera)
    {
        if (targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        fittedAspect = targetCamera.aspect;
        targetCamera.orthographicSize = Mathf.Max(
            RoomWorldHeight * 0.5f,
            RoomWorldWidth * 0.5f / Mathf.Max(fittedAspect, 0.1f)
        );
        targetCamera.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1f);
        targetCamera.clearFlags = CameraClearFlags.SolidColor;
    }

    private void LateUpdate()
    {
        Camera targetCamera = Camera.main;
        if (targetCamera != null && !Mathf.Approximately(targetCamera.aspect, fittedAspect))
        {
            FitCamera(targetCamera);
        }

        UpdateChangedBackground();
    }

    /// <summary>Fades to the room's changed picture once its flag is set.</summary>
    private void UpdateChangedBackground()
    {
        if (currentRoom == null || currentRoom.changedBackground == null || currentBackgroundObject == null)
        {
            return;
        }

        SpriteRenderer shown = currentBackgroundObject.GetComponent<SpriteRenderer>();
        if (fadingIn != null)
        {
            float t = Mathf.Clamp01((Time.time - fadeStartedAt) / BackgroundFadeSeconds);
            fadingIn.color = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f)
            {
                shown.sprite = fadingIn.sprite;
                Destroy(fadingIn.gameObject);
                fadingIn = null;
            }

            return;
        }

        Sprite wanted = currentRoom.BackgroundFor(GameState.Instance);
        if (wanted == null || shown == null || shown.sprite == wanted)
        {
            return;
        }

        GameObject overlay = new GameObject("IllustratedRoomBackgroundChange");
        overlay.transform.SetParent(currentBackgroundObject.transform, false);
        fadingIn = overlay.AddComponent<SpriteRenderer>();
        fadingIn.sprite = wanted;
        fadingIn.sortingOrder = shown.sortingOrder + 1;
        fadingIn.color = new Color(tint.r, tint.g, tint.b, 0f);
        fadeStartedAt = Time.time;
    }

    /// <summary>
    /// The illustrated background already draws the room, so every prop under
    /// the Environment/Interactables roots is just a collider carrier and its
    /// placeholder sprite must not be drawn on top of the artwork.
    /// Keyed off the scene root instead of a hard-coded name list so new props
    /// do not have to be registered here.
    /// </summary>
    public static void HideSimpleRoomSprites()
    {
        SpriteRenderer[] renderers = FindObjectsOfType<SpriteRenderer>();
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.gameObject == null)
            {
                continue;
            }

            string objectName = renderer.gameObject.name;
            // Never hide the full room background!
            if (objectName == "IllustratedRoomBackground" ||
                objectName == "RoomBackground")
            {
                continue;
            }

            string rootName = renderer.transform.root.name;
            if (rootName == "Environment" || rootName == "Interactables")
            {
                renderer.enabled = false;
            }
        }
    }
}
