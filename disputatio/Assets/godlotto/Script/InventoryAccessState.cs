using UnityEngine;

public static class InventoryAccessState
{
    private const string UnlockedPrefsKey = "InventoryAccess.UnlockedAfterHallPlayableRetry";
    private const string InventoryGuideOpenedPrefsKey = "InventoryGuide.InventoryOpened";
    private const string CorrectedHallPlayableSceneName = "Hall_playable";

    public static bool IsUnlocked => PlayerPrefs.GetInt(UnlockedPrefsKey, 0) == 1;

    public static void Unlock()
    {
        bool wasUnlocked = IsUnlocked;
        PlayerPrefs.SetInt(UnlockedPrefsKey, 1);
        if (!wasUnlocked)
            PlayerPrefs.SetInt(InventoryGuideOpenedPrefsKey, 0);

        PlayerPrefs.Save();
    }

    public static bool TryUnlockAfterRetry(string activeSceneName, bool playerDied)
    {
        if (!ShouldUnlockAfterRetry(activeSceneName, playerDied))
            return false;

        Unlock();
        return true;
    }

    public static bool TryUnlockAfterRetry(string activeSceneName, string retrySceneName, bool playerDied)
    {
        if (!ShouldUnlockAfterRetry(activeSceneName, retrySceneName, playerDied))
            return false;

        Unlock();
        return true;
    }

    public static bool ShouldUnlockAfterRetry(string activeSceneName, bool playerDied)
    {
        return ShouldUnlockAfterRetry(activeSceneName, string.Empty, playerDied);
    }

    public static bool ShouldUnlockAfterRetry(string activeSceneName, string retrySceneName, bool playerDied)
    {
        return playerDied && (IsHallPlayableScene(activeSceneName) || IsHallPlayableScene(retrySceneName));
    }

    public static bool ShouldAllowInventoryInput(bool isUnlocked)
    {
        return isUnlocked;
    }

    private static bool IsHallPlayableScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        string normalized = sceneName.Trim();
        return string.Equals(normalized, SceneNames.HallPlayable, System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, CorrectedHallPlayableSceneName, System.StringComparison.OrdinalIgnoreCase);
    }
}
