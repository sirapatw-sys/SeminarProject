using UnityEngine;

/// <summary>
/// Added at runtime to the scene's AudioListener by
/// <see cref="AudioOutputRouter"/>. A filter on the listener receives the
/// game's final mix, which is where the master volume and the chosen output
/// device are applied.
/// </summary>
[DisallowMultipleComponent]
public class AudioListenerTap : MonoBehaviour
{
    private void OnAudioFilterRead(float[] data, int channels)
    {
        AudioOutputRouter.Process(data, channels);
    }
}
