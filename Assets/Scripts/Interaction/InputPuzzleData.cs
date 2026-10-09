using System.Collections.Generic;
using System.Text.RegularExpressions;
using MysteryGame.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Input Puzzle Data")]
public class InputPuzzleData : ScriptableObject
{
    public string puzzleId;
    public string title;
    [TextArea(2, 5)] public string question;
    [TextArea(2, 5)] public string translation;
    public bool numeric;
    public List<string> acceptedAnswers = new List<string>();
    public List<ConditionRule> conditions = new List<ConditionRule>();
    public List<ActionCommand> successActions = new List<ActionCommand>();
    public string solvedFlag;
    [TextArea(2, 4)] public string failureMessage = "คำตอบยังไม่ถูกต้องค่ะ";

    public bool Accepts(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        string value = AnswerCore(answer);
        // Exact matching of the core rejects negation and lists of guesses.
        return acceptedAnswers != null && acceptedAnswers.Exists(candidate =>
            !string.IsNullOrWhiteSpace(candidate) && AnswerCore(candidate) == value);
    }

    // How people wrap an answer: "คำตอบคือ ...", "...เหรอครับ?", "it's ...".
    // Only whole lead-ins and trailing particles go; anything in the middle
    // ("ไม่ใช่", "or") stays and the match fails as before.
    private static readonly Regex AnswerLeadIn = new Regex(
        @"^(?:คำตอบ(?:ของ(?:ข้า|ผม|ฉัน|เรา))?(?:ก็)?(?:คือ|เป็น)?|(?:ข้า|ผม|ฉัน|เรา|หนู)?(?:คิดว่า|ว่า|เดาว่า)|" +
        @"น่าจะ(?:เป็น|คือ)?|อาจจะ(?:เป็น|คือ)?|มัน(?:ก็)?(?:คือ|เป็น)|ก็คือ|คือ|เป็น|ก็|" +
        @"the\s*answer\s*is|answer\s*is|its|it\s*is|is\s*it|maybe|i\s*think(?:\s*its)?|i\s*guess)\s*",
        RegexOptions.CultureInvariant);
    private static readonly Regex AnswerTail = new Regex(
        @"\s*(?:เหรอ|หรอ|หรือเปล่า|หรือ|รึเปล่า|รึป่าว|รึ|ป่ะ|ปะ|ใช่(?:ไหม|มั้ย|มั๊ย|ป่ะ|หรือเปล่า)?|ไหม|มั้ย|มั๊ย|" +
        @"ครับ|คับ|ค่ะ|คะ|ขอรับ|เจ้าค่ะ|จ้ะ|จ้า|นะ|น่ะ|ล่ะ|สินะ|แน่ๆ|แหละ|มั้ง|ไง|ฤๅ|ฤา|เพคะ|" +
        @"right|maybe|perhaps|i\s*think|i\s*guess)$",
        RegexOptions.CultureInvariant);

    /// <summary>The answer without punctuation, polite lead-ins or question particles.</summary>
    public static string AnswerCore(string value)
    {
        string text = Regex.Replace((value ?? string.Empty).ToLowerInvariant().Replace("'", string.Empty).Replace("’", string.Empty),
                                    @"[\p{P}\p{S}]+", " ").Trim();
        for (int pass = 0; pass < 6; pass++)
        {
            string trimmed = AnswerTail.Replace(AnswerLeadIn.Replace(text, string.Empty), string.Empty).Trim();
            if (trimmed == text || trimmed.Length == 0) break;
            text = trimmed;
        }
        return Normalize(text);
    }
    public bool TrySolve(GameState state, string answer)
    {
        if (state == null || string.IsNullOrWhiteSpace(solvedFlag) ||
            state.HasFlag(solvedFlag) || !ConditionRule.AllHold(conditions, state) || !Accepts(answer))
            return false;
        foreach (ActionCommand action in successActions) action?.Execute(state);
        state.SetFlag(solvedFlag);
        return true;
    }
    public static string Normalize(string value)
    {
        return Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", string.Empty);
    }
}
