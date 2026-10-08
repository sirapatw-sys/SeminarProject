using System;

[Serializable]
public class GeneratedDialogueContent
{
    public string[] lines;
    public GeneratedDialogueChoice[] choices;

    public bool IsValid(int expectedChoiceCount)
    {
        if (lines == null || lines.Length == 0 || lines.Length > 3 ||
            choices == null || choices.Length != expectedChoiceCount)
        {
            return false;
        }

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.Length > 240)
            {
                return false;
            }
        }

        foreach (GeneratedDialogueChoice choice in choices)
        {
            if (choice == null ||
                string.IsNullOrWhiteSpace(choice.optionText) ||
                string.IsNullOrWhiteSpace(choice.responseText) ||
                choice.optionText.Length > 120 ||
                choice.responseText.Length > 240)
            {
                return false;
            }
        }

        return true;
    }
}

[Serializable]
public class GeneratedDialogueChoice
{
    public string optionText;
    public string responseText;

    // Names models have used on their own when the format was not spelled
    // out ("text"/"response", "player"/"npc"). Read only to fill the two above.
    public string text;
    public string response;
    public string player;
    public string npc;

    /// <summary>Fills optionText/responseText from the other names when they are empty.</summary>
    public void AdoptAlternateNames()
    {
        if (string.IsNullOrWhiteSpace(optionText))
        {
            optionText = !string.IsNullOrWhiteSpace(text) ? text : player;
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            responseText = !string.IsNullOrWhiteSpace(response) ? response : npc;
        }
    }
}
