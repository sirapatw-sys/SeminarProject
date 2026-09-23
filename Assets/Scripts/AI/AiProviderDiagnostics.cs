using System.Text.RegularExpressions;

/// <summary>
/// Turns a raw provider error into something a student running the demo can
/// act on. Providers disagree wildly on shape and wording, so this classifies
/// on the status code first and the message text second.
/// </summary>
public enum AiFailureKind
{
    Unknown,
    InvalidApiKey,
    InvalidModel,
    RateLimited,
    DailyLimitReached,
    Network,
    ServerError,
}

public static class AiProviderDiagnostics
{
    public static AiFailureKind Classify(long statusCode, string message)
    {
        string text = (message ?? string.Empty).ToLowerInvariant();

        if (Regex.IsMatch(text, "daily|per[- ]day|quota exceeded|out of credit|โควต"))
        {
            return AiFailureKind.DailyLimitReached;
        }

        if (statusCode == 401 || statusCode == 403 ||
            Regex.IsMatch(text, "api key|apikey|unauthor|invalid token|forbidden"))
        {
            return AiFailureKind.InvalidApiKey;
        }

        if (statusCode == 404 ||
            Regex.IsMatch(text, "model.*(not found|does not exist|invalid)|invalid model|unknown model"))
        {
            return AiFailureKind.InvalidModel;
        }

        if (statusCode == 429 || text.Contains("rate limit") ||
            text.Contains("too many requests"))
        {
            return AiFailureKind.RateLimited;
        }

        if (statusCode >= 500)
        {
            return AiFailureKind.ServerError;
        }

        if (statusCode == 0)
        {
            return AiFailureKind.Network;
        }

        return AiFailureKind.Unknown;
    }

    /// <summary>A short Thai explanation plus the next thing to try.</summary>
    public static string Explain(AiFailureKind kind)
    {
        switch (kind)
        {
            case AiFailureKind.InvalidApiKey:
                return "API key ไม่ถูกต้องหรือหมดอายุ — ตรวจสอบ key แล้วกด APPLY ใหม่";
            case AiFailureKind.InvalidModel:
                return "ชื่อ model ใช้ไม่ได้กับบัญชีนี้ — กด 'โหลดรายชื่อโมเดล' แล้วเลือกจากรายการ";
            case AiFailureKind.RateLimited:
                return "ส่งคำขอถี่เกินไป — รอสักครู่แล้วลองใหม่";
            case AiFailureKind.DailyLimitReached:
                return "ใช้โควตาของวันนี้หมดแล้ว — เกมจะใช้บทสนทนาสำรองไปก่อน";
            case AiFailureKind.Network:
                return "ติดต่อเซิร์ฟเวอร์ไม่ได้ — ตรวจสอบอินเทอร์เน็ตหรือ endpoint";
            case AiFailureKind.ServerError:
                return "ฝั่งผู้ให้บริการขัดข้อง — ลองใหม่อีกครั้งภายหลัง";
            default:
                return string.Empty;
        }
    }

    // Header names used by OpenAI-compatible gateways (OpenAI, LiteLLM,
    // Azure, Groq, ...). Matching is case-insensitive.
    private static readonly string[][] QuotaHeaders =
    {
        new[] { "x-ratelimit-remaining-requests", "x-ratelimit-limit-requests", "คำขอ" },
        new[] { "x-ratelimit-remaining-tokens", "x-ratelimit-limit-tokens", "โทเคน" },
        new[] { "x-ratelimit-remaining-requests-day", "x-ratelimit-limit-requests-day", "คำขอวันนี้" },
        new[] { "x-ratelimit-remaining", "x-ratelimit-limit", "คำขอ" },
    };

    /// <summary>
    /// A short Thai line describing how much quota is left, read from the
    /// rate-limit headers or from a quota/credits field in the body. Empty
    /// when the provider sends neither, which is the honest answer: most
    /// gateways, KKU included, do not advertise it on every response.
    /// </summary>
    public static string ReadQuota(
        System.Collections.Generic.IDictionary<string, string> headers,
        string body)
    {
        System.Collections.Generic.List<string> parts =
            new System.Collections.Generic.List<string>();

        if (headers != null)
        {
            System.Collections.Generic.Dictionary<string, string> lower =
                new System.Collections.Generic.Dictionary<string, string>();
            foreach (System.Collections.Generic.KeyValuePair<string, string> pair in headers)
            {
                if (pair.Key != null)
                {
                    lower[pair.Key.ToLowerInvariant()] = pair.Value;
                }
            }

            foreach (string[] names in QuotaHeaders)
            {
                string remaining;
                if (!lower.TryGetValue(names[0], out remaining) ||
                    string.IsNullOrWhiteSpace(remaining))
                {
                    continue;
                }

                string limit;
                string text = lower.TryGetValue(names[1], out limit) &&
                              !string.IsNullOrWhiteSpace(limit)
                    ? remaining.Trim() + "/" + limit.Trim()
                    : remaining.Trim();
                string entry = names[2] + " " + text;
                if (!parts.Exists(p => p.StartsWith(names[2] + " ")))
                {
                    parts.Add(entry);
                }
            }
        }

        if (parts.Count == 0 && !string.IsNullOrEmpty(body))
        {
            Match match = Regex.Match(
                body,
                "\"(?<key>remaining_quota|quota_remaining|remaining_credits|credits_remaining|remaining_balance|remaining)\"\\s*:\\s*\"?(?<value>[0-9.]+)",
                RegexOptions.IgnoreCase);
            if (match.Success)
            {
                parts.Add("คงเหลือ " + match.Groups["value"].Value);
            }
        }

        return parts.Count == 0 ? string.Empty : string.Join(" · ", parts.ToArray());
    }

    /// <summary>
    /// Strips anything that looks like a bearer token out of text bound for a
    /// log or the settings panel.
    /// </summary>
    public static string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string safe = Regex.Replace(text, @"(?i)bearer\s+\S+", "Bearer ***");
        safe = Regex.Replace(safe, @"\bsk-[A-Za-z0-9_\-]{8,}", "sk-***");
        return safe;
    }
}
