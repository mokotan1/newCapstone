using UnityEngine;

/// <summary>Player entry: request Supervisor start before the first scene. Does not wait on the main thread.</summary>
public static class LocalAiPlayerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void StartHost()
    {
        LocalAiRuntimeHost.EnsureStarted(Application.isBatchMode);
    }
}
