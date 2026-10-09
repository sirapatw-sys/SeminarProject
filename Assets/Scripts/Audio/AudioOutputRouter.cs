#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#define AUDIO_ROUTER_WASAPI
#endif
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/// <summary>
/// Master volume and the choice of speakers or headphones.
///
/// Unity always plays through the Windows default device and has no API to
/// pick another one. So <see cref="AudioListenerTap"/>, a filter added to
/// the scene's AudioListener, sees the game's final mix: it scales the mix
/// by the master volume, and when the player picked a device it hands the
/// mix to a WASAPI stream on that device and silences Unity's own output.
/// Until the stream is running, and again if the device cannot be opened or
/// is unplugged, the game simply keeps playing through the Windows default
/// and <see cref="Status"/> says why.
/// </summary>
public class AudioOutputRouter : MonoBehaviour
{
    public struct Device
    {
        public string id;
        public string name;
    }

    private static AudioOutputRouter instance;

    // Read on the audio thread.
    private static volatile float masterGain = 1f;
#if AUDIO_ROUTER_WASAPI
    private static volatile WasapiStream stream;
#endif

    /// <summary>What the settings panel shows under the device list; empty when nothing to say.</summary>
    public static string Status { get; private set; }

    /// <summary>True while the game's sound goes to the device the player picked.</summary>
    public static bool IsRouting
    {
        get
        {
#if AUDIO_ROUTER_WASAPI
            WasapiStream current = stream;
            return current != null && current.Running;
#else
            return false;
#endif
        }
    }

    /// <summary>False where picking a device is not possible (not Windows).</summary>
    public static bool CanChooseDevice
    {
        get
        {
#if AUDIO_ROUTER_WASAPI
            return true;
#else
            return false;
#endif
        }
    }

    private AudioListenerTap tap;
    private float nextListenerSearch;
    private string failedDeviceId;
    private bool configurationChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
        {
            return;
        }

        GameObject host = new GameObject("AudioOutputRouter");
        instance = host.AddComponent<AudioOutputRouter>();
        DontDestroyOnLoad(host);
    }

    /// <summary>Tries the chosen device again after it failed (picked again in the list).</summary>
    public static void Retry()
    {
        if (instance == null)
        {
            return;
        }

        instance.failedDeviceId = null;
        instance.StopStream();
        instance.Apply();
    }

    /// <summary>
    /// Called by the listener filter on the audio thread with the final mix.
    /// </summary>
    internal static void Process(float[] data, int channels)
    {
        float gain = masterGain;
#if AUDIO_ROUTER_WASAPI
        WasapiStream current = stream;
        if (current != null && current.Running)
        {
            current.Write(data, channels, gain);
            Array.Clear(data, 0, data.Length);
            return;
        }
#endif
        if (gain < 0.9999f)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] *= gain;
            }
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        Status = string.Empty;
        AudioPrefs.Changed += Apply;
        AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;
    }

    private void Start()
    {
        Apply();
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        AudioPrefs.Changed -= Apply;
        AudioSettings.OnAudioConfigurationChanged -= HandleAudioConfigurationChanged;
        StopStream();
        instance = null;
    }

    private void OnApplicationQuit()
    {
        StopStream();
    }

    private void HandleAudioConfigurationChanged(bool deviceWasChanged)
    {
        // The output rate may have changed: reopen the stream next frame.
        configurationChanged = true;
    }

    private void Update()
    {
        // Every room scene brings its own camera and AudioListener.
        if ((tap == null || !tap.isActiveAndEnabled) && Time.unscaledTime >= nextListenerSearch)
        {
            nextListenerSearch = Time.unscaledTime + 0.5f;
            AudioListener listener = FindObjectOfType<AudioListener>();
            if (listener != null)
            {
                tap = listener.GetComponent<AudioListenerTap>();
                if (tap == null)
                {
                    tap = listener.gameObject.AddComponent<AudioListenerTap>();
                }
            }
        }

        if (configurationChanged)
        {
            configurationChanged = false;
            StopStream();
            Apply();
        }

#if AUDIO_ROUTER_WASAPI
        WasapiStream current = stream;
        if (current != null && current.ErrorCode != 0)
        {
            failedDeviceId = current.DeviceId;
            Status = Explain(current.ErrorCode) + " — ตอนนี้เสียงออกทางอุปกรณ์ค่าเริ่มต้นของ Windows";
            StopStream();
        }
        else if (current != null && current.Running)
        {
            Status = string.Empty;
        }
#endif
    }

    private void Apply()
    {
        masterGain = AudioPrefs.Master;
        string wanted = AudioPrefs.OutputDevice;
#if AUDIO_ROUTER_WASAPI
        WasapiStream current = stream;
        if (current != null && current.DeviceId == wanted)
        {
            return;
        }

        StopStream();
        if (string.IsNullOrEmpty(wanted))
        {
            failedDeviceId = null;
            Status = string.Empty;
            return;
        }

        if (wanted == failedDeviceId)
        {
            return; // keep the failure message until the player picks it again
        }

        Status = "กำลังเปิดอุปกรณ์...";
        WasapiStream next = new WasapiStream(wanted, AudioSettings.outputSampleRate);
        stream = next;
        next.Start();
#else
        Status = string.IsNullOrEmpty(wanted) ? string.Empty : "เลือกอุปกรณ์เสียงออกได้เฉพาะบน Windows";
#endif
    }

    private void StopStream()
    {
#if AUDIO_ROUTER_WASAPI
        WasapiStream current = stream;
        stream = null;
        if (current != null)
        {
            current.Stop();
        }
#endif
    }

    /// <summary>
    /// Speakers and headphones Windows has active, plus which one is the
    /// default. Empty where devices cannot be listed.
    /// </summary>
    public static List<Device> ListOutputDevices(out string defaultId)
    {
        defaultId = string.Empty;
#if AUDIO_ROUTER_WASAPI
        List<Device> devices = new List<Device>();
        string foundDefault = string.Empty;
        // COM objects get their own short-lived MTA thread, whatever
        // apartment Unity's main thread is in.
        Thread worker = new Thread(() =>
        {
            try
            {
                foundDefault = WasapiStream.Enumerate(devices);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not list audio outputs: " + e.Message);
            }
        });
        worker.IsBackground = true;
        worker.Start();
        if (!worker.Join(3000))
        {
            return new List<Device>();
        }

        defaultId = foundDefault;
        return devices;
#else
        return new List<Device>();
#endif
    }

    private static string Explain(int hr)
    {
        switch ((uint)hr)
        {
            case 0x88890004: return "อุปกรณ์เสียงที่เลือกถูกถอดหรือปิดไป";
            case 0x80070490: return "ไม่พบอุปกรณ์เสียงที่เลือก";
            case 0x8889000A: return "อุปกรณ์เสียงที่เลือกถูกโปรแกรมอื่นใช้แบบผูกขาดอยู่";
            default: return "เปิดอุปกรณ์เสียงที่เลือกไม่ได้ (0x" + hr.ToString("X8") + ")";
        }
    }

#if AUDIO_ROUTER_WASAPI
    /// <summary>
    /// A shared-mode WASAPI stream on one device, fed from a small ring
    /// buffer the audio thread fills. Everything COM lives on its own thread.
    /// </summary>
    private sealed class WasapiStream
    {
        private const int Channels = 2;
        private const int TargetMilliseconds = 50;  // kept queued on the device
        private const int MaxBufferedMilliseconds = 140;

        public readonly string DeviceId;
        private readonly int sampleRate;
        private readonly float[] ring;
        private readonly int maxBuffered;
        private readonly int resumeBuffered;
        private readonly object gate = new object();
        private int readPosition;
        private int count;
        private Thread thread;
        private volatile bool stopping;
        private volatile bool running;
        private volatile int errorCode;

        public bool Running { get { return running; } }
        public int ErrorCode { get { return errorCode; } }

        public WasapiStream(string deviceId, int rate)
        {
            DeviceId = deviceId;
            sampleRate = rate > 0 ? rate : 48000;
            ring = new float[sampleRate * Channels]; // one second
            maxBuffered = sampleRate * Channels * MaxBufferedMilliseconds / 1000;
            resumeBuffered = sampleRate * Channels * TargetMilliseconds / 1000;
        }

        public void Start()
        {
            thread = new Thread(Run) { IsBackground = true, Name = "AudioOutputRouter" };
            thread.Start();
        }

        public void Stop()
        {
            stopping = true;
            running = false;
            if (thread != null && thread.IsAlive)
            {
                thread.Join(500);
            }
        }

        /// <summary>Audio thread: queue the mix (any channel count) as stereo.</summary>
        public void Write(float[] data, int channels, float gain)
        {
            if (channels <= 0)
            {
                return;
            }

            int frames = data.Length / channels;
            lock (gate)
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    int index = frame * channels;
                    float left = data[index] * gain;
                    float right = channels > 1 ? data[index + 1] * gain : left;
                    Push(left);
                    Push(right);
                }

                // The two devices' clocks drift apart a little. When the game
                // runs ahead, skip the oldest audio instead of letting the
                // delay grow.
                if (count > maxBuffered)
                {
                    int drop = count - resumeBuffered;
                    drop -= drop % Channels;
                    readPosition = (readPosition + drop) % ring.Length;
                    count -= drop;
                }
            }
        }

        private void Push(float value)
        {
            int write = (readPosition + count) % ring.Length;
            ring[write] = value;
            if (count < ring.Length)
            {
                count++;
            }
            else
            {
                readPosition = (readPosition + 1) % ring.Length;
            }
        }

        private int Pull(float[] target, int samples)
        {
            lock (gate)
            {
                int taken = Math.Min(samples, count);
                taken -= taken % Channels;
                for (int i = 0; i < taken; i++)
                {
                    target[i] = ring[readPosition];
                    readPosition = (readPosition + 1) % ring.Length;
                }
                count -= taken;
                return taken;
            }
        }

        private void Run()
        {
            CoInitializeEx(IntPtr.Zero, CoinitMultithreaded);
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioClient client = null;
            IAudioRenderClient render = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                Check(enumerator.GetDevice(DeviceId, out device));

                Guid clientId = typeof(IAudioClient).GUID;
                object activated;
                Check(device.Activate(ref clientId, ClsctxAll, IntPtr.Zero, out activated));
                client = (IAudioClient)activated;

                WaveFormatEx format = new WaveFormatEx
                {
                    wFormatTag = WaveFormatIeeeFloat,
                    nChannels = Channels,
                    nSamplesPerSec = (uint)sampleRate,
                    wBitsPerSample = 32,
                    nBlockAlign = Channels * 4,
                    nAvgBytesPerSec = (uint)(sampleRate * Channels * 4),
                    cbSize = 0,
                };
                // Windows converts our rate/format to the device's own mix format.
                Check(client.Initialize(0, StreamFlagsAutoConvertPcm | StreamFlagsSrcDefaultQuality,
                                        1000000 /* 100 ms in 100-ns units */, 0, ref format, IntPtr.Zero));
                uint bufferFrames;
                Check(client.GetBufferSize(out bufferFrames));
                Guid renderId = typeof(IAudioRenderClient).GUID;
                object service;
                Check(client.GetService(ref renderId, out service));
                render = (IAudioRenderClient)service;

                int targetFrames = Math.Min((int)bufferFrames, sampleRate * TargetMilliseconds / 1000);
                float[] scratch = new float[bufferFrames * Channels];
                Check(client.Start());
                running = true;

                while (!stopping)
                {
                    uint padding;
                    Check(client.GetCurrentPadding(out padding));
                    int wanted = targetFrames - (int)padding;
                    if (wanted > 0)
                    {
                        int frames = Pull(scratch, wanted * Channels) / Channels;
                        // Ran dry (the game paused its audio, a hitch): keep
                        // the device fed with silence rather than stutter.
                        if (frames < wanted && padding < targetFrames / 2)
                        {
                            Array.Clear(scratch, frames * Channels, (wanted - frames) * Channels);
                            frames = wanted;
                        }

                        if (frames > 0)
                        {
                            IntPtr buffer;
                            Check(render.GetBuffer((uint)frames, out buffer));
                            Marshal.Copy(scratch, 0, buffer, frames * Channels);
                            Check(render.ReleaseBuffer((uint)frames, 0));
                        }
                    }

                    Thread.Sleep(4);
                }

                client.Stop();
            }
            catch (COMException e)
            {
                errorCode = e.ErrorCode != 0 ? e.ErrorCode : unchecked((int)0x80004005);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Audio output stream failed: " + e.Message);
                errorCode = unchecked((int)0x80004005);
            }
            finally
            {
                running = false;
                Release(render);
                Release(client);
                Release(device);
                Release(enumerator);
                CoUninitialize();
            }
        }

        /// <summary>Fills <paramref name="devices"/>; returns the default device's id.</summary>
        public static string Enumerate(List<Device> devices)
        {
            CoInitializeEx(IntPtr.Zero, CoinitMultithreaded);
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;
            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                Check(enumerator.EnumAudioEndpoints(DataFlowRender, DeviceStateActive, out collection));
                uint total;
                Check(collection.GetCount(out total));
                for (uint i = 0; i < total; i++)
                {
                    IMMDevice device;
                    if (collection.Item(i, out device) != 0)
                    {
                        continue;
                    }

                    string id;
                    if (device.GetId(out id) == 0)
                    {
                        devices.Add(new Device { id = id, name = FriendlyName(device) ?? id });
                    }
                    Release(device);
                }

                IMMDevice fallback;
                string defaultId = string.Empty;
                if (enumerator.GetDefaultAudioEndpoint(DataFlowRender, RoleMultimedia, out fallback) == 0)
                {
                    fallback.GetId(out defaultId);
                    Release(fallback);
                }

                return defaultId ?? string.Empty;
            }
            finally
            {
                Release(collection);
                Release(enumerator);
                CoUninitialize();
            }
        }

        private static string FriendlyName(IMMDevice device)
        {
            IPropertyStore store;
            if (device.OpenPropertyStore(0 /* STGM_READ */, out store) != 0)
            {
                return null;
            }

            try
            {
                PropertyKey key = new PropertyKey
                {
                    fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                    pid = 14, // PKEY_Device_FriendlyName
                };
                PropVariant value;
                if (store.GetValue(ref key, out value) != 0)
                {
                    return null;
                }

                string name = value.vt == VtLpwstr ? Marshal.PtrToStringUni(value.pointer) : null;
                PropVariantClear(ref value);
                return name;
            }
            finally
            {
                Release(store);
            }
        }

        private static void Check(int hr)
        {
            if (hr != 0)
            {
                throw new COMException("WASAPI call failed", hr);
            }
        }

        private static void Release(object comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.ReleaseComObject(comObject);
            }
        }

        private const int CoinitMultithreaded = 0;
        private const int ClsctxAll = 23;
        private const int DataFlowRender = 0;
        private const int DeviceStateActive = 1;
        private const int RoleMultimedia = 1;
        private const ushort WaveFormatIeeeFloat = 3;
        private const short VtLpwstr = 31;
        private const uint StreamFlagsAutoConvertPcm = 0x80000000;
        private const uint StreamFlagsSrcDefaultQuality = 0x08000000;

        [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, int coInit);
        [DllImport("ole32.dll")] private static extern void CoUninitialize();
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public short vt;
        [FieldOffset(8)] public IntPtr pointer;
        [FieldOffset(16)] public IntPtr padding;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormatEx
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
                                   [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetAt(int index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity,
                                     ref WaveFormatEx format, IntPtr sessionGuid);
        [PreserveSig] int GetBufferSize(out uint frames);
        [PreserveSig] int GetStreamLatency(out long latency);
        [PreserveSig] int GetCurrentPadding(out uint padding);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr handle);
        [PreserveSig] int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(uint frames, out IntPtr data);
        [PreserveSig] int ReleaseBuffer(uint frames, uint flags);
    }
#endif
}
