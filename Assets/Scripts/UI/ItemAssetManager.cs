using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ItemAssetManager : MonoBehaviour
{
    public static ItemAssetManager Instance { get; private set; }

    private static readonly Dictionary<string, Sprite[]> cachedItemSprites =
        new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Texture2D> cachedTextures =
        new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static Sprite[] GetItemFrames(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        if (cachedItemSprites.ContainsKey(itemId) && cachedItemSprites[itemId] != null)
        {
            return cachedItemSprites[itemId];
        }

        // Try loading from CopyPasteAssets or Assets/Art/Items
        Sprite[] loaded = TryLoadFromDisk(itemId);
        if (loaded != null && loaded.Length > 0)
        {
            cachedItemSprites[itemId] = loaded;
            return loaded;
        }

        return null;
    }

    private static Sprite[] TryLoadFromDisk(string itemId)
    {
        string[] searchDirectories = new string[]
        {
            Path.Combine(Application.dataPath, "..", "CopyPasteAssets"),
            Path.Combine(Application.dataPath, "Art", "Items"),
            Path.Combine(Application.dataPath, "Sprites", "Items")
        };

        foreach (string dir in searchDirectories)
        {
            if (!Directory.Exists(dir))
            {
                continue;
            }

            string[] files = Directory.GetFiles(dir, "*.png");
            foreach (string file in files)
            {
                string filename = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (filename.StartsWith(itemId.ToLowerInvariant()) ||
                    filename.Contains(itemId.ToLowerInvariant()))
                {
                    Sprite[] frames = LoadAndSliceSpriteSheet(file);
                    if (frames != null && frames.Length > 0)
                    {
                        return frames;
                    }
                }
            }
        }

        return null;
    }

    public static Sprite[] LoadAndSliceSpriteSheet(string filePath)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point; // Crisp pixel art
            texture.wrapMode = TextureWrapMode.Clamp;
            if (!texture.LoadImage(fileData))
            {
                return null;
            }

            int height = texture.height;
            int width = texture.width;

            // Detect frame count:
            // If width > height and divisible by height (e.g. 768 x 32 -> 24 frames of 32x32)
            int frameWidth = height;
            int frameHeight = height;
            int frameCount = 1;

            if (width > height && width % height == 0)
            {
                frameCount = width / height;
                frameWidth = height;
            }

            Sprite[] sprites = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                Rect rect = new Rect(i * frameWidth, 0, frameWidth, frameHeight);
                sprites[i] = Sprite.Create(
                    texture,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    frameHeight
                );
            }

            return sprites;
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to load item sprite from " + filePath + ": " + ex.Message);
            return null;
        }
    }
}
