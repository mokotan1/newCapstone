using UnityEngine;

public static class DeveloperModeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureDeveloperModeController()
    {
        if (!DeveloperModeController.CanUseDeveloperModeRuntime)
            return;

        if (Object.FindFirstObjectByType<DeveloperModeController>(FindObjectsInactive.Include) != null)
            return;

        GameObject go = new GameObject("DeveloperModeController");
        go.AddComponent<DeveloperModeController>();
    }
}
