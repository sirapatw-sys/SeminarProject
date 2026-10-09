using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>Pushing the NPC away: cold, distancing, refusing closeness.</summary>
    Cold = 1 << 7,
}

public static class PlayerIntentClassifier
{
    // Whether a message is rude or cold is decided in one place,
    // PlayerToneClassifier, which reads the whole sentence (negation,
    // sarcasm, who it is aimed at). The lists below are simple topics.

    private static readonly string[] ApologyWords =
    {
        "ขอโทษ", "ขออภัย", "ดีกันนะ", "ไม่ได้ตั้งใจ", "sorry",
    };

    private static readonly string[] GreetingWords =
    {
        "สวัสดี", "หวัดดี", "ดีครับ", "ดีค่ะ", "ดีจ้า", "hello", "hi", "hey",
    };

    private static readonly string[] GenericHelpWords =
    {
        "ช่วยด้วย", "ช่วยหน่อย", "ช่วยเรา", "ช่วยฉัน", "ช่วยผม",
        "ทำอะไรต่อ", "ไปไหนต่อ", "ไปทางไหน", "ติดปริศนา", "ติดด่าน", "ไปต่อไม่ได้", "หาไม่เจอ",
        "แก้ยังไง", "รหัสอะไร", "ดูตรงไหน", "กุญแจอยู่ไหน",
        "help me", "need help", "can you help",
    };
    private static readonly Regex GenericHelpRequest = R(string.Join("|", Array.ConvertAll(GenericHelpWords, Regex.Escape)));

    // Saying you are stuck is asking for help, however it is worded:
    // "ไม่รู้จะทำยังไงต่อดี", "ตันแล้ว", "ไปต่อไม่ถูก", "what now?".
    // Treated like the generic requests above (feelings elsewhere in the
    // message still win, "no hints" still holds).
    private static readonly Regex StuckRequest = R(
        @"ไม่รู้(?:ว่า)?\s*(?:จะ|ต้อง|ควร)\s*(?:ทำ(?!ตัว|ใจ)|ไป|เริ่ม|หา|ดู|เล่น)|" +
        @"(?:ทำ|ไป|เล่น|เริ่ม)\s*(?:ยังไง|ไง|อย่างไร|อะไร|ไหน|ตรงไหน|ทางไหน)\s*ต่อ|" +
        @"ต่อ\s*(?:จากนี้|ไป)?\s*(?:ต้อง|ควร|จะ)?\s*(?:ทำ|ไป)\s*(?:อะไร|ยังไง|อย่างไร|ไหน)|" +
        @"(?:ทาง)?ตัน(?:เลย|อยู่|จริง|\s*$)|(?:คิด|นึก)ไม่ออก(?!ว่าจะ(?:คุย|พูด|ตอบ))|ไปต่อไม่(?:ได้|ถูก|เป็น)|" +
        @"ไม่มี(?:ไอเดีย|ทางไป)|งงไปหมด|ติดอยู่ตรงนี้|ขั้น(?:ต่อไป|ถัดไป)|" +
        @"\b(?:stuck|no\s+idea\s+what|what\s+now|what\s+next|i'?m\s+lost|(?:don'?t|do\s+not)\s+know\s+what\s+to\s+do|not\s+sure\s+what\s+to\s+do)\b");

    // Help with feelings is not puzzle help. A real object/action question or
    // explicit hint request still wins in a message that also mentions feelings.
    private const string SocialTopics =
        @"(?:รู้สึก|ความรู้สึก|คิดถึง|กลัว|เหงา|เหนื่อย|เสียใจ|สงบใจ|ใจเย็น|หายใจ|ปลอบ|กำลังใจ|คุยเป็นเพื่อน|ความทรงจำ|บ้าน|ครอบครัว|" +
        @"\b(?:feel(?:ing)?s?|emotions?|moods?|calm|breathe|breathing|comfort|company|talk|heart|cope|coping|remember|memories|miss|fear|afraid|lonely|loneliness|homesick(?:ness)?|home|family)\b)";
    private static readonly Regex SocialTopic = R(SocialTopics);
    private static readonly Regex Clauses = R(@"[\r\n.!?;,:]+|\b(?:and|but|then|instead|actually)\b|แต่(?:ว่า)?|แล้ว(?:ก็)?|และ");
    private const string ThaiRefusal = @"(?:ไม่(?:ต้อง|อยาก(?:ได้)?|เอา|ขอ|ต้องการ)|อย่า(?:เพิ่ง)?|งด)";
    private const string EnglishRefusal = @"\b(?:do\s+not|don't|dont|not)\s+(?:(?:need|want)\s+(?:(?:you|anyone)\s+to\s+)?)?";
    private static readonly Regex HintDecline = R(
        ThaiRefusal + @"(?:\s*(?:ให้|ช่วย|มา|บอก|เธอ|คุณ)){0,4}\s*(?:คำ)?ใบ้|" +
        @"\b(?:no(?:\s+more)?|without)\s+hints?\b|\bno\s+need\s+(?:for\s+)?(?:a\s+)?hints?\b|" +
        EnglishRefusal + @"(?:(?:give|offer|provide|tell|show)(?:\s+(?:me|us))?\s+)?(?:(?:a|any|the)\s+)?hints?\b");
    private static readonly Regex ExplicitHintRequest = R(
        @"(?:ขอ|ช่วย|บอก|ให้|อยากได้|ต้องการ|มี).{0,12}(?:คำ)?ใบ้|^\s*(?:คำ)?ใบ้.{0,16}$|" +
        @"\b(?:give|offer|provide|tell|show|need|want|use|have|get)(?:\s+(?:me|us))?\s+(?:(?:a|the|some|any)\s+)?hints?\b|" +
        @"\b(?:can|could|would)\s+you\s+hint\b|\bhint\s+(?:me|us)\b|^\s*(?:any\s+|a\s+)?hints?(?:\s+please)?\s*$");
    private static readonly Regex RequestDecline = R(
        ThaiRefusal + @"(?:\s*(?:มา|ให้|เธอ|คุณ)){0,3}\s*(?:บอก|ช่วย|ให้)|" +
        EnglishRefusal + @"(?:give|tell|show|help)\b");

    // Bind a question to its object, not to an unrelated noun elsewhere in the
    // sentence (e.g. "Where is your home outside this room?").
    private static readonly string[] GameplaySubjects =
    {
        "ประตู", "ลิ้นชัก", "กุญแจ", "แม่กุญแจ", "ปริศนา", "รหัส", "ด่าน", "ห้องนี้", "ทางออก",
        "door", "drawer", "key", "keys", "lock", "puzzle", "code", "level", "this room", "exit",
    };
    private const string ThaiAction = @"(?:เปิด|ปลดล็อ[คก]?|ไข|ใช้|แก้|ผ่าน|หา|ค้น|สำรวจ|ตรวจ|อ่าน|หมุน|หยิบ|เริ่ม)";
    private const string QuestionGap = @"(?:(?!" + SocialTopics + @").)";
    private const string ThaiMethod = @"(?:ยังไง|อย่างไร|ได้ไง|ด้วยอะไร|กับอะไร|ที่ไหน|ตรงไหน|วิธี)";
    private const string EnglishAction = @"\b(?:open(?:s|ed|ing)?|unlock(?:s|ed|ing)?|use|using|solve|solving|inspect|search|find|read|turn|take|pick|escape|leave|pass|go)\b";
    private static readonly Regex BareHelpQuestion = R(
        @"^\s*(?:ทำยังไง|ทำไง|ทำอย่างไร|ต้องทำอะไร|หาอะไร|what\s+to\s+do(?:\s+next)?|" +
        @"what\s+(?:do|should)\s+(?:i|we)\s+do(?:\s+next)?|where\s+should\s+(?:i|we)\s+go|how\s+to\s+proceed)" +
        @"\s*(?:ครับ|ค่ะ|คะ|นะ|ดี)?[?.!]*\s*$");
    private static readonly Regex EnglishSubject = new Regex(@"^[a-z ]+$", RegexOptions.CultureInvariant);
    private static readonly Regex DefaultGameplayQuestion = R(BuildGameplayQuestion(null));
    private static readonly Regex ExplorationQuestion = R(
        @"(?:เริ่ม|สำรวจ|ค้นหา)" + QuestionGap + @"{0,24}(?:ตรงไหน|ที่ไหน|อย่างไร|ยังไง)|" +
        @"(?:ควร|ต้อง).{0,12}(?:เริ่มสำรวจ|สำรวจ)|\b(?:where|how)\s+(?:do|should|can)\s+(?:i|we)\s+(?:start|explore)\b");

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
        ToneReading tone = PlayerToneClassifier.Read(message);
        if (tone.Tone == PlayerTone.Hostile) intent |= PlayerIntent.Hostile;
        if (tone.Tone == PlayerTone.Cold) intent |= PlayerIntent.Cold;
        if (ContainsAny(text, ApologyWords)) intent |= PlayerIntent.Apology;
        if (ContainsAny(text, GreetingWords)) intent |= PlayerIntent.Greeting;
        if (IsAskingForHint(message)) intent |= PlayerIntent.AskingHint;
        if (ContainsAny(text, ThanksWords)) intent |= PlayerIntent.Thanks;
        if (ContainsAny(text, FeelingWords)) intent |= PlayerIntent.Feelings;
        if (ContainsAny(text, ComfortWords)) intent |= PlayerIntent.Comfort;
        return intent;
    }

    public static bool IsHostile(string message)
    {
        return (Classify(message) & PlayerIntent.Hostile) != 0;
    }

    /// <summary>Hostile or cold: the message pushes the NPC away.</summary>
    public static bool IsNegative(PlayerIntent intent)
    {
        return (intent & (PlayerIntent.Hostile | PlayerIntent.Cold)) != 0;
    }

    public static bool IsAskingForHint(string message)
    {
        return IsAskingForHint(message, null);
    }

    /// <summary>Room-authored nouns extend detection without hardcoding each room here.</summary>
    public static bool IsAskingForHint(string message, IEnumerable<string> gameplaySubjects)
    {
        return ReadHintRequest(NormalizeHintText(message), gameplaySubjects);
    }

    private const int GameplayQuestionCacheLimit = 32;
    private static readonly Dictionary<string, Regex> GameplayQuestionCache = new Dictionary<string, Regex>(StringComparer.Ordinal);
    private static readonly Queue<string> GameplayQuestionCacheOrder = new Queue<string>();
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetQuestionCache()
    {
        GameplayQuestionCache.Clear(); GameplayQuestionCacheOrder.Clear();
    }

    private static Regex GetGameplayQuestion(IEnumerable<string> roomSubjects)
    {
        if (roomSubjects == null) return DefaultGameplayQuestion;
        var subjects = new List<string>(); var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string subject in roomSubjects)
        {
            string term = NormalizeHintText(subject).Trim();
            if (term.Length > 0 && seen.Add(term)) subjects.Add(term);
        }
        if (subjects.Count == 0) return DefaultGameplayQuestion;
        subjects.Sort(StringComparer.Ordinal);
        var key = new StringBuilder();
        foreach (string subject in subjects) key.Append(subject.Length).Append(':').Append(subject);
        string cacheKey = key.ToString();
        Regex question;
        if (GameplayQuestionCache.TryGetValue(cacheKey, out question)) return question;
        question = R(BuildGameplayQuestion(subjects));
        if (GameplayQuestionCache.Count >= GameplayQuestionCacheLimit)
            GameplayQuestionCache.Remove(GameplayQuestionCacheOrder.Dequeue());
        GameplayQuestionCache.Add(cacheKey, question); GameplayQuestionCacheOrder.Enqueue(cacheKey);
        return question;
    }

    private static bool ReadHintRequest(string message, IEnumerable<string> roomSubjects)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        Regex question = GetGameplayQuestion(roomSubjects);
        bool social = SocialTopic.IsMatch(message);
        bool asking = false, declined = false;
        foreach (string clause in Clauses.Split(message))
        {
            MatchCollection declines = HintDecline.Matches(clause);
            int lastDecline = declines.Count > 0 ? declines[declines.Count - 1].Index : -1;
            int lastRequest = -1;
            foreach (Match request in ExplicitHintRequest.Matches(clause))
            {
                bool negated = false;
                foreach (Match deny in declines)
                    if (request.Index < deny.Index + deny.Length && deny.Index < request.Index + request.Length)
                    { negated = true; break; }
                if (!negated) lastRequest = request.Index;
            }
            // A later explicit request can retract a refusal. An indirect
            // question cannot silently undo "no hints", even in a later clause.
            if (lastDecline > lastRequest)
            { declined = true; asking = false; continue; }
            if (lastRequest >= 0)
            { declined = false; asking = true; continue; }
            if (declined || RequestDecline.IsMatch(clause)) continue;
            if (question.IsMatch(clause) ||
                (ExplorationQuestion.IsMatch(clause) && !SocialTopic.IsMatch(clause)))
            { asking = true; continue; }
            // Bare/general help may refer to another clause's loneliness or
            // anxiety. Specific puzzle requests above are not suppressed.
            if (!social &&
                (GenericHelpRequest.IsMatch(clause) || BareHelpQuestion.IsMatch(clause) || StuckRequest.IsMatch(clause)))
                asking = true;
        }
        return asking;
    }

    private static string BuildGameplayQuestion(IEnumerable<string> roomSubjects)
    {
        var nouns = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddSubjects(GameplaySubjects, nouns, seen);
        AddSubjects(roomSubjects, nouns, seen);
        string subject = "(?:" + string.Join("|", nouns) + ")";
        return ThaiAction + QuestionGap + "{0,24}" + subject + QuestionGap + "{0,24}" + ThaiMethod + "|" +
            subject + QuestionGap + "{0,16}" + ThaiAction + QuestionGap + "{0,24}" + ThaiMethod + "|" +
            @"(?:วิธี|ทำยังไง|ทำอย่างไร)" + QuestionGap + "{0,12}" + ThaiAction + QuestionGap + "{0,24}" + subject + "|" +
            subject + QuestionGap + @"{0,16}(?:อยู่|ซ่อน|เก็บ|หา)" + QuestionGap + @"{0,20}(?:ที่ไหน|ตรงไหน|ไหน)|" +
            @"\b(?:(?:how|what|which)\b|(?:can|could|would)\s+(?:you|i|we)\b)" + QuestionGap + "{0,48}" + EnglishAction + QuestionGap + "{0,24}" + subject + "|" +
            @"\bwhere\b" + QuestionGap + @"{0,24}\b(?:is|are|find|look|search)\b" + QuestionGap + "{0,24}" + subject + "|" +
            @"\b(?:tell|show|give)\s+(?:me|us)\b" + QuestionGap + "{0,32}" + subject;
    }

    private static void AddSubjects(IEnumerable<string> subjects, List<string> nouns, HashSet<string> seen)
    {
        if (subjects == null) return;
        foreach (string subject in subjects)
        {
            string term = NormalizeHintText(subject).Trim();
            if (term.Length == 0 || !seen.Add(term)) continue;
            // "key" in "monkey" must not turn a personal question into a hint.
            string pattern = Regex.Escape(term);
            if (EnglishSubject.IsMatch(term)) pattern = @"(?<![a-z])" + pattern + @"(?![a-z])";
            nouns.Add(pattern);
        }
    }

    private static string NormalizeHintText(string message)
    {
        string source = message ?? string.Empty;
        try { source = source.Normalize(NormalizationForm.FormKC); }
        catch (ArgumentException) { /* Keep malformed user text safe to classify. */ }
        var text = new StringBuilder(source.Length);
        foreach (char c in source)
            if (char.GetUnicodeCategory(c) != UnicodeCategory.Format)
                text.Append(c == '\u2019' || c == '\u2018' ? '\'' : c);
        return text.ToString().ToLowerInvariant();
    }

    private static Regex R(string pattern)
    {
        // FormKC also decomposes Thai sara am (ำ). Normalize both authored
        // patterns and input, without changing regex escapes or other intents.
        try { pattern = pattern.Normalize(NormalizationForm.FormKC); }
        catch (ArgumentException) { /* A malformed room alias may stay literal. */ }
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
