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
        if (knowledge != null && knowledge.background != null)
        {
            selectedSprite = knowledge.background;
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

            float cameraHeight = targetCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * targetCamera.aspect;
            float scale = Mathf.Max(
                cameraWidth / selectedSprite.bounds.size.x,
                cameraHeight / selectedSprite.bounds.size.y
            );
            currentBackgroundObject.transform.localScale = new Vector3(scale, scale, 1f);
        }
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
