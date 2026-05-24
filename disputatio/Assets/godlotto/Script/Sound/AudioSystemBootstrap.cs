using UnityEngine;

public static class AudioSystemBootstrap
{
    private const string AudioControllerPrefabResourcePath = "Audio/BGM Player";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureAudioControllerExists()
    {
        if (Object.FindFirstObjectByType<AudioController>() != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(AudioControllerPrefabResourcePath);
        if (prefab != null)
        {
            Object.Instantiate(prefab).name = prefab.name;
            return;
        }

        GameObject fallback = new GameObject("BGM Player");
        fallback.AddComponent<AudioSource>();
        fallback.AddComponent<AudioController>();
    }
}
