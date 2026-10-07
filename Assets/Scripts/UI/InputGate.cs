/// <summary>
/// One place that answers "may the player walk and press E right now?".
/// Every overlay that should freeze the room is listed here, so a new one
/// (like the title menu) does not have to be added to each script by hand.
/// </summary>
public static class InputGate
{
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
                   AiSettingsPanel.IsOpen ||
                   KeypadLockUI.IsOpen ||
                   JournalUI.IsOpen ||
                   RoomTransitionManager.IsBusy;
        }
    }
}
