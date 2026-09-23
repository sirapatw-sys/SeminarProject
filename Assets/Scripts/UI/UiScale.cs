using UnityEngine;

/// <summary>
/// Makes the IMGUI overlays (settings, item popup, keypad, intro, room
/// transitions, title menu) resolution independent. They were laid out in
/// pixels for 1080p, so on a 4K screen they shrank to a quarter and at 720p
/// they spilled off screen. Call <see cref="Apply"/> at the top of OnGUI and
/// lay out against <see cref="Width"/>/<see cref="Height"/> instead of
/// Screen.width/height; the canvas UI already scales through CanvasScaler.
/// </summary>
public static class UiScale
{
    public const float ReferenceHeight = 1080f;

    public static float Factor
    {
        get
        {
            // Width-limited screens (portrait windows, very narrow editors)
            // scale by width instead so nothing is cut off at the sides.
            float byHeight = Screen.height / ReferenceHeight;
            float byWidth = Screen.width / 1920f;
            return Mathf.Clamp(Mathf.Min(byHeight, byWidth * 1.35f), 0.5f, 3f);
        }
    }

    /// <summary>Virtual screen width in reference pixels.</summary>
    public static float Width
    {
        get { return Screen.width / Factor; }
    }

    /// <summary>Virtual screen height in reference pixels.</summary>
    public static float Height
    {
        get { return Screen.height / Factor; }
    }

    public static void Apply()
    {
        float factor = Factor;
        GUI.matrix = Matrix4x4.Scale(new Vector3(factor, factor, 1f));
    }
}
