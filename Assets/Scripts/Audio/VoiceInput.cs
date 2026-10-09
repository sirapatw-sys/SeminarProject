using System;
using UnityEngine;

/// <summary>
/// The player's microphone: records one utterance for voice typing, or
/// just listens so the settings panel can show a level meter.
///
/// Uses the input device chosen in the settings (Windows default when none
/// or when that device is gone) and applies the microphone volume
/// (<see cref="AudioPrefs.MicGain"/>) to what it hands back.
/// </summary>
public class VoiceInput : MonoBehaviour
{
    /// <summary>Longest single recording; recording stops by itself here.</summary>
    public const int MaxSeconds = 30;

    private const int PreferredRate = 16000;

    private static VoiceInput instance;

    /// <summary>Raised when a recording reaches <see cref="MaxSeconds"/> and stops taking audio.</summary>
    public static event Action LimitReached;

    private AudioClip clip;
    private string device;
    private bool recording;
    private bool monitoring;
    private bool limitRaised;
    private float startedAt;
    private float level;
    private readonly float[] window = new float[1024];

    public static bool IsRecording { get { return instance != null && instance.recording; } }
    public static bool IsMonitoring { get { return instance != null && instance.monitoring; } }

    /// <summary>Peak of the last few milliseconds after the microphone volume, 0..1+.</summary>
    public static float Level { get { return instance != null ? instance.level : 0f; } }

    public static float Elapsed
    {
        get { return instance != null && instance.recording ? Time.unscaledTime - instance.startedAt : 0f; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        LimitReached = null;
    }

    /// <summary>Microphones Unity can open, by name.</summary>
    public static string[] Devices
    {
        get { return Microphone.devices; }
    }

    /// <summary>The chosen microphone if it is still plugged in, otherwise null (Windows default).</summary>
    public static string ResolveDevice()
    {
        string wanted = AudioPrefs.InputDevice;
        if (!string.IsNullOrEmpty(wanted) && Array.IndexOf(Microphone.devices, wanted) >= 0)
        {
            return wanted;
        }

        return null;
    }

    public static bool TryStartRecording(out string error)
    {
        VoiceInput voice = Ensure();
        voice.StopMonitorInternal();
        if (voice.recording)
        {
            error = null;
            return true;
        }

        if (!voice.Open(false, MaxSeconds, out error))
        {
            return false;
        }

        voice.recording = true;
        voice.limitRaised = false;
        voice.startedAt = Time.unscaledTime;
        return true;
    }

    /// <summary>
    /// Ends the recording and returns it as mono samples with the microphone
    /// volume applied, or null when nothing was captured.
    /// </summary>
    public static float[] StopRecording(out int sampleRate)
    {
        sampleRate = 0;
        if (instance == null || !instance.recording)
        {
            return null;
        }

        VoiceInput voice = instance;
        voice.recording = false;
        AudioClip captured = voice.clip;
        int position = Microphone.IsRecording(voice.device)
            ? Microphone.GetPosition(voice.device)
            : captured != null ? captured.samples : 0;
        Microphone.End(voice.device);
        voice.clip = null;
        voice.level = 0f;
        if (captured == null || position <= 0)
        {
            Discard(captured);
            return null;
        }

        int channels = Mathf.Max(1, captured.channels);
        float[] raw = new float[position * channels];
        captured.GetData(raw, 0);
        sampleRate = captured.frequency;
        Discard(captured);

        float gain = AudioPrefs.MicGain;
        float[] mono = new float[position];
        for (int frame = 0; frame < position; frame++)
        {
            float sum = 0f;
            for (int channel = 0; channel < channels; channel++)
            {
                sum += raw[frame * channels + channel];
            }

            mono[frame] = Mathf.Clamp(sum / channels * gain, -1f, 1f);
        }

        return mono;
    }

    /// <summary>Drops a recording without using it (the conversation closed).</summary>
    public static void CancelRecording()
    {
        int ignored;
        StopRecording(out ignored);
    }

    /// <summary>Listens without keeping anything, for the level meter in the settings.</summary>
    public static void StartMonitor()
    {
        VoiceInput voice = Ensure();
        if (voice.recording || voice.monitoring)
        {
            return;
        }

        string error;
        voice.monitoring = voice.Open(true, 1, out error);
        if (!voice.monitoring && !string.IsNullOrEmpty(error))
        {
            Debug.LogWarning(error);
        }
    }

    public static void StopMonitor()
    {
        if (instance != null)
        {
            instance.StopMonitorInternal();
        }
    }

    private static VoiceInput Ensure()
    {
        if (instance == null)
        {
            GameObject host = new GameObject("VoiceInput");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<VoiceInput>();
        }

        return instance;
    }

    private bool Open(bool loop, int seconds, out string error)
    {
        error = null;
        if (Microphone.devices.Length == 0)
        {
            error = "ไม่พบไมโครโฟน — เสียบไมค์ แล้วตรวจว่า Windows อนุญาตให้แอปใช้ไมโครโฟน (Settings > Privacy > Microphone)";
            return false;
        }

        device = ResolveDevice();
        int minimum, maximum;
        Microphone.GetDeviceCaps(device, out minimum, out maximum);
        int rate = minimum == 0 && maximum == 0 ? PreferredRate : Mathf.Clamp(PreferredRate, minimum, maximum);
        clip = Microphone.Start(device, loop, seconds, rate);
        if (clip == null)
        {
            error = "เปิดไมโครโฟนไม่ได้ — ลองเลือกไมค์ตัวอื่นในหน้าตั้งค่า > เสียง";
            return false;
        }

        level = 0f;
        return true;
    }

    private void StopMonitorInternal()
    {
        if (!monitoring)
        {
            return;
        }

        monitoring = false;
        Microphone.End(device);
        Discard(clip);
        clip = null;
        level = 0f;
    }

    private void Update()
    {
        if (monitoring && ResolveDevice() != device)
        {
            // The player picked another microphone while the meter runs.
            StopMonitorInternal();
            StartMonitor();
        }

        if ((!recording && !monitoring) || clip == null)
        {
            return;
        }

        if (recording && !limitRaised && !Microphone.IsRecording(device))
        {
            limitRaised = true;
            if (LimitReached != null) LimitReached();
            return;
        }

        int position = Microphone.GetPosition(device);
        int channels = Mathf.Max(1, clip.channels);
        int frames = Mathf.Min(window.Length / channels, position);
        float peak = 0f;
        if (frames > 0)
        {
            float[] samples = frames * channels == window.Length ? window : new float[frames * channels];
            clip.GetData(samples, position - frames);
            for (int i = 0; i < samples.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
        }

        peak *= AudioPrefs.MicGain;
        // Jump up at once, fall back slowly, like a VU meter.
        level = Mathf.Max(peak, level - 1.6f * Time.unscaledDeltaTime);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            if (recording || monitoring)
            {
                Microphone.End(device);
            }
            instance = null;
        }
    }

    private static void Discard(AudioClip captured)
    {
        if (captured != null)
        {
            Destroy(captured);
        }
    }
}
