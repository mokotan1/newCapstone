using UnityEngine;
using UnityEngine.SceneManagement;

public static class CheckpointLoadCoordinator
{
    private static CheckpointSaveData pendingApply;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void RegisterSceneLoadedHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static bool HasContinueData()
    {
        return CheckpointRepository.HasCheckpoint();
    }

    public static string GetResumeSceneOrFallback(string fallbackSceneName)
    {
        return CheckpointRepository.TryLoad(out var data) ? data.resumeSceneName : fallbackSceneName;
    }

    public static void LoadLatestOrFallback(string fallbackSceneName)
    {
        Time.timeScale = 1f;

        if (CheckpointRepository.TryLoad(out var data))
        {
            pendingApply = data;
            SceneManager.LoadScene(data.resumeSceneName);
            return;
        }

        pendingApply = null;
        SceneManager.LoadScene(fallbackSceneName);
    }

    public static void ClearContinueData()
    {
        pendingApply = null;
        CheckpointRepository.Clear();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingApply == null)
            return;

        if (!string.Equals(scene.name, pendingApply.resumeSceneName, System.StringComparison.Ordinal))
            return;

        ProgressSnapshotApplier.Apply(pendingApply);
        pendingApply = null;
    }
}
