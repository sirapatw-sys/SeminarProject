using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using MysteryGame.Knowledge;
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
    public const string GeminiChatCompletionsUrl =
        "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

    public static AiDialogueGenerator Instance { get; private set; }

    [SerializeField] private bool enableAiGeneration = true;
    [SerializeField] private AiProviderType provider = AiProviderType.KkuIntelsphere;
    [SerializeField] private string model = "gpt-5.6-luna";
    [SerializeField] private string apiUrl = KkuChatCompletionsUrl;
    [SerializeField, Min(5)] private int timeoutSeconds = 20;

    private string sessionApiKey = string.Empty;

    public AiProviderType Provider { get { return provider; } }
    public string Model { get { return model; } }
    public string ApiUrl { get { return apiUrl; } }
    public string LastError { get; private set; }

    /// <summary>Why the most recent call failed, for the settings panel.</summary>
    public static AiFailureKind LastFailureKind { get; private set; }

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
            if (string.IsNullOrWhiteSpace(model))
            {
                model = "gpt-5.6-luna";
                PlayerPrefs.SetString("ai.model", model);
                PlayerPrefs.Save();
            }
        }
        else if (provider == AiProviderType.Gemini)
        {
            apiUrl = GeminiChatCompletionsUrl;
            if (string.IsNullOrWhiteSpace(model) || model == "gemini-2.0-flash")
            {
                model = "gemini-3.6-flash";
                PlayerPrefs.SetString("ai.model", model);
                PlayerPrefs.Save();
            }
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
            case AiProviderType.Gemini:
                return GeminiChatCompletionsUrl;
            default:
                return (selectedApiUrl ?? string.Empty).Trim();
        }
    }

    public string GetApiKey()
    {
        if (!string.IsNullOrWhiteSpace(sessionApiKey))
        {
            return sessionApiKey;
        }

        string environmentName;
        string fileName;
        switch (provider)
        {
            case AiProviderType.OpenAiResponses:
                environmentName = "OPENAI_API_KEY";
                fileName = "openai_api_key.txt";
                break;
            case AiProviderType.KkuIntelsphere:
                environmentName = "KKU_API_KEY";
                fileName = "kku_api_key.txt";
                break;
            case AiProviderType.Gemini:
                environmentName = "GEMINI_API_KEY";
                fileName = "gemini_api_key.txt";
                break;
            default:
                environmentName = "CUSTOM_AI_API_KEY";
                fileName = "custom_api_key.txt";
                break;
        }

        // 1. Check environment variable (never in git)
        string envKey = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(envKey))
        {
            return envKey.Trim();
        }

        // 2. Check UserSettings/ folder (in .gitignore)
        try
        {
            string userSettingsPath = System.IO.Path.Combine(Application.dataPath, "..", "UserSettings", fileName);
            if (System.IO.File.Exists(userSettingsPath))
            {
                string key = System.IO.File.ReadAllText(userSettingsPath).Trim();
                if (!string.IsNullOrWhiteSpace(key)) return key;
            }
        }
        catch (Exception) { }

        // 3. Check api_keys.json (in .gitignore)
        try
        {
            string jsonPath = System.IO.Path.Combine(Application.dataPath, "..", "api_keys.json");
            if (System.IO.File.Exists(jsonPath))
            {
                string jsonText = System.IO.File.ReadAllText(jsonPath);
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(
                    jsonText,
                    "\\\"" + environmentName + "\\\"\\s*:\\s*\\\"(?<val>[^\\\"]+)\\\""
                );
                if (m.Success)
                {
                    return m.Groups["val"].Value.Trim();
                }
            }
        }
        catch (Exception) { }

        return string.Empty;
    }

    // The model keeps reaching for ทวาร when it plays up Sena's archaic voice.
    // It is an old word for "gate" but reads as an anatomical term in modern
    // Thai, so the canon says ประตู and anything generated is rewritten here.
    // The prompt asks for this too; this pass is what actually guarantees it.
    private const string BannedGateWord = "ทวาร";
    private const string GateWord = "ประตู";

    private static string ScrubGateWord(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text.Replace(BannedGateWord, GateWord);
    }

    private static void ScrubGateWord(GeneratedDialogueContent content)
    {
        if (content == null)
        {
            return;
        }

        if (content.lines != null)
        {
            for (int i = 0; i < content.lines.Length; i++)
            {
                content.lines[i] = ScrubGateWord(content.lines[i]);
            }
        }

        if (content.choices == null)
        {
            return;
        }

        foreach (GeneratedDialogueChoice choice in content.choices)
        {
            if (choice == null)
            {
                continue;
            }

            choice.optionText = ScrubGateWord(choice.optionText);
            choice.responseText = ScrubGateWord(choice.responseText);
        }
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
            ScrubGateWord(result);

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
        Action<GeneratedChatReply> onComplete,
        string customPersonality = null)
    {
        LastError = string.Empty;
        if (!CanGenerate || string.IsNullOrWhiteSpace(playerMessage))
        {
            LastError = "การตั้งค่า AI ยังไม่ครบหรือข้อความว่าง";
            onComplete(null);
            yield break;
        }

        NpcKnowledgeContext knowledge = BuildKnowledgeContext(
            npcId, playerMessage);

        string prompt = BuildReplyPrompt(
            npcId,
            speakerName,
            dialogueContext,
            playerMessage,
            customPersonality,
            knowledge
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
            else
            {
                result.reply = ScrubGateWord(result.reply);
            }

            if (result != null && knowledge.HasData)
            {
                string offendingFactId;
                if (!knowledge.ValidateReferences(
                        result.referencedFactIds, out offendingFactId))
                {
                    LastError =
                        "AI อ้างถึงข้อมูลที่ตัวละครนี้ไม่มีสิทธิ์รู้ (" +
                        offendingFactId + ") จึงใช้คำตอบสำรองแทน";
                    Debug.LogWarning(LastError);
                    result = null;
                }
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

        AiFailureKind kind =
            AiProviderDiagnostics.Classify(request.responseCode, detail);
        string advice = AiProviderDiagnostics.Explain(kind);

        LastFailureKind = kind;

        string summary = "HTTP " + request.responseCode + ": " +
                         AiProviderDiagnostics.Redact(detail);
        return string.IsNullOrEmpty(advice)
            ? summary
            : advice + " (" + summary + ")";
    }

    /// <summary>
    /// Asks the provider which models this key may use. Only the
    /// OpenAI-compatible providers expose /models, so the OpenAI Responses
    /// preset keeps its curated list.
    /// </summary>
    public IEnumerator FetchAvailableModels(Action<string[], string> onComplete)
    {
        string url = ModelsUrlForProvider();
        if (string.IsNullOrWhiteSpace(url))
        {
            onComplete(null, "ผู้ให้บริการนี้ไม่มีรายการโมเดลให้ดึง");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(GetApiKey()))
        {
            onComplete(null, "ใส่ API key ก่อนจึงจะโหลดรายชื่อโมเดลได้");
            yield break;
        }

        UnityWebRequest request = UnityWebRequest.Get(url);
        request.timeout = timeoutSeconds;
        request.SetRequestHeader("Authorization", "Bearer " + GetApiKey());

        yield return request.SendWebRequest();

        if (!string.IsNullOrEmpty(request.error))
        {
            string error = GetRequestError(request);
            request.Dispose();
            onComplete(null, error);
            yield break;
        }

        string body = request.downloadHandler.text;
        request.Dispose();

        List<string> ids = new List<string>();
        foreach (Match match in Regex.Matches(
                     body, "\"id\"\\s*:\\s*\"(?<id>[^\"]+)\""))
        {
            string id = match.Groups["id"].Value;
            if (!ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            onComplete(null, "ดึงรายการสำเร็จ แต่ไม่พบโมเดลที่บัญชีนี้ใช้ได้");
            yield break;
        }

        ids.Sort(StringComparer.OrdinalIgnoreCase);
        onComplete(ids.ToArray(), string.Empty);
    }

    private string ModelsUrlForProvider()
    {
        if (provider == AiProviderType.KkuIntelsphere)
        {
            return KkuModelsUrl;
        }

        if (provider == AiProviderType.OpenAiCompatible ||
            provider == AiProviderType.Gemini)
        {
            // /chat/completions -> /models on any OpenAI-compatible service.
            int index = apiUrl.LastIndexOf("/chat/completions",
                                           StringComparison.OrdinalIgnoreCase);
            return index > 0 ? apiUrl.Substring(0, index) + "/models" : null;
        }

        return null;
    }

    public static bool ContainsAnyNegative(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string lower = text.ToLowerInvariant();
        string[] negatives = new string[]
        {
            "อย่ามายุ่ง", "ไม่ยุ่ง", "ไปไกลๆ", "ไปให้พ้น", "หุบปาก", "รำคาญ",
            "เงียบ", "เสือก", "น่ารำคาญ", "เกะกะ", "ออกไป", "ไม่ต้องช่วย",
            "ไม่ต้องพูด", "ช่างหัว", "กวนใจ", "ด่า", "บ้า", "ไปตาย", "shut up", "go away"
        };
        foreach (string neg in negatives)
        {
            if (lower.Contains(neg))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsAskingForHelpOrHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string lower = text.ToLowerInvariant();
        string[] hintKeywords = new string[]
        {
            "ใบ้", "คำใบ้", "ช่วย", "ช่วยด้วย", "ช่วยหน่อย", "ทำยังไง", "ทำไง", "ทำอะไรต่อ",
            "ไปไหนต่อ", "ไปทางไหน", "ติด", "หาไม่เจอ", "อยู่ไหน", "แก้ยังไง", "รหัสอะไร",
            "ทางออก", "ต่อไป", "ต้องทำอะไร", "ดูตรงไหน", "หาอะไร", "ทำอะไร",
            "hint", "help", "what to do", "where", "how to"
        };
        foreach (string kw in hintKeywords)
        {
            if (lower.Contains(kw))
            {
                return true;
            }
        }
        return false;
    }


    /// <summary>
    /// The slice of authored canon this NPC may use for this message. Returns
    /// an empty context for rooms that have no RoomKnowledgeData yet, which
    /// keeps the older hard-coded prompt working.
    /// </summary>
    public static NpcKnowledgeContext BuildKnowledgeContext(
        string npcId,
        string playerMessage)
    {
        GameState state = GameState.Instance;
        string roomId = state != null ? state.GetCurrentScene() : string.Empty;
        return NpcKnowledgeContextBuilder.Build(
            npcId,
            roomId,
            state,
            IsAskingForHelpOrHint(playerMessage)
        );
    }

    private static string BuildReplyPrompt(
        string npcId,
        string speakerName,
        string dialogueContext,
        string playerMessage,
        string customPersonality = null,
        NpcKnowledgeContext knowledge = null)
    {
        StringBuilder prompt = new StringBuilder();
        GameState state = GameState.Instance;

        int relationship = state != null ? state.GetRelationship(npcId) : 50;
        bool isHostileMessage = ContainsAnyNegative(playerMessage);
        bool isAskingForHelp = IsAskingForHelpOrHint(playerMessage);
        bool isLowRelationship = relationship < 45;

        prompt.AppendLine("คุณคือ " + speakerName + " (" + npcId + ") ในเกม escape room แนวลึกลับ 2D");

        string currentScene = state != null ? state.GetCurrentScene() : string.Empty;
        bool isRoom02 = currentScene == "Room02" || (state != null && state.HasFlag("room02_entered"));

        bool isSena = (speakerName != null && speakerName.ToLowerInvariant().Contains("sena")) ||
                      (npcId != null && npcId.ToLowerInvariant().Contains("sena"));
        bool isAlice = (speakerName != null && speakerName.ToLowerInvariant().Contains("alice")) ||
                       (npcId != null && npcId.ToLowerInvariant().Contains("alice"));

        prompt.AppendLine("คุณคือ " + speakerName + " (" + npcId + ") ในเกม escape room แนวลึกลับ 2D");

        string personalityDescription;
        if (!string.IsNullOrWhiteSpace(customPersonality))
        {
            personalityDescription = customPersonality;
        }
        else if (isSena)
        {
            personalityDescription =
                "บุคลิกพื้นฐาน: เซนะ (Sena) นางฟ้าผู้เฝ้าประตูดวงดาว (The Celestial Gatekeeper) มีปีกสีขาวบริสุทธิ์และรัศมีศักดิ์สิทธิ์ ยืนเฝ้าประตูบานนี้มาแล้วสามพันฤดู\n" +
                "สำนวนการพูด (สำคัญมาก): พูดจาแบบโบราณ วรรณคดี เพราะนางมีอายุนับพันปี ใช้คำว่า 'ข้า' แทนตัวเอง 'เจ้า' แทนผู้เล่น และใช้คำโบราณเช่น 'มิ' (ไม่), 'ฤๅ' (หรือ), 'เถิด', 'ดอก', 'แล', 'ครานี้', 'เสาะหา', 'จารึก'\n" +
                "ลักษณะนิสัย (High Ego / หยิ่งยโส / ดูถูกมนุษย์): มั่นใจในตนเองสูงส่ง มองมนุษย์เป็นสิ่งมีชีวิตชั้นต่ำที่ไร้ความสามารถ พูดจาข่ม กดขี่ และเยาะเย้ยผู้เล่นตลอดเวลา\n" +
                "คำพูดติดปาก: 'มนุษย์ช่างไร้ความสามารถ', 'ข้าเห็นผู้เช่นเจ้ามาแล้วนับร้อย', 'เจ้ามิมีวันผ่านประตูนี้ไปได้ดอก', 'ข้ามิใช่พี่เลี้ยงของเจ้า'\n" +
                "ภารกิจ: เฝ้าประตูสู่ห้องถัดไป โดยจะเรียกของถวายก่อน แล้วจึงตั้งปริศนา";
        }
        else if (isAlice)
        {
            personalityDescription =
                "บุคลิกพื้นฐาน: เป็นคนเงียบขรึม พูดน้อย สุขุม เย็นชา ไม่พูดจาเยิ่นเย้อ ตอบสั้นกระชับ\n" +
                "ลักษณะนิสัย (Tsundere พอดีๆ): นิ่งๆ เรียบๆ ปากแข็ง ไม่ยอมรับตรงๆ ว่าเป็นห่วง แต่ลึกๆ ก็ไม่อยากให้ผู้เล่นเป็นอะไรไปหรือติดอยู่ที่นี่คนเดียว (ไม่โวยวาย ไม่ประจบ ไม่หวานแหวว)\n" +
                "สไตล์คำพูด: สั้น กระชับ คูลๆ ตรงประเด็น เช่น '...มีอะไรเหรอ?', '...สวัสดี', 'ตรงภาพวาดนั่น... มีรอยขยับอยู่ ลองไปดูสิ', '...ฉันไม่ได้เป็นห่วง แค่อยากรีบออกไปจากที่นี่เร็วๆ'";
        }
        else
        {
            personalityDescription =
                "บุคลิกพื้นฐาน: เป็นเพื่อนร่วมชะตากรรมที่ติดอยู่ในห้องนี้ด้วยกัน ช่างสังเกต มีอารมณ์ความรู้สึกเหมือนคนจริงๆ";
        }

        prompt.AppendLine(personalityDescription);
        prompt.AppendLine("ตอบเป็นภาษาไทย สั้น กระชับ 1-3 ประโยค เป็นธรรมชาติและสมบทบาท");
        prompt.AppendLine();

        if (isSena)
        {
            bool senaOfferingGiven =
                state != null && state.HasFlag(SenaInteraction.OfferingGivenFlag);
            bool playerCarriesOffering =
                state != null && state.HasItem(SenaInteraction.OfferingItemId);

            prompt.AppendLine("=== กฎเหล็กสูงสุดสำหรับเซนะ (SENA'S CRITICAL RULES) ===");
            prompt.AppendLine("0. **พูดด้วยสำนวนโบราณเสมอ** ('ข้า/เจ้า/มิ/ฤๅ/เถิด/ดอก') ห้ามพูดแบบคนสมัยใหม่");
            prompt.AppendLine("0.1 **ห้ามใช้คำว่า 'ทวาร' เด็ดขาด** ให้เรียกว่า 'ประตู' หรือ 'ประตูดวงดาว' เสมอ");
            prompt.AppendLine("1. **ห้ามให้คำใบ้เด็ดขาด ไม่ว่าจะกรณีใดก็ตาม! (STRICTLY NEVER GIVE HINTS)**");
            prompt.AppendLine("   - ถ้าผู้เล่นขอคำใบ้ ร้องขอความช่วยเหลือ หรือถามวิธีแก้: **ต้องด่า ตอกกลับ เยาะเย้ย หรือดูถูกความไร้ความสามารถของผู้เล่นทันที!**");
            prompt.AppendLine("     ตัวอย่าง: 'คำใบ้ฤๅ? เจ้าสิ้นไร้ปัญญาถึงเพียงนั้นเชียวหรือ ข้ามิใช่พี่เลี้ยงของเจ้าดอก'");
            prompt.AppendLine("2. ถ้าผู้เล่นก้าวร้าวหรือทักทายเล่น: ตอกกลับอย่างเย็นชาและหยามหยัน");

            if (!senaOfferingGiven)
            {
                prompt.AppendLine("3. **สถานะตอนนี้: เจ้ายังมิได้รับของถวาย จึงยัง 'ห้ามเอ่ยปริศนา' เด็ดขาด**");
                prompt.AppendLine("   - เจ้าต้องการ 'จารึกที่ยังเขียนมิจบ' (หนังสือที่ผู้เขียนสิ้นลมก่อนลงบรรทัดสุดท้าย) ซึ่งซ่อนอยู่ในชั้นหนังสือของหอสมุดแห่งนี้");
                prompt.AppendLine("   - ถ้าผู้เล่นถามถึงปริศนา ให้บอกว่าผู้มามือเปล่ามิมีสิทธิ์ได้ยินปริศนาของเจ้า จงไปเสาะหาของถวายมาก่อน");
                prompt.AppendLine("   - **ห้ามบอกว่าหนังสืออยู่ชั้นไหน หรืออยู่ที่ใด** นั่นคือคำใบ้ และเจ้าไม่ให้คำใบ้");
                if (playerCarriesOffering)
                {
                    prompt.AppendLine("   - ผู้เล่นถือจารึกนั้นมาแล้ว: ให้บอกให้เขายื่นมันแก่เจ้า (กด E คุยอีกครั้ง)");
                }
            }
            else
            {
                prompt.AppendLine("3. **สถานะตอนนี้: เจ้ารับของถวายแล้ว และได้เอ่ยปริศนาไปแล้ว**");
                prompt.AppendLine("   - ปริศนาคือ: 'I am always running ahead of you, yet I never arrive. You spend your life anticipating me, but the moment I reach you, my name has already changed. What am I?'");
                prompt.AppendLine("   - คำตอบที่ถูกต้องคือ 'Tomorrow' (วันพรุ่งนี้ / พรุ่งนี้)");
                prompt.AppendLine("   - ถ้าผู้เล่นตอบถูก: แสดงความพอใจแบบผู้สูงส่ง เช่น '...หึ เจ้าตอบถูกจนได้ฤๅ ข้ายอมรับในปัญญาของเจ้าในครานี้'");
                prompt.AppendLine("   - ถ้าผู้เล่นตอบผิด: เยาะเย้ยและไล่ให้ไปคิดมาใหม่");
            }

            prompt.AppendLine();
        }
        else
        {
            prompt.AppendLine("=== กฎเหล็กสูงสุดในการตอบ (CRITICAL RULES) ===");
            prompt.AppendLine("1. **การให้คำใบ้ปริศนา (Puzzle Hints): ให้ทำได้ 'เฉพาะ' เมื่อผู้เล่นร้องขอความช่วยเหลือ หรือถามหาคำใบ้เท่านั้น!**");
            prompt.AppendLine("   - ถ้าผู้เล่น 'ไม่ได้ขอคำใบ้' หรือ 'ไม่ได้ถามวิธีแก้ปริศนา' (เช่น ทักทาย 'สวัสดี', ถามชื่อ, ชวนคุยเล่น, ถามความรู้สึก, คุยทั่วไป):");
            prompt.AppendLine("     -> **ห้ามใส่คำใบ้ ห้ามบอกเบาะแส และห้ามชวนไปสำรวจสิ่งของใดๆ ในห้องเด็ดขาด!**");
            prompt.AppendLine("     -> ให้ตอบสนทนาทั่วไปตามลักษณะนิสัยของตัวละครเท่านั้น");
            prompt.AppendLine("   - ถ้าผู้เล่น 'ขอความช่วยเหลือ' หรือ 'ถามหาคำใบ้' (เช่น 'ขอคำใบ้หน่อย', 'ช่วยหน่อย', 'ต้องทำยังไงต่อ', 'ไปทางไหน'):");
            prompt.AppendLine("     -> ให้ตอบคำใบ้ตามระดับความสนิทและสถานะปัจจุบัน");
            prompt.AppendLine("2. ห้ามคิดค้นไอเท็ม เบาะแส หรือข้อเท็จจริงใหม่ที่ไม่มีในห้องนี้เด็ดขาด");
            prompt.AppendLine("3. หากผู้เล่นถามถึงสิ่งของหรือเรื่องที่ไม่มีในห้อง ให้ตอบตามความจริงว่าไม่รู้ หรือในห้องนี้ไม่มีสิ่งนั้น");
            prompt.AppendLine();
        }

        prompt.AppendLine("=== การวิเคราะห์เจตนาของผู้เล่นในข้อความนี้ ===");
        if (isAskingForHelp)
        {
            prompt.AppendLine("เจตนา: [ผู้เล่นกำลังขอความช่วยเหลือ หรือขอคำใบ้ปริศนา]");
            prompt.AppendLine("คำสั่ง: ให้คำใบ้ตามระดับความสนิทและสถานะปริศนาปัจจุบันด้านล่าง (ยกเว้นเซนะที่ห้ามใบ้เด็ดขาด)");
        }
        else if (isHostileMessage)
        {
            prompt.AppendLine("เจตนา: [ผู้เล่นพูดจาไม่ดี / ก้าวร้าว / ปฏิเสธ / ไล่]");
            prompt.AppendLine("คำสั่ง: ตอบกลับอย่างเย็นชา เคือง หรือบอกปัด และห้ามให้คำใบ้เด็ดขาด");
        }
        else
        {
            prompt.AppendLine("เจตนา: [ผู้เล่นทักทาย หรือคุยเรื่องทั่วไป (ไม่ได้ขอคำใบ้)]");
            prompt.AppendLine("คำสั่ง: ตอบกลับการทักทายหรือการสนทนาตามนิสัยตัวละคร **ห้ามใส่คำใบ้ปริศนา และห้ามชวนไปตรวจสิ่งของใดๆ ในห้องเด็ดขาด!**");
            if (isAlice)
            {
                prompt.AppendLine("ตัวอย่างการตอบของ Alice เมื่อทักทาย: '...มีอะไรเหรอ?', '...สวัสดี', 'ถ้าไม่มีอะไรก็อย่าชวนคุยเรื่อยเปื่อยเลย', '...อืม'");
            }
        }
        prompt.AppendLine();

        if (knowledge != null && knowledge.HasData)
        {
            // Authored canon wins: the room owns its own facts, hint
            // ladder and forbidden knowledge.
            prompt.AppendLine(knowledge.ToPromptSection());
        }
        else
        {
            if (isRoom02)
            {
                bool r2WantsOffering =
                    state != null && state.HasFlag(SenaInteraction.WantsOfferingFlag);
                bool r2OfferingGiven =
                    state != null && state.HasFlag(SenaInteraction.OfferingGivenFlag);
                bool r2ReadLedger = state != null && state.HasFlag("read_ledger");
                bool r2HasTome =
                    state != null && state.HasItem(SenaInteraction.OfferingItemId);

                prompt.AppendLine("=== ข้อมูลความจริงใน Room02 (Canon Facts) ===");
                prompt.AppendLine("ห้องนี้คือ Room 02 (หอสมุดแห่งดวงดาว) เป็นหอสมุดเพดานสูง ผนังซ้ายเป็นตู้หนังสือสูงเรียงกันสามชั้นใหญ่ มีประตูดวงดาวเรืองแสงอยู่กลางผนังด้านบน");
                prompt.AppendLine("สิ่งที่มีอยู่จริงในห้องนี้:");
                prompt.AppendLine("1. นาฬิกาโบราณ (Grandfather Clock) ฝั่งซ้ายของประตู: เข็มกำลังไต่ขึ้นสู่เที่ยงคืน มีแผ่นทองเหลืองสลักว่า 'เมื่อเข็มข้ามเที่ยงคืนไป สิ่งที่เจ้าเฝ้ารอทั้งคืนจะเปลี่ยนชื่อของมันทันที'");
                prompt.AppendLine("2. ตู้หนังสือชั้นบน (ฝั่งซ้าย): หนังสือเรียงกันนับพันเล่ม มีป้ายบอกว่าทุกเล่มถูกลงทะเบียนไว้ในสมุดทะเบียนบนโต๊ะอ่านหนังสือ");
                prompt.AppendLine("3. ตู้หนังสือชั้นกลาง (ฝั่งซ้าย): มีบทกวีบทหนึ่งว่า 'ข้าวิ่งนำหน้าเจ้าเสมอ แต่ไม่เคยไปถึงไหน เจ้าใช้ทั้งชีวิตรอคอยข้า แต่วินาทีที่ข้ามาถึง ข้าก็ถูกเรียกด้วยชื่ออื่นไปแล้ว'");
                prompt.AppendLine("4. ตู้หนังสือชั้นล่าง (ฝั่งซ้าย): เป็นที่เก็บ 'จารึกที่ยังเขียนมิจบ' แต่หาไม่เจอถ้ายังไม่รู้เลขชั้นจากสมุดทะเบียน");
                prompt.AppendLine("5. โต๊ะอ่านหนังสือ (ฝั่งขวา): มีสมุดทะเบียนหนังสือของหอสมุดเปิดค้างอยู่ ใช้ค้นว่าหนังสือเล่มใดอยู่ชั้นใด");
                prompt.AppendLine("6. แท่นไฟศักดิ์สิทธิ์ข้างประตู: มีศิลาจารึกเล่าเรื่องเซนะ ผู้เฝ้าประตูที่เรียกของถวายก่อนจะตั้งปริศนา และไม่เคยให้คำใบ้แก่ผู้ใด");
                prompt.AppendLine("7. หีบศิลาใกล้ผนังล่าง: ว่างเปล่า มีแต่ฝุ่น");
                prompt.AppendLine("8. เซนะ (Sena): นางฟ้าเฝ้าประตู พูดสำนวนโบราณ หยิ่งยโส ไม่ช่วยใบ้อะไรเลย");
                prompt.AppendLine("ลำดับการผ่านห้องนี้ (ห้ามเปลี่ยนแปลง):");
                prompt.AppendLine("  ขั้นที่ 1 คุยกับเซนะ -> นางเรียก 'จารึกที่ยังเขียนมิจบ' เป็นของถวาย");
                prompt.AppendLine("  ขั้นที่ 2 อ่านสมุดทะเบียนบนโต๊ะอ่านหนังสือ -> รู้ว่าจารึกอยู่ชั้นล่างสุดของตู้ฝั่งซ้าย");
                prompt.AppendLine("  ขั้นที่ 3 หยิบจารึกจากตู้หนังสือชั้นล่าง");
                prompt.AppendLine("  ขั้นที่ 4 นำจารึกไปให้เซนะ -> นางจึงเอ่ยปริศนา");
                prompt.AppendLine("  ขั้นที่ 5 ตอบปริศนาว่า 'Tomorrow' (พรุ่งนี้) -> เซนะจางหายไป ประตูเปิด");
                prompt.AppendLine("ความคืบหน้าตอนนี้:");
                prompt.AppendLine("  - เซนะเรียกของถวายแล้ว: " + (r2WantsOffering ? "ใช่" : "ยังไม่ได้คุยกับเซนะเลย"));
                prompt.AppendLine("  - อ่านสมุดทะเบียนแล้ว: " + (r2ReadLedger ? "ใช่ (รู้เลขชั้นแล้ว)" : "ยังไม่ได้อ่าน"));
                prompt.AppendLine("  - ถือจารึกอยู่ในตัว: " + (r2HasTome ? "ใช่" : "ไม่มี"));
                prompt.AppendLine("  - มอบของถวายให้เซนะแล้ว: " + (r2OfferingGiven ? "ใช่ (เซนะเอ่ยปริศนาแล้ว)" : "ยังไม่ได้มอบ (เซนะยังไม่เอ่ยปริศนา)"));
                prompt.AppendLine();

                if (isAlice)
                {
                    prompt.AppendLine("=== กฎพิเศษสำหรับ Alice ใน Room02 (ALICE'S GRADUATED HINTS) ===");
                    prompt.AppendLine("- Alice เป็นฝ่ายที่ช่วยผู้เล่นคิด และใบ้ได้ แต่ใบ้ได้เฉพาะ 'ขั้นตอนถัดไปที่ผู้เล่นทำได้จริงตอนนี้' เท่านั้น ห้ามข้ามขั้น");

                    if (!r2WantsOffering)
                    {
                        prompt.AppendLine("- ขั้นตอนถัดไปของผู้เล่น: ไปคุยกับนางฟ้าที่ขวางประตูก่อน");
                        prompt.AppendLine("- **ห้ามพูดถึงปริศนา ห้ามพูดถึงสมุดทะเบียน และห้ามพูดคำว่า 'พรุ่งนี้'/'Tomorrow' เด็ดขาด** เพราะผู้เล่นยังไม่รู้เรื่องเหล่านี้");
                    }
                    else if (!r2ReadLedger)
                    {
                        prompt.AppendLine("- ขั้นตอนถัดไปของผู้เล่น: ไปอ่านสมุดทะเบียนบนโต๊ะอ่านหนังสือฝั่งขวา");
                        prompt.AppendLine("- ให้ใบ้ว่าหนังสือในหอนี้มีเป็นพันเล่ม ต้องหาเลขชั้นจากสมุดทะเบียนก่อน **ห้ามเฉลยปริศนา และห้ามพูดคำว่า 'พรุ่งนี้'/'Tomorrow'**");
                    }
                    else if (!r2HasTome)
                    {
                        prompt.AppendLine("- ขั้นตอนถัดไปของผู้เล่น: ไปหยิบจารึกจากตู้หนังสือชั้นล่างสุดฝั่งซ้าย");
                        prompt.AppendLine("- **ห้ามเฉลยปริศนา และห้ามพูดคำว่า 'พรุ่งนี้'/'Tomorrow'**");
                    }
                    else if (!r2OfferingGiven)
                    {
                        prompt.AppendLine("- ขั้นตอนถัดไปของผู้เล่น: นำจารึกไปมอบให้เซนะ");
                        prompt.AppendLine("- **ห้ามเฉลยปริศนา และห้ามพูดคำว่า 'พรุ่งนี้'/'Tomorrow'**");
                    }
                    else
                    {
                        int room02CluesFound = DialogueManager.CountRoom02CluesFound();
                        prompt.AppendLine("- เซนะเอ่ยปริศนาแล้ว ตอนนี้ Alice ใบ้เรื่องปริศนาได้ โดยใบ้เป็นขั้นตามจำนวนเบาะแสที่ผู้เล่นอ่านแล้ว (นาฬิกา / บทกวีบนชั้นกลาง / ศิลาจารึกที่แท่นไฟ)");
                        prompt.AppendLine("  * 0 เบาะแส: ใบ้กว้างๆ ว่าเป็นเรื่องของเวลา และชี้ให้ไปอ่านนาฬิกา ชั้นหนังสือ และแท่นไฟ ห้ามพูดคำตอบ");
                        prompt.AppendLine("  * 1-2 เบาะแส: บอกว่าคำตอบไม่ใช่สิ่งของ แต่เป็น 'วัน' ที่เฝ้ารอและไม่มีวันไปถึง ยังห้ามพูดคำตอบตรงๆ");
                        prompt.AppendLine("  * ครบ 3 เบาะแส: เฉลยได้เลยว่าคำตอบคือ 'พรุ่งนี้' (Tomorrow) แล้วไล่ให้ไปพิมพ์ตอบเซนะ");
                        prompt.AppendLine("  * ขณะนี้ผู้เล่นอ่านเบาะแสไปแล้ว " + room02CluesFound + "/3 อย่าง");
                    }

                    prompt.AppendLine();
                }
            }
            else
            {
                prompt.AppendLine("=== ข้อมูลความจริงในห้อง Room01 (Canon Facts) ===");
                prompt.AppendLine("ห้องนี้มีสิ่งของและลำดับการไขปริศนาดังนี้:");
                prompt.AppendLine("1. ภาพวาดบนผนัง (Painting): ด้านหลังกรอบรูปมีรูปลูกศรสลักไว้ ชี้ตรงไปที่ซอกขอบโต๊ะทำงานข้างๆ");
                prompt.AppendLine("2. โต๊ะทำงาน (Desk): มีกระดาษโน้ตพับซ่อนอยู่ตรงซอกขอบโต๊ะตามรอยลูกศรจากภาพวาด");
                prompt.AppendLine("   - กระดาษโน้ตเขียนโดยคนที่เคยติดอยู่ ระบายความเครียด และบันทึกรหัส 4 หลักของลิ้นชักไว้คือ 4 5 9 2");
                prompt.AppendLine("3. ลิ้นชัก (Drawer): มีแม่กุญแจรหัส 4 หลักล็อคอยู่ ต้องใส่รหัส 4592 จากกระดาษโน้ต จึงจะเปิดได้และพบกุญแจทองเหลือง (key)");
                prompt.AppendLine("4. ประตูทางออก (Door): ทางออกถูกล็อค ต้องใช้กุญแจทองเหลือง (key) ไขเท่านั้น");
                prompt.AppendLine();
            }
        }

        prompt.AppendLine("=== สถานะของผู้เล่นในขณะนี้ ===");
        if (state != null)
        {
            bool inspectedPainting = state.HasFlag("inspected_painting");
            bool foundNote = state.HasFlag("found_note");
            bool hasKey = state.HasItem("key");
            bool drawerOpened = state.HasFlag("drawer_opened");
            bool doorUnlocked = state.HasFlag("door_unlocked");

            if (isRoom02)
            {
                prompt.AppendLine("- อยู่ใน Room02 (หอสมุดแห่งดวงดาว): ใช่");
            }
            else
            {
                prompt.AppendLine("- ตรวจภาพวาดแล้ว: " + (inspectedPainting ? "ใช่" : "ยังไม่ได้ตรวจ"));
                prompt.AppendLine("- พบกระดาษโน้ตแล้ว: " + (foundNote ? "ใช่ (รู้รหัส 4592 แล้ว)" : "ยังไม่พบ"));
                prompt.AppendLine("- เปิดลิ้นชัก/ได้กุญแจแล้ว: " + (drawerOpened || hasKey ? "ใช่" : "ยังไม่ได้เปิด (ยังล็อคอยู่)"));
                prompt.AppendLine("- มีกุญแจอยู่ในตัว: " + (hasKey ? "มีกุญแจ (key)" : "ไม่มี"));
                prompt.AppendLine("- ประตูปลดล็อคแล้ว: " + (doorUnlocked ? "ปลดล็อคแล้ว" : "ยังล็อคอยู่"));
            }
            prompt.AppendLine("- ระดับความสัมพันธ์กับผู้เล่น: " + relationship + "/100");

            IReadOnlyList<string> memories = state.GetNpcMemory(npcId);
            if (memories.Count > 0)
            {
                prompt.AppendLine("- ความทรงจำล่าสุด: " + memories[memories.Count - 1]);
            }
        }
        else
        {
            prompt.AppendLine("- ผู้เล่นเพิ่งเริ่มสำรวจห้อง");
        }
        prompt.AppendLine();

        prompt.AppendLine("=== สภาวะอารมณ์และระดับความสัมพันธ์ (" + relationship + "/100) ===");
        if (isSena)
        {
            prompt.AppendLine("สถานะของเซนะ: ผู้เฝ้าประตูระดับสูง หยิ่งยโส ดูถูกมนุษย์ ไม่สนใจความสัมพันธ์ ไม่ช่วยใบ้เด็ดขาด");
        }
        else if (isHostileMessage || isLowRelationship)
        {
            prompt.AppendLine("สถานะอารมณ์: [น้อยใจ / เคือง / เย็นชา / ไม่พอใจ]");
            prompt.AppendLine("สาเหตุ: ผู้เล่นพูดจาไม่ดี ไล่ หรือมีท่าทีปฏิเสธ/ก้าวร้าว (เช่น 'อย่ามายุ่ง') หรือความสัมพันธ์ต่ำ (" + relationship + "/100)");
            prompt.AppendLine("กฎเหล็ก:");
            prompt.AppendLine("1. ห้ามให้คำใบ้เด็ดขาด! หากผู้เล่นขอคำใบ้ ให้บอกปัดอย่างเย็นชา เช่น 'ก็บอกว่าอย่ามายุ่งไม่ใช่เหรอ... จัดการเองแล้วกัน', '...'");
            prompt.AppendLine("2. กำหนด relationshipDelta เป็นค่าติดลบระหว่าง -5 ถึง -15 ตามความรุนแรงของคำพูด");
        }
        else if (relationship <= 65)
        {
            prompt.AppendLine("สถานะอารมณ์: [เพิ่งรู้จักกัน / วางตัวนิ่ง / ความสนิทระดับปานกลาง (" + relationship + "/100)]");
            prompt.AppendLine("กฎการตอบ:");
            prompt.AppendLine("1. ถ้าผู้เล่นทักทายหรือคุยทั่วไป: ตอบสั้นๆ ห้วนๆ นิ่งๆ ตามนิสัย เช่น '...มีอะไร?', '...สวัสดี'");
            prompt.AppendLine("2. ถ้าผู้เล่นขอความช่วยเหลือหรือขอคำใบ้: **ต้องใบ้น้อยนิดหน่อย** แบบคลุมเครือ/กว้างๆ เท่านั้น ห้ามบอกเฉลย");
            prompt.AppendLine("3. กำหนด relationshipDelta เป็นค่าระหว่าง 0 ถึง 2 ตามความสุภาพ");
        }
        else if (relationship <= 80)
        {
            prompt.AppendLine("สถานะอารมณ์: [เริ่มสนิทและเปิดใจ / ไว้ใจมากขึ้น (" + relationship + "/100)]");
            prompt.AppendLine("กฎการตอบ:");
            prompt.AppendLine("1. ถ้าผู้เล่นทักทายหรือคุยทั่วไป: ตอบอย่างเป็นมิตรขึ้นเล็กน้อย เช่น 'สวัสดี... มีอะไรให้ช่วยมั้ย'");
            prompt.AppendLine("2. ถ้าผู้เล่นขอความช่วยเหลือหรือขอคำใบ้: ช่วยบอกเบาะแสชัดเจนขึ้นในขั้นตอนปัจจุบัน");
            prompt.AppendLine("3. กำหนด relationshipDelta เป็นค่าระหว่าง 0 ถึง 3");
        }
        else
        {
            prompt.AppendLine("สถานะอารมณ์: [สนิทมาก / ผูกพัน / เป็นห่วงและไว้ใจเต็มที่ (" + relationship + "/100)]");
            prompt.AppendLine("กฎการตอบ:");
            prompt.AppendLine("1. ถ้าผู้เล่นทักทายหรือคุยทั่วไป: ตอบอย่างอบอุ่นและเป็นห่วง");
            prompt.AppendLine("2. ถ้าผู้เล่นขอความช่วยเหลือหรือขอคำใบ้: แนะนำเบาะแสอย่างละเอียดครบถ้วน คอยเตือนและให้กำลังใจ");
            prompt.AppendLine("3. กำหนด relationshipDelta เป็นค่าระหว่าง 1 ถึง 4");
        }
        prompt.AppendLine();

        prompt.AppendLine("บริบทปัจจุบัน: " + dialogueContext);
        prompt.AppendLine("ผู้เล่นพูดว่า: \"" + playerMessage + "\"");

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
               "\"minimum\":-15,\"maximum\":5}," +
               "\"referencedFactIds\":{\"type\":\"array\"," +
               "\"items\":{\"type\":\"string\"}}}," +
               "\"required\":[\"reply\",\"relationshipDelta\"," +
               "\"referencedFactIds\"]," +
               "\"additionalProperties\":false}}}}";
    }

    private string BuildCompatibleReplyRequestJson(string prompt)
    {
        return "{" +
               "\"model\":" + FormatCompatibleModel(model) + "," +
               "\"messages\":[{" +
               "\"role\":\"system\",\"content\":\"" +
               "Return only JSON with reply, relationshipDelta (-15 to 5) and referencedFactIds (array of canon fact ids you used).\"}," +
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
    OpenAiResponses = 0,
    KkuIntelsphere = 1,
    Gemini = 2,
    OpenAiCompatible = 3
}

[Serializable]
public class GeneratedChatReply
{
    public string reply;
    public int relationshipDelta;

    /// <summary>
    /// Canon fact ids the model claims to have used. Anything the speaking NPC
    /// is not allowed to know makes the reply invalid.
    /// </summary>
    public string[] referencedFactIds;

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(reply) &&
               relationshipDelta >= -15 && relationshipDelta <= 5;
    }
}
