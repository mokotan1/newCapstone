using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 HUD의 씬 Canvas 부착·중복 제거 규칙. EditMode 테스트에서 Canvas 탐색을 주입할 수 있다.
/// </summary>
public static class QuestTrackerHudHost
{
    public const string FallbackCanvasObjectName = "QuestTrackerCanvas";

    public static bool ShouldAttachHud(string sceneName)
    {
        return !TutorialQuestWorldScenes.ShouldHideTutorialHud(sceneName);
    }

    public static Transform ResolveCanvasParent(Func<Canvas> findCanvas = null)
    {
        Canvas canvas = findCanvas != null ? findCanvas() : FindSceneCanvas();
        if (canvas != null)
            return canvas.transform;

        return CreateOverlayCanvas().transform;
    }

    public static Canvas FindSceneCanvas()
    {
        return UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
    }

    public static GameObject CreateOverlayCanvas()
    {
        var canvasObject = new GameObject(
            FallbackCanvasObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        return canvasObject;
    }

    public static IReadOnlyList<GameObject> FindHudRootsInScene(Scene scene)
    {
        var results = new List<GameObject>();
        if (!scene.IsValid())
            return results;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            QuestTrackerHudView[] views = roots[i].GetComponentsInChildren<QuestTrackerHudView>(true);
            for (int j = 0; j < views.Length; j++)
            {
                if (views[j] != null)
                    results.Add(views[j].gameObject);
            }
        }

        return results;
    }

    public static int DestroyExtraHudRoots(Scene scene, GameObject keepRoot)
    {
        IReadOnlyList<GameObject> hudRoots = FindHudRootsInScene(scene);
        int destroyed = 0;

        for (int i = 0; i < hudRoots.Count; i++)
        {
            GameObject candidate = hudRoots[i];
            if (candidate == null || candidate == keepRoot)
                continue;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(candidate);
            else
                UnityEngine.Object.DestroyImmediate(candidate);

            destroyed++;
        }

        return destroyed;
    }

    public static bool HasHudRootInScene(Scene scene, GameObject hudRoot)
    {
        if (hudRoot == null || !scene.IsValid())
            return false;

        return hudRoot.scene == scene;
    }
}

/// <summary>
/// 퀘스트 트래커 DDOL 시스템과 씬별 HUD 부착을 부트스트랩합니다.
/// AudioSystemBootstrap·SceneBookOverlayRuntime과 동일한 RuntimeInitialize 패턴을 따릅니다.
/// </summary>
public static class QuestTrackerHudBootstrap
{
    const string SystemsObjectName = "TutorialQuestSystems";

    static bool sceneHookRegistered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeAfterSceneLoad()
    {
        EnsureSystems();
        RegisterSceneHook();
        AttachHudToActiveScene();
    }

    public static void EnsureSystems()
    {
        if (QuestTrackerHudController.Instance != null
            && UnityEngine.Object.FindFirstObjectByType<TutorialQuestGameBridge>() != null)
            return;

        if (QuestTrackerHudController.Instance == null)
        {
            var systemsObject = new GameObject(SystemsObjectName);
            UnityEngine.Object.DontDestroyOnLoad(systemsObject);
            systemsObject.AddComponent<QuestTrackerHudController>();
        }

        if (UnityEngine.Object.FindFirstObjectByType<TutorialQuestGameBridge>() == null)
            QuestTrackerHudController.Instance.gameObject.AddComponent<TutorialQuestGameBridge>();
    }

    static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHookRegistered = true;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachHudToActiveScene();
    }

    public static void AttachHudToActiveScene()
    {
        QuestTrackerHudController controller = QuestTrackerHudController.Instance;
        if (controller == null)
            return;

        controller.AttachHudToScene(SceneManager.GetActiveScene());
    }

    internal static void ResetForTests()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        sceneHookRegistered = false;
    }
}
