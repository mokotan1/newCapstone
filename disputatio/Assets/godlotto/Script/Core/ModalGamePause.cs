/// <summary>
/// In-game modal panels (settings, dialogue log) share pause and world-input blocking.
/// </summary>
public static class ModalGamePause
{
    public static bool IsSettingsOpen =>
        InGameSettingsPanel.Instance != null && InGameSettingsPanel.Instance.IsOpen;

    public static bool IsDialogueLogOpen =>
        DialogueLogPanel.Instance != null && DialogueLogPanel.Instance.IsOpen;

    public static bool IsAnyModalOpen => IsSettingsOpen || IsDialogueLogOpen;

    public static float ResolveTimeScaleOnClose(bool settingsOpen, bool dialogueLogOpen) =>
        settingsOpen || dialogueLogOpen ? 0f : 1f;

    public static float ResolveTimeScaleOnClose() =>
        ResolveTimeScaleOnClose(IsSettingsOpen, IsDialogueLogOpen);

    public static bool ShouldEndWorldInputBlocker(bool settingsOpen, bool dialogueLogOpen) =>
        !settingsOpen && !dialogueLogOpen;

    public static bool ShouldEndWorldInputBlocker() =>
        ShouldEndWorldInputBlocker(IsSettingsOpen, IsDialogueLogOpen);
}
