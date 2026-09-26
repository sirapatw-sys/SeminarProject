using UnityEngine;

/// <summary>
/// Shared pieces for the IMGUI modals (item card, keypad, AI settings).
///
/// A modal draws in front of every other overlay (<see cref="FrontDepth"/>),
/// its buttons act on the mouse press, and it swallows the clicks it does
/// not use (<see cref="BlockMouse"/>). Before this, clicking "ตกลง" on the
/// item card or the keypad sometimes did nothing and only Space worked:
/// GUI.Button fires only when the same control sees both the press and the
/// release, several OnGUI scripts shared one depth so another overlay could
/// take one of the two, and the keypad's shake moved its keys in between.
/// </summary>
public static class ModalGui
{
    /// <summary>GUI.depth for a modal: lower is in front and gets the mouse first.</summary>
    public const int FrontDepth = -20;

    /// <summary>
    /// A button that answers the mouse press itself. Drawn with the style's
    /// hover state under the mouse and its "on" state when <paramref name="on"/>
    /// (a selected tab). Does nothing while GUI.enabled is false.
    /// </summary>
    public static bool Button(Rect rect, string text, GUIStyle style, bool on = false)
    {
        return Button(rect, new GUIContent(text), style, on);
    }

    public static bool Button(Rect rect, GUIContent content, GUIStyle style, bool on = false)
    {
        Event e = Event.current;
        bool inside = rect.Contains(e.mousePosition);
        if (GUI.enabled && e.type == EventType.MouseDown && e.button == 0 && inside)
        {
            e.Use();
            return true;
        }

        if (e.type == EventType.Repaint)
        {
            // Draw enabled and fade by hand, so a disabled button looks the
            // same however the engine would have dimmed it.
            bool enabled = GUI.enabled;
            Color old = GUI.color;
            if (!enabled)
            {
                GUI.color = new Color(old.r, old.g, old.b, old.a * 0.45f);
            }

            GUI.enabled = true;
            style.Draw(rect, content, enabled && inside, false, on, false);
            GUI.enabled = enabled;
            GUI.color = old;
        }

        return false;
    }

    /// <summary>The same button placed by GUILayout.</summary>
    public static bool LayoutButton(string text, GUIStyle style, bool on, params GUILayoutOption[] options)
    {
        GUIContent content = new GUIContent(text);
        Rect rect = GUILayoutUtility.GetRect(content, style, options);
        return Button(rect, content, style, on);
    }

    /// <summary>
    /// Call last in a modal's OnGUI: every click and scroll it did not use
    /// stops here instead of reaching the HUD or the game behind it.
    /// </summary>
    public static void BlockMouse()
    {
        Event e = Event.current;
        if (e.isMouse || e.type == EventType.ScrollWheel)
        {
            e.Use();
        }
    }

    /// <summary>
    /// A rounded rectangle for a 9-sliced style background: set the style's
    /// border to <c>new RectOffset(radius, radius, radius, radius)</c>.
    /// </summary>
    public static Texture2D Rounded(Color fill, int radius, Color outline = default(Color), float outlineWidth = 0f)
    {
        int size = radius * 2 + 4;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        float half = size * 0.5f;
        float inner = half - radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance outside the rounded rectangle's inner box.
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - inner, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - inner, 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float coverage = Mathf.Clamp01(radius - distance + 0.5f);
                Color color = fill;
                if (outlineWidth > 0f)
                {
                    float edge = Mathf.Clamp01(distance - (radius - outlineWidth) + 0.5f);
                    color = Color.Lerp(fill, outline, edge);
                }

                color.a *= coverage;
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    public static Texture2D Solid(Color color)
    {
        Texture2D texture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
