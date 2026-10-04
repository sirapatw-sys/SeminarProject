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
        string value = Normalize(answer);
        // Exact matching rejects negation and lists of guesses.
        return acceptedAnswers != null && acceptedAnswers.Exists(candidate =>
            !string.IsNullOrWhiteSpace(candidate) && Normalize(candidate) == value);
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
