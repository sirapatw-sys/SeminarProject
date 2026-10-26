using System;
using System.Collections;

public interface IAiDialogueProvider
{
    bool CanGenerate { get; }
    string LastError { get; }
    IEnumerator Generate(MiniEventData data, Action<GeneratedDialogueContent> complete);
    IEnumerator GenerateReply(string npcId, string speakerName, string context, string message,
        Action<GeneratedChatReply> complete, string personality = null);
}

public static class DialogueProviders
{
    // A view can install a mock/offline/alternative implementation without changing GameState.
    public static IAiDialogueProvider Override { get; set; }
    public static IAiDialogueProvider Current { get { return Override ?? AiDialogueGenerator.Instance; } }
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Override = null; }
}
