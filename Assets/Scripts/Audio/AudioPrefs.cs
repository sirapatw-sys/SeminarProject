using System;
using UnityEngine;

/// <summary>
/// The player's sound settings (the "เสียง" tab of the settings panel),
/// kept in PlayerPrefs.
///
/// Output: <see cref="Master"/> scales everything the game plays (applied
/// on the listener by <see cref="AudioOutputRouter"/>), <see cref="Music"/>
/// the room loops and the music box, <see cref="Effects"/> the one-off
/// sounds (doors, items, the room's random noises).
/// Input: <see cref="MicGain"/> scales the player's voice before it is
/// transcribed. Device choices are stored by id/name; empty means the
/// Windows default.
/// </summary>
public static class AudioPrefs
{
    private const string MasterKey = "audio.master";
    private const string MusicKey = "audio.music";
    private const string EffectsKey = "audio.effects";
    private const string MicGainKey = "audio.micGain";
    private const string OutputKey = "audio.outputDevice";
    private const string InputKey = "audio.inputDevice";

    /// <summary>Loudest the microphone can be boosted (300%).</summary>
    public const float MaxMicGain = 3f;

    /// <summary>Raised on the main thread whenever a setting changes.</summary>
    public static event Action Changed;

    private static bool loaded;
    private static float master = 1f;
    private static float music = 1f;
    private static float effects = 1f;
    private static float micGain = 1f;
    private static string outputDevice = string.Empty;
    private static string inputDevice = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }

    public static float Master
    {
        get { Load(); return master; }
        set { SetFloat(ref master, Mathf.Clamp01(value), MasterKey); }
    }

    public static float Music
    {
        get { Load(); return music; }
        set { SetFloat(ref music, Mathf.Clamp01(value), MusicKey); }
    }

    public static float Effects
    {
        get { Load(); return effects; }
        set { SetFloat(ref effects, Mathf.Clamp01(value), EffectsKey); }
    }

    public static float MicGain
    {
        get { Load(); return micGain; }
        set { SetFloat(ref micGain, Mathf.Clamp(value, 0f, MaxMicGain), MicGainKey); }
    }

    /// <summary>Windows endpoint id of the speakers/headphones; empty = Windows default.</summary>
    public static string OutputDevice
    {
        get { Load(); return outputDevice; }
        set { SetString(ref outputDevice, value, OutputKey); }
    }

    /// <summary>Microphone name as Unity lists it; empty = Windows default.</summary>
    public static string InputDevice
    {
        get { Load(); return inputDevice; }
        set { SetString(ref inputDevice, value, InputKey); }
    }

    /// <summary>Writes the settings to disk (sliders only update PlayerPrefs in memory).</summary>
    public static void Save()
    {
        PlayerPrefs.Save();
    }

    private static void Load()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        master = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
        music = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
        effects = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 1f));
        micGain = Mathf.Clamp(PlayerPrefs.GetFloat(MicGainKey, 1f), 0f, MaxMicGain);
        outputDevice = PlayerPrefs.GetString(OutputKey, string.Empty);
        inputDevice = PlayerPrefs.GetString(InputKey, string.Empty);
    }

    private static void SetFloat(ref float field, float value, string key)
    {
        Load();
        if (Mathf.Approximately(field, value))
        {
            return;
        }

        field = value;
        PlayerPrefs.SetFloat(key, value);
        if (Changed != null) Changed();
    }

    private static void SetString(ref string field, string value, string key)
    {
        Load();
        value = value ?? string.Empty;
        if (field == value)
        {
            return;
        }

        field = value;
        PlayerPrefs.SetString(key, value);
        PlayerPrefs.Save();
        if (Changed != null) Changed();
    }
}
