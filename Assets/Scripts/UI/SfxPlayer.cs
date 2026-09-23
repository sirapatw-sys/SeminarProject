using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Interaction sounds and room ambience, synthesised at start-up so the
/// project does not depend on audio files nobody has recorded yet. Swap a cue
/// for a real clip by dropping it at Resources/Audio/&lt;Cue&gt; (for example
/// Resources/Audio/Item.wav), or Resources/Audio/Room03Ambience for the drone.
/// </summary>
public class SfxPlayer : MonoBehaviour
{
    public enum Cue
    {
        Interact,
        Success,
        Locked,
        Item,
        Talk,
        EventPing,
        Scare,
        Save,
    }

    private const int SampleRate = 44100;

    private static SfxPlayer instance;

    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;

    private readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
    private AudioSource effects;
    private AudioSource ambience;
    private AudioClip hauntedAmbience;
    private float lastTalkTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("SfxPlayer");
        instance = host.AddComponent<SfxPlayer>();
        DontDestroyOnLoad(host);
    }

    public static void Play(Cue cue)
    {
        if (instance == null)
        {
            return;
        }

        instance.PlayCue(cue);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        ambience = gameObject.AddComponent<AudioSource>();
        ambience.playOnAwake = false;
        ambience.loop = true;

        BuildClips();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Room03 is the haunted one; the other rooms stay quiet so the drone
        // lands as a change of mood when the player walks in.
        if (scene.name == "Room03")
        {
            if (ambience.clip != hauntedAmbience || !ambience.isPlaying)
            {
                ambience.clip = hauntedAmbience;
                ambience.volume = ambienceVolume;
                ambience.Play();
            }
        }
        else if (ambience.isPlaying)
        {
            ambience.Stop();
        }
    }

    private void PlayCue(Cue cue)
    {
        if (cue == Cue.Talk)
        {
            // A blip per line, not per frame of a fast click-through.
            if (Time.unscaledTime - lastTalkTime < 0.08f)
            {
                return;
            }
            lastTalkTime = Time.unscaledTime;
        }

        AudioClip clip;
        if (clips.TryGetValue(cue, out clip) && clip != null)
        {
            effects.pitch = cue == Cue.Talk ? Random.Range(0.94f, 1.08f) : 1f;
            effects.PlayOneShot(clip, effectsVolume);
        }
    }

    private void BuildClips()
    {
        clips[Cue.Interact] = Tone("sfx_interact", 0.07f, t => Env(t, 0.004f, 0.07f) * Square(t, 660f) * 0.18f);
        clips[Cue.Success] = Tone("sfx_success", 0.45f, t =>
            Env(t, 0.005f, 0.2f) * Sine(t, 523f) * 0.35f +
            Env(t - 0.1f, 0.005f, 0.3f) * Sine(t, 784f) * 0.35f);
        clips[Cue.Locked] = Tone("sfx_locked", 0.25f, t =>
            Env(t, 0.002f, 0.2f) * (Sine(t, 90f) * 0.6f + Noise() * 0.12f));
        clips[Cue.Item] = Tone("sfx_item", 0.6f, t =>
            Env(t, 0.004f, 0.15f) * Sine(t, 880f) * 0.25f +
            Env(t - 0.08f, 0.004f, 0.15f) * Sine(t, 1175f) * 0.25f +
            Env(t - 0.16f, 0.004f, 0.35f) * Sine(t, 1568f) * 0.25f);
        clips[Cue.Talk] = Tone("sfx_talk", 0.05f, t => Env(t, 0.003f, 0.05f) * Triangle(t, 420f) * 0.2f);
        clips[Cue.EventPing] = Tone("sfx_event", 0.3f, t =>
            Env(t, 0.004f, 0.12f) * Sine(t, 988f) * 0.25f +
            Env(t - 0.12f, 0.004f, 0.16f) * Sine(t, 1319f) * 0.25f);
        clips[Cue.Scare] = Tone("sfx_scare", 1.4f, t =>
            Env(t, 0.02f, 1.3f) * (Sine(t, 55f + 30f * t) * 0.4f +
                                   Sine(t, 233f + 7f * Mathf.Sin(t * 40f)) * 0.18f +
                                   Noise() * 0.08f));
        clips[Cue.Save] = Tone("sfx_save", 0.25f, t =>
            Env(t, 0.004f, 0.2f) * Sine(t, 660f + 400f * t) * 0.25f);

        foreach (Cue cue in System.Enum.GetValues(typeof(Cue)))
        {
            AudioClip recorded = Resources.Load<AudioClip>("Audio/" + cue);
            if (recorded != null)
            {
                clips[cue] = recorded;
            }
        }

        float[] times = { 1.5f, 5.2f, 5.6f, 9.1f };
        float[] notes = { 1046.5f, 1318.5f, 1174.7f, 987.8f };
        hauntedAmbience = Tone("amb_haunted", 12f, t =>
        {
            // Two detuned low drones beating slowly, a breath of noise, and a
            // far-off music-box note every few seconds.
            float swell = 0.6f + 0.4f * Mathf.Sin(t * 0.52f);
            float drone = Sine(t, 55f) * 0.25f + Sine(t, 55.7f) * 0.25f + Sine(t, 82.4f) * 0.08f;
            float breath = Noise() * 0.03f * (0.5f + 0.5f * Mathf.Sin(t * 0.9f));
            float chime = 0f;
            for (int i = 0; i < times.Length; i++)
            {
                chime += Env(t - times[i], 0.003f, 1.6f) * Sine(t, notes[i]) * 0.05f;
            }
            return (drone * swell + breath + chime) * 0.8f;
        }, fadeEdges: true);

        AudioClip recordedAmbience = Resources.Load<AudioClip>("Audio/Room03Ambience");
        if (recordedAmbience != null)
        {
            hauntedAmbience = recordedAmbience;
        }
    }

    private static readonly System.Random noise = new System.Random(7);

    private static float Noise()
    {
        return (float)(noise.NextDouble() * 2.0 - 1.0);
    }

    private static float Sine(float t, float hz)
    {
        return Mathf.Sin(2f * Mathf.PI * hz * t);
    }

    private static float Square(float t, float hz)
    {
        return Sine(t, hz) >= 0f ? 1f : -1f;
    }

    private static float Triangle(float t, float hz)
    {
        float phase = t * hz - Mathf.Floor(t * hz);
        return 4f * Mathf.Abs(phase - 0.5f) - 1f;
    }

    /// <summary>Linear attack then exponential-ish decay; zero before t = 0.</summary>
    private static float Env(float t, float attack, float decay)
    {
        if (t < 0f)
        {
            return 0f;
        }

        if (t < attack)
        {
            return t / attack;
        }

        return Mathf.Exp(-(t - attack) / Mathf.Max(decay * 0.35f, 0.001f));
    }

    private static AudioClip Tone(
        string clipName,
        float seconds,
        System.Func<float, float> wave,
        bool fadeEdges = false)
    {
        int count = Mathf.CeilToInt(seconds * SampleRate);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float sample = wave(t);
            if (fadeEdges)
            {
                // Crossfade-friendly loop edges so the drone does not click.
                float edge = Mathf.Min(t, seconds - t);
                sample *= Mathf.Clamp01(edge / 0.4f);
            }
            data[i] = Mathf.Clamp(sample, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
