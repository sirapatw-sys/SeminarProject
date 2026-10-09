using System;

public static class DialogueSignals
{
    public static event Action<string, string> TypedReplyCompleted;
    public static void PublishTypedReply(string npcId, string playerMessage)
    {
        TypedReplyCompleted?.Invoke(npcId, playerMessage);
    }
}
