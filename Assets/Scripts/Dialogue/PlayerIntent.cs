using System;

/// <summary>
/// What a typed player message is trying to do. The keyword lists are about
/// the Thai language, not about any one NPC, so they live here once and the
/// per-NPC reactions live in NpcProfileData.
/// </summary>
[Flags]
public enum PlayerIntent
{
    None = 0,
    Hostile = 1 << 0,
    Apology = 1 << 1,
    Greeting = 1 << 2,
    AskingHint = 1 << 3,
    Thanks = 1 << 4,
    Feelings = 1 << 5,
    Comfort = 1 << 6,
}

public static class PlayerIntentClassifier
{
    private static readonly string[] HostileWords =
    {
        "อย่ามายุ่ง", "ไม่ยุ่ง", "ไปไกลๆ", "ไปให้พ้น", "หุบปาก", "รำคาญ",
        "เงียบ", "เสือก", "น่ารำคาญ", "เกะกะ", "ออกไป", "ไม่ต้องช่วย",
        "ไม่ต้องพูด", "ช่างหัว", "กวนใจ", "ด่า", "บ้า", "ไปตาย", "โง่",
        "shut up", "go away",
    };

    private static readonly string[] ApologyWords =
    {
        "ขอโทษ", "ขออภัย", "ดีกันนะ", "ไม่ได้ตั้งใจ", "sorry",
    };

    private static readonly string[] GreetingWords =
    {
        "สวัสดี", "หวัดดี", "ดีครับ", "ดีค่ะ", "ดีจ้า", "hello", "hi", "hey",
    };

    private static readonly string[] HintWords =
    {
        "ใบ้", "คำใบ้", "ช่วย", "ช่วยด้วย", "ช่วยหน่อย", "ทำยังไง", "ทำไง",
        "ทำอะไรต่อ", "ไปไหนต่อ", "ไปทางไหน", "ติด", "หาไม่เจอ", "อยู่ไหน",
        "แก้ยังไง", "รหัสอะไร", "ทางออก", "ต่อไป", "ต้องทำอะไร", "ดูตรงไหน",
        "หาอะไร", "ทำอะไร", "hint", "help", "what to do", "where", "how to",
    };

    private static readonly string[] ThanksWords =
    {
        "ขอบคุณ", "ขอบใจ", "thank",
    };

    private static readonly string[] FeelingWords =
    {
        "เป็นไง", "เป็นอย่างไร", "รู้สึก", "กลัว", "โอเคไหม", "ไหวไหม",
        "เหนื่อย", "ไม่เป็นไรนะ",
    };

    private static readonly string[] ComfortWords =
    {
        "ไม่ต้องกลัว", "ใจเย็น", "อยู่ด้วย", "ปกป้อง", "ไม่เป็นไร", "ไม่ต้องห่วง",
        "ฉันอยู่นี่", "ปลอดภัย",
    };

    public static PlayerIntent Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return PlayerIntent.None;
        }

        string text = message.ToLowerInvariant();
        PlayerIntent intent = PlayerIntent.None;
        if (ContainsAny(text, HostileWords)) intent |= PlayerIntent.Hostile;
        if (ContainsAny(text, ApologyWords)) intent |= PlayerIntent.Apology;
        if (ContainsAny(text, GreetingWords)) intent |= PlayerIntent.Greeting;
        if (ContainsAny(text, HintWords)) intent |= PlayerIntent.AskingHint;
        if (ContainsAny(text, ThanksWords)) intent |= PlayerIntent.Thanks;
        if (ContainsAny(text, FeelingWords)) intent |= PlayerIntent.Feelings;
        if (ContainsAny(text, ComfortWords)) intent |= PlayerIntent.Comfort;
        return intent;
    }

    public static bool IsHostile(string message)
    {
        return (Classify(message) & PlayerIntent.Hostile) != 0;
    }

    public static bool IsAskingForHint(string message)
    {
        return (Classify(message) & PlayerIntent.AskingHint) != 0;
    }

    public static bool ContainsAny(string source, string[] values)
    {
        if (string.IsNullOrEmpty(source) || values == null)
        {
            return false;
        }

        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value) &&
                source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
