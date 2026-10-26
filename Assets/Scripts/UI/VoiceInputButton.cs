using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The microphone button in the chat composer: press to start talking,
/// press again to stop. The speech is turned into text (<see cref="SpeechToText"/>)
/// and put into the message box for the player to fix up and send; nothing
/// is sent by itself.
/// </summary>
public class VoiceInputButton : MonoBehaviour
{
    private enum State
    {
        Idle,
        Recording,
        Transcribing,
    }

    private static readonly Color IdleColor = new Color(0.13f, 0.31f, 0.39f);
    private static readonly Color RecordingColor = new Color(0.72f, 0.16f, 0.18f);
    private static readonly Color BusyColor = new Color(0.25f, 0.29f, 0.36f);

    private DialogueManager owner;
    private TMP_InputField input;
    private TMP_FontAsset font;
    private Button button;
    private Image buttonImage;
    private Image icon;
    private static Sprite micSprite;
    private RectTransform levelBar;
    private TMP_Text status;
    private State state;
    private int requestSerial;
    private AiRequestOperation operation;
    private int conversation;
    private string npcId;
    private float statusUntil;

    public void Configure(DialogueManager manager, Transform composer, TMP_InputField chatInput, TMP_FontAsset textFont)
    {
        owner = manager;
        input = chatInput;
        font = textFont;

        GameObject buttonObject = Box(composer, "VoiceButton", IdleColor);
        buttonImage = buttonObject.GetComponent<Image>();
        button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.78f, 0.95f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f);
        button.colors = colors;
        button.onClick.AddListener(Toggle);
        RectTransform rect = (RectTransform)buttonObject.transform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-276f, 0f);
        rect.sizeDelta = new Vector2(60f, -12f);

        GameObject iconObject = new GameObject("MicIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = buttonObject.layer;
        iconObject.transform.SetParent(buttonObject.transform, false);
        icon = iconObject.GetComponent<Image>();
        icon.sprite = MicSprite();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(34f, 34f);

        // A thin bar along the bottom of the button that follows the voice.
        GameObject bar = Box(buttonObject.transform, "Level", new Color(1f, 0.86f, 0.5f, 0.95f));
        bar.GetComponent<Image>().raycastTarget = false;
        levelBar = (RectTransform)bar.transform;
        levelBar.anchorMin = Vector2.zero;
        levelBar.anchorMax = new Vector2(0f, 0f);
        levelBar.pivot = Vector2.zero;
        levelBar.anchoredPosition = new Vector2(0f, 2f);
        levelBar.sizeDelta = new Vector2(0f, 4f);
        bar.SetActive(false);

        // What is going on, just above the composer.
        status = Text(composer, "VoiceStatus", string.Empty, 19f);
        status.color = new Color(0.86f, 0.9f, 0.96f);
        RectTransform statusRect = (RectTransform)status.transform;
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = Vector2.one;
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 4f);
        statusRect.sizeDelta = new Vector2(-24f, 30f);
        status.alignment = TextAlignmentOptions.BottomRight;

        VoiceInput.LimitReached += HandleLimitReached;
    }

    private void OnDestroy()
    {
        VoiceInput.LimitReached -= HandleLimitReached;
    }

    private void OnDisable()
    {
        // The conversation closed: drop what was being said or transcribed.
        if (state == State.Recording)
        {
            VoiceInput.CancelRecording();
        }

        requestSerial++;
        if (operation != null)
        {
            operation.Cancel();
            operation = null;
        }
        SetState(State.Idle);
        ShowStatus(string.Empty, 0f);
    }

    private void Toggle()
    {
        if (state == State.Recording)
        {
            Finish();
            return;
        }

        if (state != State.Idle || input == null || !input.interactable)
        {
            return;
        }

        string reason;
        if (!SpeechToText.IsAvailable(out reason) || !VoiceInput.TryStartRecording(out reason))
        {
            ShowStatus(reason, 6f);
            return;
        }

        conversation = owner.ConversationVersion;
        npcId = owner.ActiveNpcId;
        SetState(State.Recording);
    }

    private void HandleLimitReached()
    {
        if (state == State.Recording)
        {
            Finish();
        }
    }

    private void Finish()
    {
        int rate;
        float[] samples = VoiceInput.StopRecording(out rate);
        if (samples == null || samples.Length < rate * 0.3f)
        {
            SetState(State.Idle);
            ShowStatus("สั้นเกินไป — กดไมค์ พูด แล้วกดไมค์อีกครั้งเมื่อพูดจบ", 5f);
            return;
        }

        if (SpeechToText.IsSilent(samples))
        {
            SetState(State.Idle);
            ShowStatus("ไม่ได้ยินเสียง — ลองพูดใกล้ไมค์ขึ้น หรือเพิ่มระดับเสียงไมค์ในตั้งค่า > เสียง", 6f);
            return;
        }

        SetState(State.Transcribing);
        int serial = ++requestSerial;
        // Cancelled (and the web request disposed) if the conversation closes.
        operation = new AiRequestOperation(this);
        operation.Start(
            () => SpeechToText.Transcribe(
                samples, rate, SpeechToText.NameHints(),
                text => Deliver(serial, text),
                error => Fail(serial, error)),
            exception => Fail(serial, "ถอดเสียงไม่สำเร็จ (" + exception.GetType().Name + ")"),
            () => Fail(serial, "ถอดเสียงไม่สำเร็จ"));
    }

    private void Fail(int serial, string message)
    {
        // Also called when the request ends after it already delivered.
        if (serial != requestSerial || state != State.Transcribing)
        {
            return;
        }

        SetState(State.Idle);
        ShowStatus(message, 7f);
    }

    private void Deliver(int serial, string text)
    {
        if (serial != requestSerial)
        {
            return;
        }

        SetState(State.Idle);
        if (!owner.IsCurrentConversation(conversation, npcId))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            ShowStatus("ไม่ได้ยินคำพูด ลองอีกครั้ง", 5f);
            return;
        }

        // Added after what is already typed, so talking can continue a message.
        string current = input.text.TrimEnd();
        string combined = current.Length > 0 ? current + " " + text : text;
        if (input.characterLimit > 0 && combined.Length > input.characterLimit)
        {
            combined = combined.Substring(0, input.characterLimit);
        }

        input.text = combined;
        ShowStatus("แก้ข้อความได้ แล้วกดส่งหรือ Enter", 4f);
        StartCoroutine(FocusAtEnd());
    }

    private IEnumerator FocusAtEnd()
    {
        input.ActivateInputField();
        yield return null; // activation selects everything on the next frame
        input.MoveTextEnd(false);
    }

    private void Update()
    {
        bool usable = input != null && input.interactable;
        button.interactable = state == State.Recording || (state == State.Idle && usable);

        if (state == State.Recording)
        {
            float seconds = VoiceInput.Elapsed;
            ShowStatus("กำลังฟัง... " + Mathf.FloorToInt(seconds) + " / " + VoiceInput.MaxSeconds +
                       " วิ  (กดไมค์อีกครั้งเมื่อพูดจบ)", 1f);
            // The icon breathes with the voice so it is clear the mic is live.
            float pulse = 1f + 0.18f * Mathf.Clamp01(VoiceInput.Level);
            icon.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            float width = ((RectTransform)button.transform).rect.width;
            levelBar.sizeDelta = new Vector2(Mathf.Clamp01(VoiceInput.Level) * width, 4f);
        }

        if (status != null && status.text.Length > 0 && Time.unscaledTime > statusUntil)
        {
            status.text = string.Empty;
        }
    }

    private void SetState(State next)
    {
        state = next;
        if (buttonImage == null)
        {
            return;
        }

        buttonImage.color = next == State.Recording ? RecordingColor : next == State.Transcribing ? BusyColor : IdleColor;
        icon.color = next == State.Transcribing ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        icon.rectTransform.localScale = Vector3.one;
        levelBar.gameObject.SetActive(next == State.Recording);
        if (next == State.Transcribing)
        {
            ShowStatus("กำลังแปลงเสียงเป็นข้อความ...", 60f);
        }
    }

    private void ShowStatus(string message, float seconds)
    {
        if (status == null)
        {
            return;
        }

        status.text = message ?? string.Empty;
        statusUntil = Time.unscaledTime + seconds;
    }

    /// <summary>A white microphone drawn once at start-up, so no image asset is needed.</summary>
    private static Sprite MicSprite()
    {
        if (micSprite != null)
        {
            return micSprite;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                // Signed distances (negative inside) of the four parts.
                float capsule = Capsule(px, py, 32f, 31f, 50f, 9f);
                float ring = Mathf.Abs(Vector2.Distance(new Vector2(px, py), new Vector2(32f, 35f)) - 15f) - 2.2f;
                if (py > 37f) ring = Mathf.Max(ring, py - 37f); // only the cradle under the head
                float stem = Box(px, py, 32f, 16f, 2.2f, 4.5f);
                float foot = Box(px, py, 32f, 10.5f, 10f, 2.2f);
                float distance = Mathf.Min(Mathf.Min(capsule, ring), Mathf.Min(stem, foot));
                float alpha = Mathf.Clamp01(0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        micSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        micSprite.hideFlags = HideFlags.HideAndDontSave;
        return micSprite;
    }

    private static float Capsule(float x, float y, float cx, float bottom, float top, float radius)
    {
        float clamped = Mathf.Clamp(y, bottom, top);
        return Vector2.Distance(new Vector2(x, y), new Vector2(cx, clamped)) - radius;
    }

    private static float Box(float x, float y, float cx, float cy, float halfWidth, float halfHeight)
    {
        float dx = Mathf.Abs(x - cx) - halfWidth;
        float dy = Mathf.Abs(y - cy) - halfHeight;
        float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(dx, dy), 0f);
    }

    private GameObject Box(Transform parent, string objectName, Color color)
    {
        GameObject box = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.layer = parent.gameObject.layer;
        box.transform.SetParent(parent, false);
        box.GetComponent<Image>().color = color;
        return box;
    }

    private TMP_Text Text(Transform parent, string objectName, string value, float size)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = size;
        text.text = value;
        text.color = Color.white;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }
}
