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
}
