using System;
using UnityEngine;

/// <summary>
/// Turns a provider's raw HTTP body into dialogue. Every entry point returns
/// null instead of throwing: JsonUtility throws on malformed JSON, and an
/// exception inside the request coroutine would skip the completion callback
/// and leave the chat box stuck on "thinking" forever.
/// </summary>
public static class AiResponseParser
{
    /// <summary>A typed-chat reply from either API shape, or null.</summary>
    public static GeneratedChatReply ParseReply(string body, bool responsesApi)
    {
        try
        {
            return responsesApi
                ? ParseReplyResponse(body)
                : ParseCompatibleReply(body);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AI reply could not be parsed: " + ex.Message);
            return null;
        }
    }

    /// <summary>Mini-event lines and choices from either API shape, or null.</summary>
    public static GeneratedDialogueContent ParseDialogue(
        string body,
        bool responsesApi,
        int expectedChoiceCount)
    {
        try
        {
            return responsesApi
                ? ParseDialogueResponse(body, expectedChoiceCount)
                : ParseCompatibleDialogue(body, expectedChoiceCount);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AI dialogue could not be parsed: " + ex.Message);
            return null;
        }
    }

    /// <summary>The model's own JSON text, tolerant of code fences and chatter.</summary>
    public static GeneratedChatReply ParseReplyText(string content)
    {
        string json = ExtractJsonObject(content);
        if (json == null)
        {
            return null;
        }

        try
        {
            GeneratedChatReply reply = JsonUtility.FromJson<GeneratedChatReply>(json);
            return reply != null && reply.IsValid() ? reply : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static GeneratedDialogueContent ParseDialogueText(
        string content,
        int expectedChoiceCount)
    {
        string json = ExtractJsonObject(content);
        if (json == null)
        {
            return null;
        }

        try
        {
            GeneratedDialogueContent dialogue =
                JsonUtility.FromJson<GeneratedDialogueContent>(json);
            return dialogue != null && dialogue.IsValid(expectedChoiceCount)
                ? dialogue
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Strips a ``` fence and any prose around the outermost {...}. Returns
    /// null when there is no object at all.
    /// </summary>
    public static string ExtractJsonObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            int firstNewLine = content.IndexOf('\n');
            int lastFence = content.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                content = content.Substring(
                    firstNewLine + 1, lastFence - firstNewLine - 1).Trim();
            }
        }

        int start = content.IndexOf('{');
        int end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return content.Substring(start, end - start + 1);
    }

    private static GeneratedChatReply ParseReplyResponse(string body)
    {
        OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(body);
        if (response == null)
        {
            return null;
        }

        GeneratedChatReply topLevel = ParseReplyText(response.output_text);
        if (topLevel != null || response.output == null)
        {
            return topLevel;
        }

        foreach (OpenAiOutputItem item in response.output)
        {
            if (item == null || item.content == null)
            {
                continue;
            }

            foreach (OpenAiContentItem content in item.content)
            {
                if (content == null || content.type != "output_text")
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

    private static GeneratedChatReply ParseCompatibleReply(string body)
    {
        string content = CompatibleContent(body);
        return content == null ? null : ParseReplyText(content);
    }

    private static GeneratedDialogueContent ParseDialogueResponse(
        string body,
        int expectedChoiceCount)
    {
        OpenAiResponse response = JsonUtility.FromJson<OpenAiResponse>(body);
        if (response == null)
        {
            return null;
        }

        GeneratedDialogueContent topLevel =
            ParseDialogueText(response.output_text, expectedChoiceCount);
        if (topLevel != null || response.output == null)
        {
            return topLevel;
        }

        foreach (OpenAiOutputItem item in response.output)
        {
            if (item == null || item.content == null)
            {
                continue;
            }

            foreach (OpenAiContentItem content in item.content)
            {
                if (content == null || content.type != "output_text")
                {
                    continue;
                }

                GeneratedDialogueContent dialogue =
                    ParseDialogueText(content.text, expectedChoiceCount);
                if (dialogue != null)
                {
                    return dialogue;
                }
            }
        }

        return null;
    }

    private static GeneratedDialogueContent ParseCompatibleDialogue(
        string body,
        int expectedChoiceCount)
    {
        string content = CompatibleContent(body);
        return content == null ? null : ParseDialogueText(content, expectedChoiceCount);
    }

    private static string CompatibleContent(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        CompatibleChatResponse response =
            JsonUtility.FromJson<CompatibleChatResponse>(body);
        if (response == null || response.choices == null ||
            response.choices.Length == 0 || response.choices[0] == null ||
            response.choices[0].message == null)
        {
            return null;
        }

        return response.choices[0].message.content;
    }

    [Serializable]
    private class OpenAiResponse
    {
        public string output_text;
        public OpenAiOutputItem[] output;
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
