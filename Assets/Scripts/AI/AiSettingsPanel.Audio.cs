using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "เสียง" tab of the settings panel: volumes, the output device
/// (speakers/headphones) and the microphone used for voice typing, with a
/// level meter to set the microphone volume by.
/// </summary>
public partial class AiSettingsPanel
{
    private const int AiTab = 0;
    private const int AudioTab = 1;
    private static readonly string[] TabLabels = { "AI", "เสียง" };

    private int tab;
    private Vector2 audioScroll;
    private List<AudioOutputRouter.Device> outputDevices;
    private string defaultOutputId = string.Empty;
    private string[] inputDevices;

    private GUIStyle sliderLabelStyle;
    private GUIStyle sliderValueStyle;
    private Texture2D meterTrack;
    private Texture2D meterFill;

    private void DrawTabs(Rect area)
    {
        const float width = 140f;
        for (int index = 0; index < TabLabels.Length; index++)
        {
            Rect rect = new Rect(area.x + index * (width + 8f), area.y, width, area.height);
            if (ModalGui.Button(rect, TabLabels[index], segmentStyle, index == tab) && index != tab)
            {
                SwitchTab(index);
            }
        }
    }

    private void SwitchTab(int next)
    {
        tab = next;
        pickerOpen = false;
        if (next == AudioTab)
        {
            RefreshDevices();
        }
        else
        {
            CloseAudioTab();
        }
    }

    /// <summary>The panel closed or left the tab: stop the mic test, write the volumes to disk.</summary>
    private void CloseAudioTab()
    {
        VoiceInput.StopMonitor();
        AudioPrefs.Save();
    }

    private void RefreshDevices()
    {
        outputDevices = AudioOutputRouter.ListOutputDevices(out defaultOutputId);
        inputDevices = VoiceInput.Devices;
    }

    private void DrawAudioForm()
    {
        if (outputDevices == null || inputDevices == null)
        {
            RefreshDevices();
        }

        audioScroll = GUILayout.BeginScrollView(audioScroll, false, false, GUIStyle.none, skin.verticalScrollbar, GUIStyle.none);

        Section("ระดับเสียง");
        AudioPrefs.Master = VolumeSlider("เสียงทั้งหมด", AudioPrefs.Master, 1f);
        AudioPrefs.Music = VolumeSlider("เพลงและเสียงบรรยากาศห้อง", AudioPrefs.Music, 1f);
        AudioPrefs.Effects = VolumeSlider("เอฟเฟกต์ (ประตู ไอเท็ม เสียงในห้อง)", AudioPrefs.Effects, 1f);

        Section("อุปกรณ์เสียงออก (ลำโพง / หูฟัง)");
        DrawOutputDevices();

        Section("อุปกรณ์รับเสียง (ไมโครโฟน)");
        DrawInputDevices();

        Section("พูดแทนพิมพ์");
        Note("ตอนคุยกับ NPC กดปุ่มรูปไมโครโฟนข้างช่องพิมพ์แล้วพูด กดอีกครั้งเมื่อพูดจบ ข้อความจะขึ้นในช่องพิมพ์ให้แก้ก่อนกดส่ง พูดได้ครั้งละไม่เกิน " +
             VoiceInput.MaxSeconds + " วินาที");
        Note("เสียงพูดถูกส่งไปแปลงเป็นข้อความที่ผู้ให้บริการ AI ที่ตั้งไว้ในแท็บ AI (KKU ใช้โมเดล " + SpeechToText.KkuModel +
             ") เกมไม่ได้เก็บไฟล์เสียงไว้");

        GUILayout.Space(10f);
        Note("F10 หรือ Esc เพื่อปิด");
        GUILayout.EndScrollView();
    }

    private void DrawOutputDevices()
    {
        if (!AudioOutputRouter.CanChooseDevice)
        {
            Note("เลือกอุปกรณ์ได้เฉพาะบน Windows — ตอนนี้ใช้อุปกรณ์ค่าเริ่มต้นของระบบ");
            return;
        }

        string chosen = AudioPrefs.OutputDevice;
        string defaultName = null;
        bool chosenPresent = false;
        foreach (AudioOutputRouter.Device device in outputDevices)
        {
            if (device.id == defaultOutputId) defaultName = device.name;
            if (device.id == chosen) chosenPresent = true;
        }

        string defaultLabel = "ค่าเริ่มต้นของ Windows" + (defaultName != null ? "  ·  " + defaultName : string.Empty);
        if (DeviceRow(defaultLabel, string.IsNullOrEmpty(chosen)))
        {
            AudioPrefs.OutputDevice = string.Empty;
        }

        foreach (AudioOutputRouter.Device device in outputDevices)
        {
            if (DeviceRow(device.name, device.id == chosen))
            {
                if (device.id == chosen) AudioOutputRouter.Retry(); // picked again after a failure
                else AudioPrefs.OutputDevice = device.id;
            }
        }

        if (!string.IsNullOrEmpty(chosen) && !chosenPresent)
        {
            statusStyle.normal.textColor = WarnColor;
            GUILayout.Label("อุปกรณ์ที่เลือกไว้ไม่ได้เชื่อมต่ออยู่ — เสียงออกทางค่าเริ่มต้นของ Windows", statusStyle);
        }
        else if (!string.IsNullOrEmpty(AudioOutputRouter.Status))
        {
            statusStyle.normal.textColor = AudioOutputRouter.IsRouting ? GoodColor : WarnColor;
            GUILayout.Label(AudioOutputRouter.Status, statusStyle);
        }

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        if (ModalGui.LayoutButton("รีเฟรชรายการ", secondaryStyle, false, GUILayout.Height(40f), GUILayout.Width(170f)))
        {
            RefreshDevices();
        }

        if (ModalGui.LayoutButton("ลองฟังเสียง", secondaryStyle, false, GUILayout.Height(40f), GUILayout.Width(150f)))
        {
            SfxPlayer.Play(SfxPlayer.Cue.Success);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawInputDevices()
    {
        if (inputDevices.Length == 0)
        {
            statusStyle.normal.textColor = WarnColor;
            GUILayout.Label("ไม่พบไมโครโฟน", statusStyle);
            Note("เสียบไมค์แล้วกด \"รีเฟรชรายการ\" และตรวจว่า Windows อนุญาตให้แอปใช้ไมโครโฟน (Settings > Privacy > Microphone)");
        }
        else
        {
            string chosen = AudioPrefs.InputDevice;
            bool chosenPresent = System.Array.IndexOf(inputDevices, chosen) >= 0;
            if (DeviceRow("ค่าเริ่มต้นของ Windows", string.IsNullOrEmpty(chosen) || !chosenPresent))
            {
                AudioPrefs.InputDevice = string.Empty;
            }

            foreach (string device in inputDevices)
            {
                if (DeviceRow(device, device == chosen))
                {
                    AudioPrefs.InputDevice = device;
                }
            }

            if (!string.IsNullOrEmpty(chosen) && !chosenPresent)
            {
                Note("ไมค์ที่เลือกไว้ (" + chosen + ") ไม่ได้เชื่อมต่ออยู่ — ใช้ค่าเริ่มต้นของ Windows แทน");
            }
        }

        GUILayout.Space(6f);
        AudioPrefs.MicGain = VolumeSlider("ระดับเสียงไมค์", AudioPrefs.MicGain, AudioPrefs.MaxMicGain);

        GUILayout.BeginHorizontal();
        bool testing = VoiceInput.IsMonitoring;
        GUI.enabled = inputDevices.Length > 0 && !VoiceInput.IsRecording;
        if (ModalGui.LayoutButton(testing ? "หยุดทดสอบ" : "ทดสอบไมค์", secondaryStyle, testing,
                                  GUILayout.Height(40f), GUILayout.Width(150f)))
        {
            if (testing) VoiceInput.StopMonitor();
            else VoiceInput.StartMonitor();
        }
        GUI.enabled = true;

        GUILayout.Space(10f);
        GUILayout.BeginVertical();
        GUILayout.Space(13f);
        Rect meter = GUILayoutUtility.GetRect(1f, 14f, GUILayout.ExpandWidth(true));
        DrawMeter(meter, testing || VoiceInput.IsRecording ? VoiceInput.Level : 0f);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        Note("กดทดสอบแล้วพูดตามปกติ: แถบควรขึ้นถึงช่วงเขียว–เหลือง ถ้าขึ้นแดงบ่อยให้ลดระดับเสียงไมค์ ถ้าแทบไม่ขยับให้เพิ่ม");
    }

    private bool DeviceRow(string label, bool selected)
    {
        return ModalGui.LayoutButton((selected ? "●  " : "○  ") + label, rowStyle, selected, GUILayout.Height(40f));
    }

    private float VolumeSlider(string label, float value, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, sliderLabelStyle, GUILayout.Width(300f));
        float next = GUILayout.HorizontalSlider(value, 0f, max, GUILayout.ExpandWidth(true));
        GUILayout.Label(Mathf.RoundToInt(next * 100f) + "%", sliderValueStyle, GUILayout.Width(70f));
        GUILayout.EndHorizontal();
        return Mathf.Round(next * 100f) / 100f; // whole percents
    }

    private void DrawMeter(Rect rect, float level)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        GUI.DrawTexture(rect, meterTrack);
        float amount = Mathf.Clamp01(level);
        if (amount <= 0f)
        {
            return;
        }

        Color old = GUI.color;
        GUI.color = amount < 0.6f ? GoodColor : amount < 0.9f ? WarnColor : BadColor;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * amount, rect.height), meterFill);
        GUI.color = old;
    }

    /// <summary>Slider and meter looks, on the panel's private skin.</summary>
    private void EnsureAudioStyles(Color line)
    {
        sliderLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 38f,
        };
        sliderLabelStyle.normal.textColor = Text;

        sliderValueStyle = new GUIStyle(sliderLabelStyle) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
        sliderValueStyle.normal.textColor = Accent;

        // A thin rounded track centred in a 20 px row, and a round thumb.
        GUIStyle track = skin.horizontalSlider;
        track.normal.background = Keep(SliderTrack(new Color(1f, 1f, 1f, 0.14f)));
        track.border = new RectOffset(8, 8, 0, 0);
        track.fixedHeight = 20f;
        track.margin = new RectOffset(8, 8, 9, 9);
        track.padding = new RectOffset(0, 0, 0, 0);
        track.overflow = new RectOffset(0, 0, 0, 0);

        GUIStyle thumb = skin.horizontalSliderThumb;
        Texture2D knob = Keep(ModalGui.Rounded(Accent, 9));
        Texture2D knobLit = Keep(ModalGui.Rounded(new Color(1f, 0.82f, 0.42f), 9));
        thumb.normal.background = knob;
        thumb.hover.background = knobLit;
        thumb.active.background = knobLit;
        thumb.focused.background = knob;
        thumb.border = new RectOffset(9, 9, 9, 9);
        thumb.fixedWidth = 20f;
        thumb.fixedHeight = 20f;
        thumb.padding = new RectOffset(0, 0, 0, 0);
        thumb.overflow = new RectOffset(0, 0, 0, 0);

        meterTrack = Keep(ModalGui.Solid(line));
        meterFill = Keep(ModalGui.Solid(Color.white));
    }

    private static Texture2D SliderTrack(Color color)
    {
        const int width = 16;
        const int height = 20;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color clear = new Color(color.r, color.g, color.b, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 6 px tall bar with round ends, transparent above and below.
                float dy = Mathf.Abs(y + 0.5f - height * 0.5f);
                float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - width * 0.5f) - (width * 0.5f - 3f));
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float coverage = Mathf.Clamp01(3f - distance + 0.5f);
                texture.SetPixel(x, y, coverage > 0f ? new Color(color.r, color.g, color.b, color.a * coverage) : clear);
            }
        }

        texture.Apply();
        return texture;
    }
}
