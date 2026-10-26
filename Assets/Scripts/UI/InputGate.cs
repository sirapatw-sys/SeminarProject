/// <summary>
/// One place that answers "may the player walk and press E right now?".
/// Every overlay that should freeze the room is listed here, so a new one
/// (like the title menu) does not have to be added to each script by hand.
/// </summary>
public static class InputGate
{
    public static bool BlocksCompanionSimulation => !IsGameplayActive || DialogueManager.IsDialogueOpen ||
        AiSettingsPanel.IsOpen || KeypadLockUI.IsOpen || JournalUI.IsOpen || ItemPopupUI.HoldsInput;
    /// <summary>Room simulation is active; a conversation/settings modal may still hold input.</summary>
    public static bool IsGameplayActive
    {
        get { return !IntroSequence.IsPlaying && !TitleMenu.IsOpen && !RoomTransitionManager.IsBusy; }
    }

    public static bool IsBlocked
    {
        get
        {
            return IntroSequence.IsPlaying ||
                   TitleMenu.IsOpen ||
                   DialogueManager.IsDialogueOpen ||
                   MysteryGame.Companions.CompanionChatPanel.IsOpen ||
                   AiSettingsPanel.IsOpen ||
                   KeypadLockUI.IsOpen ||
                   JournalUI.IsOpen ||
                   ItemPopupUI.HoldsInput ||
                   RoomTransitionManager.IsBusy;
        }
    }
}
