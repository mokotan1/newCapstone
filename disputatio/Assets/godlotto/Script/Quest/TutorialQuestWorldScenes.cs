using System;

/// <summary>
/// 튜토리얼 퀘스트 단계 완료에 쓰이는 씬 이름·판정 헬퍼.
/// </summary>
public static class TutorialQuestWorldScenes
{
    public const string UtilityRoom = "UtilityRoom";

    public static bool IsKitchenScene(string sceneName)
    {
        return string.Equals(sceneName, SceneNames.Kitchen, StringComparison.Ordinal);
    }

    public static bool IsInspectableLitHallScene(string sceneName, bool electricOn)
    {
        if (!electricOn || string.IsNullOrEmpty(sceneName))
            return false;

        return string.Equals(sceneName, SceneNames.HallPlayable, StringComparison.Ordinal)
            || string.Equals(sceneName, SceneNames.HallRight, StringComparison.Ordinal)
            || string.Equals(sceneName, SceneNames.HallAnimate, StringComparison.Ordinal);
    }

    public static bool ShouldHideTutorialHud(string sceneName)
    {
        return string.Equals(sceneName, SceneNames.MainMenu, StringComparison.Ordinal);
    }
}
