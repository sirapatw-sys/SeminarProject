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
        Creak,
        Save,
    }

    private const int SampleRate = 44100;
    private const float CrossfadeSeconds = 1.6f;

    /// <summary>Room mix while the intro narration is on screen.</summary>
    public const float IntroMix = 0.3f;

    /// <summary>One-off music plays a little slow, which reads as eerie.</summary>
    private const float FeaturePitch = 0.9f;

    private static SfxPlayer instance;

    /// <summary>Raised when a random room noise starts (a creak, footsteps).</summary>
    public static event System.Action RoomNoisePlayed;

    [SerializeField, Range(0f, 1f)] private float effectsVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float hauntedDroneVolume = 0.42f;
    [Tooltip("Loudest a random room noise (creak, footsteps) gets; kept under the room's loops.")]
    [SerializeField, Range(0f, 1f)] private float roomNoiseVolume = 0.16f;
    [SerializeField] private Vector2 roomNoiseGapSeconds = new Vector2(9f, 22f);

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
    private readonly List<AudioClip> roomNoises = new List<AudioClip>();
    private AudioSource noiseSource;
    private bool roomHasNoises;
    private float nextNoiseTime;
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

        // Random room noises come from somewhere in the room, not from the
        // speakers: muffled a little and given a small room's reverb.
        GameObject noiseHost = new GameObject("RoomNoises");
        noiseHost.transform.SetParent(transform, false);
        noiseSource = noiseHost.AddComponent<AudioSource>();
        noiseSource.playOnAwake = false;
        AudioLowPassFilter noiseMuffle = noiseHost.AddComponent<AudioLowPassFilter>();
        noiseMuffle.cutoffFrequency = 2600f;
        AudioReverbFilter noiseRoom = noiseHost.AddComponent<AudioReverbFilter>();
        noiseRoom.reverbPreset = AudioReverbPreset.Room;

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

        roomHasNoises = room != null && room.hauntedDrone;
        nextNoiseTime = Time.unscaledTime + Random.Range(5f, 10f);

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

        if (roomHasNoises && roomMix > 0.5f && Time.unscaledTime >= nextNoiseTime)
        {
            PlayRoomNoise();
        }

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

    /// <summary>
    /// Somebody else is in the house: a floorboard, a few steps, something
    /// being moved. Always quiet, from a random side, never two at once.
    /// </summary>
    private void PlayRoomNoise()
    {
        nextNoiseTime = Time.unscaledTime + Random.Range(roomNoiseGapSeconds.x, roomNoiseGapSeconds.y);
        if (roomNoises.Count == 0 || noiseSource.isPlaying)
        {
            return;
        }

        noiseSource.clip = roomNoises[Random.Range(0, roomNoises.Count)];
        noiseSource.panStereo = Random.Range(-0.7f, 0.7f);
        noiseSource.pitch = Random.Range(0.92f, 1.06f);
        noiseSource.volume = roomNoiseVolume * Random.Range(0.6f, 1f) * roomMix;
        noiseSource.Play();

        if (RoomNoisePlayed != null)
        {
            RoomNoisePlayed();
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
        clips[Cue.Creak] = Creak("sfx_creak", 0.9f, 38f, 620f, 11);
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

        // Room03's slow throb: two low tones 0.75 Hz apart beat against each
        // other, a pulse a little slower than a heartbeat. No swell on top,
        // so the level stays put instead of fading in and out. Every partial
        // completes a whole number of cycles in the 12 s loop, so it loops
        // without a seam.
        const float loop = 12f;
        hauntedAmbience = Tone("amb_haunted", loop, t =>
            (Sine(t, 55f) * 0.25f + Sine(t, 55.75f) * 0.25f + Sine(t, 82.5f) * 0.08f) * 0.8f);

        roomNoises.Clear();
        roomNoises.Add(Footsteps("amb_steps_a", 3, 0.62f, 1));
        roomNoises.Add(Footsteps("amb_steps_b", 5, 0.55f, 2));
        roomNoises.Add(Creak("amb_creak_a", 1.1f, 32f, 540f, 3));
        roomNoises.Add(Creak("amb_creak_b", 0.7f, 55f, 760f, 4));
        roomNoises.Add(Knock("amb_knock", 5));
        roomNoises.Add(Scrape("amb_scrape", 6));
    }

    // ------------------------------------------------------------ room noises
    // Built sample by sample (they need filter state), each from its own
    // seed so the same noises come out every run.

    private static AudioClip Footsteps(string clipName, int steps, float gap, int seed)
    {
        System.Random rng = new System.Random(seed);
        float[] data = new float[Mathf.CeilToInt((steps * gap + 0.4f) * SampleRate)];
        for (int step = 0; step < steps; step++)
        {
            float jitter = 1f + ((float)rng.NextDouble() - 0.5f) * 0.12f;
            int start = Mathf.RoundToInt(step * gap * jitter * SampleRate);
            float loudness = Mathf.Pow(0.82f, step);          // walking away
            float low = 0f;
            for (int i = 0; start + i < data.Length && i < SampleRate / 4; i++)
            {
                float t = i / (float)SampleRate;
                // heel thud: a low body and lowpassed noise, then a soft scuff
                low += 0.08f * ((float)(rng.NextDouble() * 2.0 - 1.0) - low);
                float thud = Env(t, 0.003f, 0.09f) * (Sine(t, 72f) * 0.6f + low * 2.2f);
                float scuff = Env(t - 0.05f, 0.01f, 0.06f) * low * 0.8f;
                data[start + i] += (thud + scuff) * loudness;
            }
        }

        return Finish(clipName, data, 0.6f);
    }

    /// <summary>Stick-slip clicks ringing in wood: a floorboard or a chair.</summary>
    private static AudioClip Creak(string clipName, float seconds, float clicksPerSecond, float body, int seed)
    {
        System.Random rng = new System.Random(seed);
        float[] excite = new float[Mathf.CeilToInt(seconds * SampleRate)];
        float next = 0f;
        while (next < seconds)
        {
            float phase = next / seconds;
            float rate = clicksPerSecond * (0.7f + 0.6f * Mathf.Sin(phase * Mathf.PI));
            int index = Mathf.Min(excite.Length - 1, Mathf.RoundToInt(next * SampleRate));
            excite[index] += 0.6f + 0.4f * (float)rng.NextDouble();
            next += (1f / rate) * (0.75f + 0.5f * (float)rng.NextDouble());
        }

        float[] data = new float[excite.Length];
        Resonator wood = new Resonator();
        Resonator grain = new Resonator();
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float fade = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
            float bend = body * (1f + 0.08f * Mathf.Sin(t * 5f));   // the board flexes
            data[i] = (wood.Step(excite[i], bend, 9f) + grain.Step(excite[i], bend * 2.3f, 6f) * 0.4f) * fade;
        }

        return Finish(clipName, data, 0.5f);
    }

    /// <summary>Two dull knocks on wood, like something set down in another room.</summary>
    private static AudioClip Knock(string clipName, int seed)
    {
        System.Random rng = new System.Random(seed);
        float[] data = new float[Mathf.CeilToInt(0.8f * SampleRate)];
        foreach (float at in new[] { 0f, 0.34f })
        {
            int start = Mathf.RoundToInt(at * SampleRate);
            for (int i = 0; i < 40; i++)
            {
                data[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0) * (1f - i / 40f);
            }
        }

        Resonator low = new Resonator();
        Resonator high = new Resonator();
        for (int i = 0; i < data.Length; i++)
        {
            float x = data[i];
            data[i] = low.Step(x, 170f, 7f) + high.Step(x, 410f, 5f) * 0.5f;
        }

        return Finish(clipName, data, 0.55f);
    }

    /// <summary>Something heavy dragged a short way across the floor.</summary>
    private static AudioClip Scrape(string clipName, int seed)
    {
        System.Random rng = new System.Random(seed);
        const float seconds = 0.7f;
        float[] data = new float[Mathf.CeilToInt(seconds * SampleRate)];
        Resonator band = new Resonator();
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            float x = (float)(rng.NextDouble() * 2.0 - 1.0);
            float grit = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 23f);
            data[i] = band.Step(x * grit, Mathf.Lerp(900f, 520f, t / seconds), 4f) *
                      Mathf.Sin(Mathf.PI * t / seconds);
        }

        return Finish(clipName, data, 0.4f);
    }

    /// <summary>Two-pole band-pass (RBJ cookbook): the body a click or a hiss rings in.</summary>
    private class Resonator
    {
        private float x1, x2, y1, y2;

        public float Step(float x, float hz, float q)
        {
            float w0 = 2f * Mathf.PI * hz / SampleRate;
            float alpha = Mathf.Sin(w0) / (2f * q);
            float y = (alpha * x - alpha * x2 + 2f * Mathf.Cos(w0) * y1 - (1f - alpha) * y2) / (1f + alpha);
            x2 = x1;
            x1 = x;
            y2 = y1;
            y1 = y;
            return y;
        }
    }

    /// <summary>Scales to a fixed peak so every noise sits at a known level.</summary>
    private static AudioClip Finish(string clipName, float[] data, float peak)
    {
        float max = 0.0001f;
        foreach (float v in data)
        {
            max = Mathf.Max(max, Mathf.Abs(v));
        }

        for (int i = 0; i < data.Length; i++)
        {
            data[i] = data[i] / max * peak;
        }

        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
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
