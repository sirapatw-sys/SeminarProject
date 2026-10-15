using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MysteryGame.Core;
using MysteryGame.Knowledge;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Turns the player's recorded voice into text through the AI provider set
/// in the settings, so it needs no extra key or install:
///
/// * KKU IntelSphere, Google Gemini and OpenAI-compatible services get the
///   clip as an <c>input_audio</c> part of a chat request. KKU uses
///   <see cref="KkuModel"/> for this whatever chat model is picked: it is the
///   fastest one that hears Thai well (tested 2026-10-09: a 3 s Thai line
///   came back word for word in ~2 s, ~160 tokens).
/// * OpenAI uses its transcription endpoint (<see cref="OpenAiModel"/>).
///
/// The names of the characters go along as spelling hints; without them
/// "อลิซ" came back as "อลิสเตอร์".
/// </summary>
public static class SpeechToText
{
    public const string KkuModel = "gemini-3.5-flash-lite";
    public const string OpenAiModel = "gpt-4o-mini-transcribe";
    public const string OpenAiTranscriptionsUrl = "https://api.openai.com/v1/audio/transcriptions";

    private const int TimeoutSeconds = 30;
    private const int UploadRate = 16000;

    private const string Instruction =
        "ถอดเสียงพูดในไฟล์เสียงนี้เป็นข้อความตามที่ได้ยินจริง ใช้ภาษาที่ผู้พูดพูด " +
        "ตอบเฉพาะข้อความที่ถอดได้ ไม่ต้องอธิบาย ถ้าไม่ได้ยินคำพูดให้ตอบว่าง";

    /// <summary>False with a reason when voice typing cannot work right now.</summary>
    public static bool IsAvailable(out string reason)
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        if (generator == null || !generator.CanGenerate)
        {
            reason = "พูดแทนพิมพ์ต้องใช้ AI ถอดเสียง — ตั้งค่า AI ก่อน (F10)";
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Too quiet to hold speech: nothing is sent (saves a request).
    /// </summary>
    public static bool IsSilent(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return true;
        }

        double sum = 0;
        float peak = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float value = samples[i];
            sum += value * value;
            peak = Mathf.Max(peak, Mathf.Abs(value));
        }

        float rms = (float)Math.Sqrt(sum / samples.Length);
        return peak < 0.03f || rms < 0.004f;
    }

    /// <summary>Character names to spell right, from the game's NPC profiles.</summary>
    public static List<string> NameHints()
    {
        List<string> names = new List<string>();
        GameDefinition game = GameDefinition.Current;
        IEnumerable<NpcProfileData> npcs = game != null
            ? (IEnumerable<NpcProfileData>)game.npcs
            : Resources.LoadAll<NpcProfileData>(KnowledgeLibrary.NpcPath);
        foreach (NpcProfileData npc in npcs)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.displayName))
            {
                continue;
            }

            // "รินะ (Rina)" gives both spellings.
            foreach (string part in npc.displayName.Split('(', ')', ',', '/'))
            {
                string name = part.Trim();
                if (name.Length > 0 && !names.Contains(name))
                {
                    names.Add(name);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// Sends the clip and calls back with the text (possibly empty: nothing
    /// was said) or with a message for the player.
    /// </summary>
    public static IEnumerator Transcribe(float[] samples, int sampleRate, IList<string> names,
                                         Action<string> onText, Action<string> onError)
    {
        AiDialogueGenerator generator = AiDialogueGenerator.Instance;
        string reason;
        if (!IsAvailable(out reason))
        {
            onError(reason);
            yield break;
        }

        byte[] wav = EncodeWav(Resample(samples, sampleRate, UploadRate), UploadRate);
        string hint = names != null && names.Count > 0
            ? " ชื่อเฉพาะที่อาจได้ยิน: " + string.Join(", ", names)
            : string.Empty;

        UnityWebRequest request;
        bool openAi = generator.Provider == AiProviderType.OpenAiResponses;
        if (openAi)
        {
            List<IMultipartFormSection> form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", wav, "speech.wav", "audio/wav"),
                new MultipartFormDataSection("model", OpenAiModel),
                new MultipartFormDataSection("response_format", "json"),
            };
            if (hint.Length > 0)
            {
                form.Add(new MultipartFormDataSection("prompt", hint.Trim()));
            }
            request = UnityWebRequest.Post(OpenAiTranscriptionsUrl, form);
        }
        else
        {
            string model = generator.Provider == AiProviderType.KkuIntelsphere ? KkuModel : generator.Model;
            string url = generator.Provider == AiProviderType.KkuIntelsphere
                ? AiDialogueGenerator.KkuChatCompletionsUrl
                : generator.ApiUrl;
            string body = BuildChatBody(model, Instruction + hint, Convert.ToBase64String(wav));
            request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
        }

        using (request)
        {
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Authorization", "Bearer " + generator.GetApiKey());
            yield return request.SendWebRequest();

            string response = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (!string.IsNullOrEmpty(request.error))
            {
                AiFailureKind kind = AiProviderDiagnostics.Classify(request.responseCode, request.error + " " + response);
                string advice = AiProviderDiagnostics.Explain(kind);
                if (kind == AiFailureKind.DailyLimitReached)
                {
                    advice = "ใช้โควตาของวันนี้หมดแล้ว — พิมพ์แทนไปก่อน";
                }
                onError("ถอดเสียงไม่สำเร็จ" + (string.IsNullOrEmpty(advice) ? " (" + request.error + ")" : " — " + advice));
                yield break;
            }

            string text = openAi ? ReadTranscription(response) : ReadChatContent(response);
            if (text == null)
            {
                onError("ถอดเสียงไม่สำเร็จ — ผู้ให้บริการตอบกลับในรูปแบบที่อ่านไม่ได้");
                yield break;
            }

            onText(Clean(text));
        }
    }

    /// <summary>Strips quotes and "empty" answers the model sometimes gives instead of nothing.</summary>
    public static string Clean(string text)
    {
        string value = (text ?? string.Empty).Trim().Trim('"', '\'', '“', '”', '「', '」').Trim();
        if (value == "ว่าง" || value == "(ว่าง)" || value == "-" || value == "...")
        {
            return string.Empty;
        }

        return value;
    }

    public static string BuildChatBody(string model, string instruction, string base64Wav)
    {
        StringBuilder json = new StringBuilder(base64Wav.Length + 512);
        json.Append("{\"model\":\"").Append(Escape(model)).Append("\",");
        json.Append("\"temperature\":0,\"max_tokens\":400,");
        json.Append("\"messages\":[{\"role\":\"user\",\"content\":[");
        json.Append("{\"type\":\"text\",\"text\":\"").Append(Escape(instruction)).Append("\"},");
        json.Append("{\"type\":\"input_audio\",\"input_audio\":{\"data\":\"").Append(base64Wav);
        json.Append("\",\"format\":\"wav\"}}]}]}");
        return json.ToString();
    }

    public static string ReadChatContent(string body)
    {
        try
        {
            ChatBody parsed = JsonUtility.FromJson<ChatBody>(body);
            if (parsed != null && parsed.choices != null && parsed.choices.Length > 0 &&
                parsed.choices[0].message != null)
            {
                return parsed.choices[0].message.content;
            }
        }
        catch (Exception)
        {
            // Not JSON: handled as unreadable below.
        }

        return null;
    }

    public static string ReadTranscription(string body)
    {
        try
        {
            TranscriptionBody parsed = JsonUtility.FromJson<TranscriptionBody>(body);
            return parsed != null ? parsed.text : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Linear resampling; speech needs no better for transcription.</summary>
    public static float[] Resample(float[] samples, int fromRate, int toRate)
    {
        if (samples == null || fromRate <= 0 || fromRate == toRate)
        {
            return samples;
        }

        int length = Mathf.Max(1, (int)((long)samples.Length * toRate / fromRate));
        float[] output = new float[length];
        double step = (double)fromRate / toRate;
        for (int i = 0; i < length; i++)
        {
            double source = i * step;
            int index = (int)source;
            float fraction = (float)(source - index);
            float a = samples[Mathf.Min(index, samples.Length - 1)];
            float b = samples[Mathf.Min(index + 1, samples.Length - 1)];
            output[i] = a + (b - a) * fraction;
        }

        return output;
    }

    /// <summary>16-bit mono PCM WAV.</summary>
    public static byte[] EncodeWav(float[] samples, int sampleRate)
    {
        int dataBytes = samples.Length * 2;
        using (MemoryStream memory = new MemoryStream(44 + dataBytes))
        using (BinaryWriter writer = new BinaryWriter(memory))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);      // PCM
            writer.Write((short)1);      // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2); // bytes per second
            writer.Write((short)2);      // block align
            writer.Write((short)16);     // bits per sample
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
            for (int i = 0; i < samples.Length; i++)
            {
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
            }

            writer.Flush();
            return memory.ToArray();
        }
    }

    private static string Escape(string value)
    {
        StringBuilder escaped = new StringBuilder(value.Length + 16);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': escaped.Append("\\\""); break;
                case '\\': escaped.Append("\\\\"); break;
                case '\n': escaped.Append("\\n"); break;
                case '\r': escaped.Append("\\r"); break;
                case '\t': escaped.Append("\\t"); break;
                default:
                    if (c < ' ') escaped.Append("\\u").Append(((int)c).ToString("x4"));
                    else escaped.Append(c);
                    break;
            }
        }

        return escaped.ToString();
    }

    [Serializable]
    private class ChatBody
    {
        public ChatChoice[] choices;
    }

    [Serializable]
    private class ChatChoice
    {
        public ChatMessage message;
    }

    [Serializable]
    private class ChatMessage
    {
        public string content;
    }

    [Serializable]
    private class TranscriptionBody
    {
        public string text;
    }
}
