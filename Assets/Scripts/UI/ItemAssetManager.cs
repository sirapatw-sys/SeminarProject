using System;
using System.Collections.Generic;
using MysteryGame.Core;
using UnityEngine;

public class ItemAssetManager : MonoBehaviour
{
    public static ItemAssetManager Instance { get; private set; }
    private static readonly Dictionary<string, Sprite[]> cache =
        new Dictionary<string, Sprite[]>(StringComparer.OrdinalIgnoreCase);
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public static Sprite[] GetItemFrames(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        ItemData item = ItemData.Find(itemId);
        if (item == null || item.image == null) return null;
        string cacheKey = item.GetInstanceID().ToString();
        Sprite[] found;
        if (cache.TryGetValue(cacheKey, out found)) return found;
        Sprite source = item.image;
        int count = Mathf.Max(1, item.horizontalFrames);
        if (count == 1) return cache[cacheKey] = new[] { source };
        Rect region = source.rect;
        float width = region.width / count;
        found = new Sprite[count];
        for (int i = 0; i < count; i++)
            found[i] = Sprite.Create(source.texture,
                new Rect(region.x + i * width, region.y, width, region.height),
                new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        return cache[cacheKey] = found;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        foreach (var frames in cache.Values)
            foreach (var frame in frames)
                if (frame != null && frame.name == string.Empty) Destroy(frame);
        cache.Clear();
    }
}
