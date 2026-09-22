/// <summary>
/// Stores the transient scene history used by C# back navigation.
/// This route value deliberately resets with the Unity session and is not a checkpoint value.
/// </summary>
public static class SceneRouteState
{
    private static string previousSceneName;

    public static void RecordDepartedScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        previousSceneName = sceneName;
    }

    public static bool TryGetPreviousScene(out string sceneName)
    {
        sceneName = previousSceneName;
        return !string.IsNullOrWhiteSpace(sceneName);
    }

    internal static void ResetForTests()
    {
        previousSceneName = null;
    }
}
