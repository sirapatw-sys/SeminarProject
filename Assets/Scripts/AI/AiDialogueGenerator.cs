using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using UnityEngine;
using UnityEngine.Networking;

public class AiDialogueGenerator : MonoBehaviour
{
    public const string OpenAiResponsesUrl =
        "https://api.openai.com/v1/responses";
    public const string KkuChatCompletionsUrl =
        "https://gen.ai.kku.ac.th/api/v1/chat/completions";
    public const string KkuModelsUrl =
        "https://gen.ai.kku.ac.th/api/v1/models";

    public static AiDialogueGenerator Instance { get; private set; }

    [SerializeField] private bool enableAiGeneration = true;
    [SerializeField] private AiProviderType provider = AiProviderType.OpenAiResponses;
    [SerializeField] private string model = "gpt-4o-mini";
    [SerializeField] private string apiUrl = OpenAiResponsesUrl;
    [SerializeField, Min(5)] private int timeoutSeconds = 20;

    private string sessionApiKey = string.Empty;

    public AiProviderType Provider { get { return provider; } }
    public string Model { get { return model; } }
    public string ApiUrl { get { return apiUrl; } }
    public string LastError { get; private set; }

    public bool CanGenerate
    {
        get
        {
            return enableAiGeneration &&
                   !string.IsNullOrWhiteSpace(GetApiKey()) &&
                   !string.IsNullOrWhiteSpace(model) &&
                   !string.IsNullOrWhiteSpace(apiUrl);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        LoadPreferences();
    }

    public void Configure(
        AiProviderType selectedProvider,
        string selectedApiUrl,
        string selectedModel,
        string apiKey)
    {
        provider = selectedProvider;
        model = (selectedModel ?? string.Empty).Trim();
        apiUrl = GetEndpointForProvider(selectedProvider, selectedApiUrl);
        sessionApiKey = (apiKey ?? string.Empty).Trim();
        LastError = string.Empty;

        PlayerPrefs.SetInt("ai.provider", (int)provider);
        PlayerPrefs.SetString("ai.model", model);
        PlayerPrefs.SetString("ai.endpoint", apiUrl);
        PlayerPrefs.Save();
    }

    public void ClearSessionApiKey()
    {
        sessionApiKey = string.Empty;
        LastError = string.Empty;
    }

    private void LoadPreferences()
    {
        provider = (AiProviderType)PlayerPrefs.GetInt(
            "ai.provider",
            (int)provider
        );
        model = PlayerPrefs.GetString("ai.model", model);
        apiUrl = PlayerPrefs.GetString("ai.endpoint", apiUrl);

        if (provider == AiProviderType.OpenAiResponses)
        {
            apiUrl = OpenAiResponsesUrl;
            if (model == "gpt-5.6-luna" || model == "gpt-5.6-terra")
            {
                model = "gpt-4o-mini";
                PlayerPrefs.SetString("ai.model", model);
                PlayerPrefs.Save();
            }
        }
        else if (provider == AiProviderType.KkuIntelsphere)
        {
            apiUrl = KkuChatCompletionsUrl;
        }
    }

    private static string GetEndpointForProvider(
        AiProviderType selectedProvider,
        string selectedApiUrl)
    {
        switch (selectedProvider)
        {
            case AiProviderType.OpenAiResponses:
                return OpenAiResponsesUrl;
            case AiProviderType.KkuIntelsphere:
                return KkuChatCompletionsUrl;
            default:
                return (selectedApiUrl ?? string.Empty).Trim();
        }
    }

    private string GetApiKey()
    {
        if (!string.IsNullOrWhiteSpace(sessionApiKey))
        {
            return sessionApiKey;
        }

        string environmentName;
        switch (provider)
        {
            case AiProviderType.OpenAiResponses:
                environmentName = "OPENAI_API_KEY";
                break;
            case AiProviderType.KkuIntelsphere:
                environmentName = "KKU_API_KEY";
                break;
            default:
                environmentName = "CUSTOM_AI_API_KEY";
                break;
        }
        return Environment.GetEnvironmentVariable(environmentName);
    }

    public IEnumerator Generate(
        MiniEventData eventData,
        Action<GeneratedDialogueContent> onComplete)
    {
        LastError = string.Empty;
        if (!CanGenerate || eventData == null || eventData.dialogue == null)
        {
            LastError = "การตั้งค่า AI ยังไม่ครบ";
            onComplete(null);
            yield break;
        }

        string apiKey = GetApiKey();
        string prompt = BuildPrompt(eventData);
        string requestJson = provider == AiProviderType.OpenAiResponses
            ? BuildRequestJson(prompt, eventData.dialogue.choices.Count)
            : BuildCompatibleChatRequestJson(prompt);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(
            Encoding.UTF8.GetBytes(requestJson)
        );
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = timeoutSeconds;
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);

        yield return request.SendWebRequest();

        GeneratedDialogueContent result = null;
        if (string.IsNullOrEmpty(request.error))
        {
            result = provider == AiProviderType.OpenAiResponses
                ? ParseResponse(
                    request.downloadHandler.text,
                    eventData.dialogue.choices.Count
                )
                : ParseCompatibleChatResponse(
                    request.downloadHandler.text,
                    eventData.dialogue.choices.Count
                );
            if (result == null)
            {
                LastError = "AI ส่งคำตอบกลับมาในรูปแบบที่เกมอ่านไม่ได้";
            }
        }
        else
        {
            LastError = GetRequestError(request);
            Debug.LogWarning(
                "AI dialogue generation failed; using fallback. " +
                LastError
            );
        }

        request.Dispose();
        onComplete(result);
    }

    public IEnumerator GenerateReply(
        string npcId,
        string speakerName,
        string dialogueContext,
        string playerMessage,
        Action<GeneratedChatReply> onComplete)
    {
        LastError = string.Empty;
        if (!CanGenerate || string.IsNullOrWhiteSpace(playerMessage))
        {
            LastError = "การตั้งค่า AI ยังไม่ครบหรือข้อความว่าง";
            onComplete(null);
            yield break;
        }

        string prompt = BuildReplyPrompt(
            npcId,
            speakerName,
            dialogueContext,
            playerMessage
        );
        string requestJson = provider == AiProviderType.OpenAiResponses
            ? BuildReplyRequestJson(prompt)
            : BuildCompatibleReplyRequestJson(prompt);

        UnityWebRequest request = new UnityWebRequest(apiUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(
            Encoding.UTF8.GetBytes(requestJson)
        );
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = timeoutSeconds;
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + GetApiKey());

        yield return request.SendWebRequest();

        GeneratedChatReply result = null;
        if (string.IsNullOrEmpty(request.error))
        {
            result = provider == AiProviderType.OpenAiResponses
                ? ParseReplyResponse(request.downloadHandler.text)
                : ParseCompatibleReplyResponse(request.downloadHandler.text);
            if (result == null)
            {
                LastError = "AI ตอบกลับมาแล้ว แต่รูปแบบข้อมูลไม่ถูกต้อง";
            }
        }
        else
        {
            LastError = GetRequestError(request);
            Debug.LogWarning(
                "AI typed reply failed; using local fallback. " + LastError
            );
        }

        request.Dispose();
        onComplete(result);
    }

    private static string GetRequestError(UnityWebRequest request)
    {
        string detail = request.error;
        string body = request.downloadHandler != null
            ? request.downloadHandler.text
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                ApiErrorEnvelope envelope =
                    JsonUtility.FromJson<ApiErrorEnvelope>(body);
                if (envelope != null && envelope.error != null &&
                    !string.IsNullOrWhiteSpace(envelope.error.message))
                {
                    detail = envelope.error.message;
                }
            }
            catch (Exception)
            {
                // Keep UnityWebRequest's message when the provider uses another schema.
            }

            // KKU IntelSphere returns errors as {"error":"Invalid model"}
            // instead of OpenAI's nested {"error":{"message":"..."}} shape.
            Match flatError = Regex.Match(
                body,
                "\\\"(?:error|message|detail)\\\"\\s*:\\s*\\\"(?<value>(?:\\\\.|[^\\\"])*)\\\""
            );
            if (flatError.Success)
            {
                detail = Regex.Unescape(flatError.Groups["value"].Value);
            }
        }

        return "HTTP " + request.responseCode + ": " + detail;
    }

    private static string BuildReplyPrompt(
        string npcId,
        string speakerName,
        string dialogueContext,
        string playerMessage)
    {
        StringBuilder prompt = new StringBuilder();
        GameState state = GameState.Instance;
        prompt.AppendLine("ตอบผู้เล่นเป็นภาษาไทยในบทบาท NPC เกม escape room");
        prompt.AppendLine("NPC: " + speakerName + " (" + npcId + ")");
        prompt.AppendLine("บริบทบทสนทนา: " + dialogueContext);
        prompt.AppendLine("ผู้เล่นพิมพ์: " + playerMessage);
        prompt.AppendLine("ตอบสั้น 1-3 ประโยค เป็นธรรมชาติ และรักษา canon");
        prompt.AppendLine("ห้ามสร้างไอเท็ม เบาะแส หรือข้อเท็จจริงใหม่");
        prompt.AppendLine(
            "ให้ relationshipDelta เป็นจำนวนเต็ม -3 ถึง 3 ตามน้ำเสียงของผู้เล่น"
        );

        if (state != null)
        {
            prompt.AppendLine(
                "ความสัมพันธ์ปัจจุบัน: " + state.GetRelationship(npcId)
            );
            IReadOnlyList<string> memories = state.GetNpcMemory(npcId);
            if (memories.Count > 0)
            {
                prompt.AppendLine(
                    "ความทรงจำล่าสุด: " + memories[memories.Count - 1]
                );
            }
        }

        return prompt.ToString();
    }

    private string BuildPrompt(MiniEventData eventData)
    {
        StringBuilder prompt = new StringBuilder();
        GameState state = GameState.Instance;

        prompt.AppendLine("เขียนบทสนทนา mini event ภาษาไทยสำหรับเกม escape room");
        prompt.AppendLine("NPC: " + eventData.dialogue.speakerName);
        prompt.AppendLine("สถานการณ์: " + eventData.situationPrompt);
        prompt.AppendLine("โทน: " + eventData.tonePrompt);
        prompt.AppendLine("เป้าหมายปัจจุบัน: " +
                          (state != null ? state.GetCurrentGoal() : "escape_room"));
        prompt.AppendLine("กฎ: ห้ามสร้างเบาะแส ไอเท็ม ตัวละคร หรือข้อเท็จจริงใหม่");
        prompt.AppendLine("กฎ: บทพูดต้องสั้น เป็นธรรมชาติ และไม่บังคับผู้เล่น");
        prompt.AppendLine("กฎ: รักษาความหมายและลำดับของตัวเลือกต้นฉบับต่อไปนี้");

        for (int index = 0; index < eventData.dialogue.choices.Count; index++)
        {
            DialogueChoiceData choice = eventData.dialogue.choices[index];
            prompt.AppendLine(
                (index + 1) + ". ผู้เล่น: " + choice.optionText +
                " | NPC ตอบ: " + choice.responseText
            );
        }

        if (state != null)
        {
            prompt.AppendLine(
                "ความสัมพันธ์กับผู้เล่น: " +
                state.GetRelationship(eventData.npcId)
            );
            if (eventData.triggerType == MiniEventTriggerType.NpcConflict)
            {
                prompt.AppendLine("NPC ที่เกี่ยวข้อง: " + eventData.relatedNpcId);
                prompt.AppendLine(
                    "ความสัมพันธ์ระหว่าง NPC: " +
                    GetTriggerValue(eventData, state)
                );
            }
            else if (!string.IsNullOrWhiteSpace(eventData.metricId))
            {
                prompt.AppendLine(
                    "ค่าปัจจุบันของ " + eventData.metricId + ": " +
                    GetTriggerValue(eventData, state)
                );
            }

            IReadOnlyList<string> memories =
                state.GetNpcMemory(eventData.npcId);
            if (memories.Count > 0)
            {
                prompt.AppendLine(
                    "ความทรงจำล่าสุด: " + memories[memories.Count - 1]
                );
            }
        }

        prompt.AppendLine("variation_id: " + Guid.NewGuid().ToString("N"));
        return prompt.ToString();
    }

    private static float GetTriggerValue(
        MiniEventData eventData,
        GameState state)
    {
        switch (eventData.triggerType)
        {
            case MiniEventTriggerType.NeedThreshold:
                return state.GetNpcNeed(eventData.npcId, eventData.metricId);
            case MiniEventTriggerType.EmotionThreshold:
                return state.GetNpcEmotion(eventData.npcId, eventData.metricId);
            case MiniEventTriggerType.NpcConflict:
                return state.GetNpcRelationship(
                    eventData.npcId,
                    eventData.relatedNpcId
                );
            default:
                return Time.timeSinceLevelLoad;
        }
    }

    private string BuildRequestJson(string prompt, int choiceCount)
    {
        string escapedModel = EscapeJson(model);
        string escapedPrompt = EscapeJson(prompt);

        return "{" +
               "\"model\":\"" + escapedModel + "\"," +
               "\"store\":false," +
               "\"input\":[{" +
               "\"role\":\"developer\"," +
               "\"content\":[{\"type\":\"input_text\"," +
               "\"text\":\"คุณเป็นนักเขียนบทเกมที่ต้องรักษา canon และตอบตาม schema เท่านั้น\"}]" +
               "},{\"role\":\"user\",\"content\":[{" +
               "\"type\":\"input_text\",\"text\":\"" + escapedPrompt + "\"}]}]," +
               "\"text\":{\"format\":{" +
               "\"type\":\"json_schema\"," +
               "\"name\":\"npc_mini_event_dialogue\"," +
               "\"strict\":true," +
               "\"schema\":{" +
               "\"type\":\"object\"," +
               "\"properties\":{" +
               "\"lines\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"minItems\":1,\"maxItems\":3}," +
               "\"choices\":{\"type\":\"array\",\"items\":{" +
               "\"type\":\"object\",\"properties\":{" +
               "\"optionText\":{\"type\":\"string\"}," +
               "\"responseText\":{\"type\":\"string\"}}," +
               "\"required\":[\"optionText\",\"responseText\"]," +
               "\"additionalProperties\":false}," +
               "\"minItems\":" + choiceCount + ",\"maxItems\":" + choiceCount + "}}," +
               "\"required\":[\"lines\",\"choices\"]," +
               "\"additionalProperties\":false}}}}";
    }

    private string BuildCompatibleChatRequestJson(string prompt)
    {
        return "{" +
               "\"model\":" + FormatCompatibleModel(model) + "," +
               "\"messages\":[{" +
               "\"role\":\"system\",\"content\":\"" +
               "Return only valid JSON with lines and choices. Keep game canon.\"" +
               "},{\"role\":\"user\",\"content\":\"" +
               EscapeJson(prompt) + "\"}]," +
               "\"temperature\":0.85}";
    }

    private string BuildReplyRequestJson(string prompt)
    {
        return "{" +
               "\"model\":\"" + EscapeJson(model) + "\"," +
               "\"store\":false," +
               "\"input\":[{" +
               "\"role\":\"developer\",\"content\":[{" +
               "\"type\":\"input_text\",\"text\":\"" +
               "คุณสวมบท NPC และตอบตาม schema เท่านั้น\"}]}," +
               "{\"role\":\"user\",\"content\":[{" +
               "\"type\":\"input_text\",\"text\":\"" +
               EscapeJson(prompt) + "\"}]}]," +
               "\"text\":{\"format\":{" +
               "\"type\":\"json_schema\"," +
               "\"name\":\"npc_typed_reply\",\"strict\":true," +
               "\"schema\":{" +
               "\"type\":\"object\",\"properties\":{" +
               "\"reply\":{\"type\":\"string\"}," +
               "\"relationshipDelta\":{\"type\":\"integer\"," +
               "\"minimum\":-3,\"maximum\":3}}," +
               "\"required\":[\"reply\",\"relationshipDelta\"]," +
               "\"additionalProperties\":false}}}}";
    }

    private string BuildCompatibleReplyRequestJson(string prompt)
    {
        return "{" +
               "\"model\":" + FormatCompatibleModel(model) + "," +
               "\"messages\":[{" +
               "\"role\":\"system\",\"content\":\"" +
               "Return only JSON with reply and relationshipDelta (-3 to 3).\"}," +
               "{\"role\":\"user\",\"content\":\"" +
               EscapeJson(prompt) + "\"}],\"temperature\":0.8}";
    }

    private static string FormatCompatibleModel(string modelId)
    {
        long numericId;
        return long.TryParse(modelId, out numericId)
            ? numericId.ToString()
            : "\"" + EscapeJson(modelId) + "\"";
    }

    private static string EscapeJson(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private static GeneratedDialogueContent ParseResponse(
        string json,
        int expectedChoiceCount)
    {
        OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(json);
        if (response == null)
        {
            return null;
        }

        GeneratedDialogueContent topLevel = ParseGeneratedDialogueText(
            response.output_text,
            expectedChoiceCount
        );
        if (topLevel != null)
        {
            return topLevel;
        }

        if (response.output == null)
        {
            return null;
        }

        foreach (OpenAiOutputItem item in response.output)
        {
            if (item.content == null)
            {
                continue;
            }

            foreach (OpenAiContentItem content in item.content)
            {
                if (content.type != "output_text" ||
                    string.IsNullOrWhiteSpace(content.text))
                {
                    continue;
                }

                GeneratedDialogueContent dialogue = ParseGeneratedDialogueText(
                    content.text,
                    expectedChoiceCount
                );

                if (dialogue != null)
                {
                    return dialogue;
                }
            }
        }

        Debug.LogWarning("AI dialogue response was invalid; using fallback.");
        return null;
    }

    private static GeneratedDialogueContent ParseGeneratedDialogueText(
        string content,
        int expectedChoiceCount)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        GeneratedDialogueContent dialogue =
            JsonUtility.FromJson<GeneratedDialogueContent>(
                StripCodeFence(content)
            );
        return dialogue != null && dialogue.IsValid(expectedChoiceCount)
            ? dialogue
            : null;
    }

    private static GeneratedDialogueContent ParseCompatibleChatResponse(
        string json,
        int expectedChoiceCount)
    {
        CompatibleChatResponse response =
            JsonUtility.FromJson<CompatibleChatResponse>(json);

        if (response == null || response.choices == null ||
            response.choices.Length == 0 || response.choices[0].message == null)
        {
            return null;
        }

        string content = response.choices[0].message.content;
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        content = StripCodeFence(content);

        GeneratedDialogueContent dialogue =
            JsonUtility.FromJson<GeneratedDialogueContent>(content);
        return dialogue != null && dialogue.IsValid(expectedChoiceCount)
            ? dialogue
            : null;
    }

    private static GeneratedChatReply ParseReplyResponse(string json)
    {
        OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(json);
        if (response == null)
        {
            return null;
        }

        GeneratedChatReply topLevel = ParseReplyText(response.output_text);
        if (topLevel != null)
        {
            return topLevel;
        }

        if (response.output == null)
        {
            return null;
        }

        foreach (OpenAiOutputItem item in response.output)
        {
            if (item.content == null)
            {
                continue;
            }

            foreach (OpenAiContentItem content in item.content)
            {
                if (content.type != "output_text" ||
                    string.IsNullOrWhiteSpace(content.text))
                {
                    continue;
                }

                GeneratedChatReply reply = ParseReplyText(content.text);
                if (reply != null)
                {
                    return reply;
                }
            }
        }

        return null;
    }

    private static GeneratedChatReply ParseReplyText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        GeneratedChatReply reply =
            JsonUtility.FromJson<GeneratedChatReply>(
                StripCodeFence(content)
            );
        return reply != null && reply.IsValid() ? reply : null;
    }

    private static GeneratedChatReply ParseCompatibleReplyResponse(string json)
    {
        CompatibleChatResponse response =
            JsonUtility.FromJson<CompatibleChatResponse>(json);
        if (response == null || response.choices == null ||
            response.choices.Length == 0 || response.choices[0].message == null)
        {
            return null;
        }

        string content = StripCodeFence(response.choices[0].message.content);
        GeneratedChatReply reply =
            JsonUtility.FromJson<GeneratedChatReply>(content);
        return reply != null && reply.IsValid() ? reply : null;
    }

    private static string StripCodeFence(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        content = content.Trim();
        if (!content.StartsWith("```"))
        {
            return content;
        }

        int firstNewLine = content.IndexOf('\n');
        int lastFence = content.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewLine >= 0 && lastFence > firstNewLine
            ? content.Substring(
                firstNewLine + 1,
                lastFence - firstNewLine - 1
            ).Trim()
            : content;
    }

    [Serializable]
    private class OpenAiResponse
    {
        public string output_text;
        public OpenAiOutputItem[] output;
    }

    [Serializable]
    private class ApiErrorEnvelope
    {
        public ApiErrorBody error;
    }

    [Serializable]
    private class ApiErrorBody
    {
        public string message;
    }

    [Serializable]
    private class OpenAiOutputItem
    {
        public OpenAiContentItem[] content;
    }

    [Serializable]
    private class OpenAiContentItem
    {
        public string type;
        public string text;
    }

    [Serializable]
    private class CompatibleChatResponse
    {
        public CompatibleChatChoice[] choices;
    }

    [Serializable]
    private class CompatibleChatChoice
    {
        public CompatibleChatMessage message;
    }

    [Serializable]
    private class CompatibleChatMessage
    {
        public string content;
    }
}

public enum AiProviderType
{
    OpenAiResponses,
    KkuIntelsphere,
    OpenAiCompatible
}

[Serializable]
public class GeneratedChatReply
{
    public string reply;
    public int relationshipDelta;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(reply) &&
               relationshipDelta >= -3 && relationshipDelta <= 3;
    }
}
