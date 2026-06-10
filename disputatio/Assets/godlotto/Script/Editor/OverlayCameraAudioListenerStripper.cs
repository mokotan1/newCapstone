using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// URP Overlay 카메라에 붙은 AudioListener를 씬·프리팹에서 일괄 제거합니다.
/// 메인 카메라만 AudioListener를 유지해 "2 Audio Listeners" 경고를 방지합니다.
/// </summary>
public static class OverlayCameraAudioListenerStripper
{
    private const string MenuPath = "Tools/Godlotto/Remove Overlay Camera AudioListeners (All Scenes)";

    [MenuItem(MenuPath)]
    public static void RemoveFromAllScenesAndPrefabs()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("[OverlayCameraAudioListenerStripper] Play 모드에서는 실행할 수 없습니다. Play를 중지한 뒤 다시 시도하세요.");
            return;
        }

        string activeScenePath = SceneManager.GetActiveScene().path;
        int removedFromScenes = 0;
        int changedScenes = 0;
        int removedFromPrefabs = 0;
        int changedPrefabs = 0;

        try
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(scenePath))
                    continue;

                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int removedInScene = RemoveOverlayAudioListenersInOpenScene(scene);
                if (removedInScene > 0)
                {
                    removedFromScenes += removedInScene;
                    changedScenes++;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(prefabPath))
                    continue;

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                int removedInPrefab = RemoveOverlayAudioListenersInHierarchy(prefabRoot);
                if (removedInPrefab > 0)
                {
                    removedFromPrefabs += removedInPrefab;
                    changedPrefabs++;
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(activeScenePath))
                EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(
            $"[OverlayCameraAudioListenerStripper] 완료 — 씬 {changedScenes}개에서 {removedFromScenes}개, " +
            $"프리팹 {changedPrefabs}개에서 {removedFromPrefabs}개 AudioListener 제거.");
    }

    private static int RemoveOverlayAudioListenersInOpenScene(Scene scene)
    {
        int removed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            removed += RemoveOverlayAudioListenersInHierarchy(root);

        return removed;
    }

    private static int RemoveOverlayAudioListenersInHierarchy(GameObject root)
    {
        int removed = 0;
        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (!IsOverlayCamera(camera))
                continue;

            AudioListener listener = camera.GetComponent<AudioListener>();
            if (listener == null)
                continue;

            Object.DestroyImmediate(listener, true);
            removed++;
            EditorUtility.SetDirty(camera.gameObject);
        }

        return removed;
    }

    private static bool IsOverlayCamera(Camera camera)
    {
        if (camera == null)
            return false;

        UniversalAdditionalCameraData additionalData = camera.GetUniversalAdditionalCameraData();
        return additionalData != null && additionalData.renderType == CameraRenderType.Overlay;
    }
}
