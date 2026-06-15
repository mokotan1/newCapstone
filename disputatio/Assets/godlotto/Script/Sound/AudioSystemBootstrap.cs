using UnityEngine;

public static class AudioSystemBootstrap
{
    private const string BgmControllerPrefabResourcePath = "Audio/BGM Player";
    private const string SfxControllerPrefabResourcePath = "Audio/SFX Player";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureAudioPlayersExist()
    {
        EnsureBgmControllerExists();
        EnsureSfxControllerExists();
    }

    private static void EnsureBgmControllerExists()
    {
        if (Object.FindFirstObjectByType<AudioController>() != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(BgmControllerPrefabResourcePath);
        if (prefab != null)
        {
            Object.Instantiate(prefab).name = prefab.name;
            return;
        }

        GameObject fallback = new GameObject("BGM Player");
        fallback.AddComponent<AudioSource>();
        fallback.AddComponent<AudioController>();
    }

    private static void EnsureSfxControllerExists()
    {
        if (Object.FindFirstObjectByType<SfxController>() != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(SfxControllerPrefabResourcePath);
        if (prefab != null)
        {
            Object.Instantiate(prefab).name = prefab.name;
            return;
        }

        GameObject fallback = new GameObject("SFX Player");
        fallback.AddComponent<AudioSource>();
        fallback.AddComponent<SfxController>();
    }
}
