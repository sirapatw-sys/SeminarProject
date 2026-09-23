using System.Collections.Generic;
using MysteryGame.Knowledge;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Interaction sounds and room audio.
///
/// Room audio is data: each RoomKnowledgeData lists its looping layers
/// (music, rain, a ticking clock) with a volume, and SfxPlayer crossfades to
/// the new set whenever a room loads. The whole room mix is silent behind
/// the title menu, quiet during the intro narration, and full volume in
/// play; it also dips while a one-off piece such as the music box plays.
///
/// Short effects are synthesised at start-up so the project does not depend
/// on recordings. Swap a cue for a real clip by dropping it at
/// Resources/Audio/&lt;Cue&gt; (for example Resources/Audio/Item.wav).
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
    private const float CrossfadeSeconds = 1.6f;

    /// <summary>Room mix while the intro narration is on screen.</summary>
    public const float IntroMix = 0.3f;

    /// <summary>One-off music plays a little slow, which reads as eerie.</summary>
    private const float FeaturePitch = 0.9f;

    private static SfxPlayer instance;

    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float hauntedDroneVolume = 0.32f;

    private class Layer
    {
        public AudioSource source;
        public float volume;      // authored volume of this layer
        public float level;       // smoothed towards volume (or 0 when leaving)
        public bool leaving;
    }

    private readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
    private readonly List<Layer> layers = new List<Layer>();
    private AudioSource effects;
    private AudioSource feature;
    private AudioClip hauntedAmbience;
    private float lastTalkTime;
    private float roomMix;
    private float featureDuck = 1f;

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

    /// <summary>
    /// A one-off piece of music (the music box's lullaby). The room's loops
    /// dip underneath it until it ends.
    /// </summary>
    public static void PlayFeature(AudioClip clip, float volume)
    {
        if (instance == null || clip == null)
        {
            return;
        }

        instance.feature.Stop();
        instance.feature.clip = clip;
        instance.feature.volume = volume;
        instance.feature.Play();
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
        // One-off pieces play through a muffled, cavernous chain, a little
        // slow and slightly warbling (see Update), so the music box sounds
        // faint and wrong, like a worn-out box somewhere behind a wall.
        GameObject featureHost = new GameObject("FeatureMusic");
        featureHost.transform.SetParent(transform, false);
        feature = featureHost.AddComponent<AudioSource>();
        feature.playOnAwake = false;
        feature.pitch = FeaturePitch;
        AudioLowPassFilter muffle = featureHost.AddComponent<AudioLowPassFilter>();
        muffle.cutoffFrequency = 1300f;
        muffle.lowpassResonanceQ = 1.4f;
        AudioReverbFilter reverb = featureHost.AddComponent<AudioReverbFilter>();
        reverb.reverbPreset = AudioReverbPreset.Cave;

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
        RoomKnowledgeData room = KnowledgeLibrary.GetRoom(scene.name);
        List<KeyValuePair<AudioClip, float>> wanted = new List<KeyValuePair<AudioClip, float>>();
        if (room != null)
        {
            if (room.sounds != null)
            {
                foreach (RoomSound sound in room.sounds)
                {
                    if (sound != null && sound.clip != null)
                    {
                        wanted.Add(new KeyValuePair<AudioClip, float>(sound.clip, sound.volume));
                    }
                }
            }

            if (room.hauntedDrone)
            {
                wanted.Add(new KeyValuePair<AudioClip, float>(hauntedAmbience, hauntedDroneVolume));
            }
        }

        // Keep layers the new room shares with the old one (rain from room
        // to room does not restart), fade out the rest, fade in the new.
        foreach (Layer layer in layers)
        {
            int index = wanted.FindIndex(w => w.Key == layer.source.clip);
            if (index >= 0)
            {
                layer.volume = wanted[index].Value;
                layer.leaving = false;
                wanted.RemoveAt(index);
            }
            else
            {
                layer.leaving = true;
            }
        }

        foreach (KeyValuePair<AudioClip, float> entry in wanted)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.clip = entry.Key;
            source.volume = 0f;
            source.Play();
            layers.Add(new Layer { source = source, volume = entry.Value });
        }

        // A one-off piece belongs to the room it was played in.
        feature.Stop();
    }

    private void Update()
    {
        // Silent behind the title menu, quiet under the intro narration,
        // full volume once the player is in the room.
        float targetMix = TitleMenu.IsOpen ? 0f : IntroSequence.IsPlaying ? IntroMix : 1f;
        float rate = targetMix > roomMix ? 0.35f : 1.2f; // swell slowly, drop quickly
        roomMix = Mathf.MoveTowards(roomMix, targetMix, rate * Time.unscaledDeltaTime);

        float duckTarget = feature.isPlaying ? 0.6f : 1f;
        featureDuck = Mathf.MoveTowards(featureDuck, duckTarget, 0.8f * Time.unscaledDeltaTime);

        // A slow, uneven wow in the pitch: the tune sags and recovers like a
        // spring that is running down.
        if (feature.isPlaying)
        {
            float t = Time.unscaledTime;
            feature.pitch = FeaturePitch +
                            0.012f * Mathf.Sin(t * 2f * Mathf.PI * 0.21f) +
                            0.005f * Mathf.Sin(t * 2f * Mathf.PI * 0.53f);
        }

        for (int i = layers.Count - 1; i >= 0; i--)
        {
            Layer layer = layers[i];
            // Exponential glide: a new room's layer fades in, a shared layer
            // (rain) glides to the new room's level, a dropped one fades out.
            float target = layer.leaving ? 0f : layer.volume;
            float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f / CrossfadeSeconds);
            layer.level = Mathf.Lerp(layer.level, target, blend);
            layer.source.volume = layer.level * roomMix * featureDuck;

            if (layer.leaving && layer.level < 0.002f)
            {
                layer.source.Stop();
                Destroy(layer.source);
                layers.RemoveAt(i);
            }
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

        // Room03's drone: every partial completes a whole number of cycles in
        // the 12 s loop and the swell is one full period, so the loop point
        // is seamless instead of dipping to silence every 12 seconds.
        const float loop = 12f;
        float[] times = { 1.5f, 5.2f, 5.6f, 9.1f };
        float[] notes = { 1046.5f, 1318.5f, 1174.7f, 987.8f };
        hauntedAmbience = Tone("amb_haunted", loop, t =>
        {
            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * t / loop);
            float drone = Sine(t, 55f) * 0.25f + Sine(t, 55.75f) * 0.25f + Sine(t, 82.5f) * 0.08f;
            float chime = 0f;
            for (int i = 0; i < times.Length; i++)
            {
                chime += Env(t - times[i], 0.003f, 1.6f) * Sine(t, notes[i]) * 0.05f;
            }
            return (drone * swell + chime) * 0.8f;
        });
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

    private static AudioClip Tone(string clipName, float seconds, System.Func<float, float> wave)
    {
        int count = Mathf.CeilToInt(seconds * SampleRate);
        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            data[i] = Mathf.Clamp(wave(i / (float)SampleRate), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(clipName, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
