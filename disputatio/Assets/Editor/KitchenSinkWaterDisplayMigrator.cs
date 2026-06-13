using Godlotto.Interaction;
using Fungus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kitchen Sink_Pannel 물 표시 레이아웃을 배경 Image와 FX 오버레이 Canvas로 분리합니다.
/// </summary>
public static class KitchenSinkWaterDisplayMigrator
{
    const string KitchenScenePath =
        "Assets/Scenes/Mokotan/First Floor/1foorLeft/Kitchen.unity";

    [MenuItem("Tools/Godlotto/Migrate/Kitchen Sink Water Display Layout")]
    public static void MigrateKitchenSinkWaterDisplayLayout()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        if (!ApplyLayout(scene))
        {
            Debug.LogWarning("[KitchenSinkWaterDisplayMigrator] No changes applied.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[KitchenSinkWaterDisplayMigrator] Sink water display layout migrated.");
    }

    [MenuItem("Tools/Godlotto/Migrate/Kitchen Sink Water Display Fungus Cleanup")]
    public static void MigrateKitchenSinkWaterDisplayFungusCleanup()
    {
        var scene = EditorSceneManager.OpenScene(KitchenScenePath, OpenSceneMode.Single);
        Flowchart flowchart = FindFlowchart(scene);
        if (flowchart == null)
        {
            Debug.LogError("[KitchenSinkWaterDisplayMigrator] Flowchart not found.");
            return;
        }

        if (!DisableDirectSinkVisualSetActiveCommands(flowchart))
        {
            Debug.Log("[KitchenSinkWaterDisplayMigrator] No conflicting Fungus commands found.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[KitchenSinkWaterDisplayMigrator] Disabled direct Fungus SetActive on sink water visuals.");
    }

    public static bool ApplyLayout(UnityEngine.SceneManagement.Scene scene)
    {
        GameObject sinkPanel = FindInScene(scene, KitchenSinkWaterDisplayPolicy.SinkPanelName);
        if (sinkPanel == null)
        {
            Debug.LogError("[KitchenSinkWaterDisplayMigrator] Sink_Pannel not found.");
            return false;
        }

        bool changed = false;
        changed |= EnsureBackgroundChild(sinkPanel);
        changed |= EnsureWaterOverlay(sinkPanel);
        changed |= WireDisplayComponent(sinkPanel);
        changed |= WireInteractionController(scene, sinkPanel);

        Flowchart flowchart = FindFlowchart(scene);
        if (flowchart != null)
            changed |= DisableDirectSinkVisualSetActiveCommands(flowchart);

        return changed;
    }

    static Flowchart FindFlowchart(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Flowchart flowchart = root.GetComponentInChildren<Flowchart>(true);
            if (flowchart != null && flowchart.GetComponents<Block>().Length > 0)
                return flowchart;
        }

        return null;
    }

    static bool DisableDirectSinkVisualSetActiveCommands(Flowchart flowchart)
    {
        if (flowchart == null)
            return false;

        var blockedBlocks = new System.Collections.Generic.HashSet<string>(
            KitchenSinkWaterDisplayPolicy.SinkWaterDisplayFungusBlockNames,
            System.StringComparer.Ordinal);
        var blockedObjectNames = new System.Collections.Generic.HashSet<string>(
            KitchenSinkWaterDisplayPolicy.SinkWaterDisplayObjectNames,
            System.StringComparer.Ordinal);

        bool changed = false;
        foreach (Block block in flowchart.GetComponents<Block>())
        {
            if (block == null || !blockedBlocks.Contains(block.BlockName))
                continue;

            foreach (Command command in block.CommandList)
            {
                if (command == null || !command.enabled || command.GetType().Name != "SetActive")
                    continue;

                if (!TryGetSetActiveTarget(command, out GameObject target, out _))
                    continue;

                if (target == null || !blockedObjectNames.Contains(target.name))
                    continue;

                command.enabled = false;
                EditorUtility.SetDirty(command);
                changed = true;
            }

            if (block.BlockName != KitchenSinkInteractionGate.FaucetBlockName)
                continue;

            foreach (Command command in block.CommandList)
            {
                if (command == null || !command.enabled || command.GetType().Name != "Wait")
                    continue;

                var waitSo = new SerializedObject(command);
                if (waitSo.FindProperty("_duration.floatVal").floatValue != 3f)
                    continue;

                command.enabled = false;
                EditorUtility.SetDirty(command);
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(flowchart);

        return changed;
    }

    static bool TryGetSetActiveTarget(Command command, out GameObject target, out bool active)
    {
        target = null;
        active = false;

        if (command == null)
            return false;

        var so = new SerializedObject(command);
        target = so.FindProperty("_targetGameObject.gameObjectVal").objectReferenceValue as GameObject;
        active = so.FindProperty("activeState.booleanVal").boolValue;
        return target != null;
    }

    static bool EnsureBackgroundChild(GameObject sinkPanel)
    {
        bool changed = false;
        Transform background = sinkPanel.transform.Find(KitchenSinkWaterDisplayPolicy.BackgroundChildName);
        Image panelImage = sinkPanel.GetComponent<Image>();

        if (background == null)
        {
            var backgroundGo = new GameObject(
                KitchenSinkWaterDisplayPolicy.BackgroundChildName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            backgroundGo.transform.SetParent(sinkPanel.transform, false);
            background = backgroundGo.transform;

            RectTransform backgroundRect = backgroundGo.GetComponent<RectTransform>();
            StretchFullPanel(backgroundRect);

            if (panelImage != null)
            {
                Image backgroundImage = backgroundGo.GetComponent<Image>();
                CopyImageSettings(panelImage, backgroundImage);
                Object.DestroyImmediate(panelImage);
                changed = true;
            }
        }

        background.SetAsFirstSibling();
        return changed;
    }

    static bool EnsureWaterOverlay(GameObject sinkPanel)
    {
        bool changed = false;
        Transform overlay = sinkPanel.transform.Find(KitchenSinkWaterDisplayPolicy.OverlayChildName);
        if (overlay == null)
        {
            var overlayGo = new GameObject(
                KitchenSinkWaterDisplayPolicy.OverlayChildName,
                typeof(RectTransform),
                typeof(Canvas));
            overlayGo.transform.SetParent(sinkPanel.transform, false);
            overlay = overlayGo.transform;
            StretchFullPanel(overlayGo.GetComponent<RectTransform>());
            changed = true;
        }

        Canvas overlayCanvas = overlay.GetComponent<Canvas>();
        Canvas rootPanelCanvas = sinkPanel.GetComponentInParent<Canvas>();
        int sortingLayerId = rootPanelCanvas != null
            ? rootPanelCanvas.sortingLayerID
            : SortingLayer.NameToID("Default");

        if (overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            || overlayCanvas.overrideSorting != true
            || overlayCanvas.sortingLayerID != sortingLayerId
            || overlayCanvas.sortingOrder != KitchenSinkWaterDisplayPolicy.OverlaySortingOrder)
        {
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingLayerID = sortingLayerId;
            overlayCanvas.sortingOrder = KitchenSinkWaterDisplayPolicy.OverlaySortingOrder;
            changed = true;
        }

        changed |= ApplyWaterRendererSorting(FindInChildren(sinkPanel, KitchenSinkWaterDisplayPolicy.WaterRootName), sortingLayerId);

        changed |= ReparentIfNeeded(FindInChildren(sinkPanel, KitchenSinkWaterDisplayPolicy.WaterRootName), overlay);
        changed |= ReparentIfNeeded(FindInChildren(sinkPanel, KitchenSinkWaterDisplayPolicy.FaucetOpenName), overlay);
        changed |= NormalizeFaucetOpenCanvas(FindInChildren(sinkPanel, KitchenSinkWaterDisplayPolicy.FaucetOpenName));

        Transform backspace = sinkPanel.transform.Find("BackspaceCornerFold");
        if (backspace != null)
            overlay.SetSiblingIndex(backspace.GetSiblingIndex());
        else
            overlay.SetAsLastSibling();

        return changed;
    }

    static bool ApplyWaterRendererSorting(GameObject waterRoot, int sortingLayerId)
    {
        if (waterRoot == null)
            return false;

        bool changed = false;
        foreach (LineRenderer lineRenderer in waterRoot.GetComponentsInChildren<LineRenderer>(true))
        {
            if (lineRenderer.sortingLayerID != sortingLayerId
                || lineRenderer.sortingOrder != KitchenSinkWaterDisplayPolicy.OverlaySortingOrder)
            {
                lineRenderer.sortingLayerID = sortingLayerId;
                lineRenderer.sortingOrder = KitchenSinkWaterDisplayPolicy.OverlaySortingOrder;
                changed = true;
            }
        }

        foreach (ParticleSystemRenderer particleRenderer in waterRoot.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (particleRenderer.sortingLayerID != sortingLayerId
                || particleRenderer.sortingOrder != KitchenSinkWaterDisplayPolicy.OverlaySortingOrder)
            {
                particleRenderer.sortingLayerID = sortingLayerId;
                particleRenderer.sortingOrder = KitchenSinkWaterDisplayPolicy.OverlaySortingOrder;
                changed = true;
            }
        }

        return changed;
    }

    static bool NormalizeFaucetOpenCanvas(GameObject faucetOpen)
    {
        if (faucetOpen == null)
            return false;

        bool changed = false;
        Canvas nestedCanvas = faucetOpen.GetComponent<Canvas>();
        if (nestedCanvas != null)
        {
            Object.DestroyImmediate(nestedCanvas);
            changed = true;
        }

        var raycaster = faucetOpen.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
        {
            Object.DestroyImmediate(raycaster);
            changed = true;
        }

        return changed;
    }

    static bool WireDisplayComponent(GameObject sinkPanel)
    {
        var display = sinkPanel.GetComponent<KitchenSinkWaterDisplay>();
        if (display == null)
        {
            display = sinkPanel.AddComponent<KitchenSinkWaterDisplay>();
        }

        var so = new SerializedObject(display);
        so.FindProperty("sinkBackground").objectReferenceValue =
            sinkPanel.transform.Find(KitchenSinkWaterDisplayPolicy.BackgroundChildName)?.gameObject;
        so.FindProperty("waterOverlayCanvas").objectReferenceValue =
            sinkPanel.transform.Find(KitchenSinkWaterDisplayPolicy.OverlayChildName)?.GetComponent<Canvas>();
        so.FindProperty("faucetClosed").objectReferenceValue =
            FindInChildren(sinkPanel, KitchenSinkWaterDisplayPolicy.FaucetClosedName);
        so.FindProperty("faucetOpen").objectReferenceValue =
            FindInChildren(sinkPanel, KitchenSinkWaterDisplayPolicy.FaucetOpenName);
        so.FindProperty("waterRoot").objectReferenceValue =
            FindInChildren(sinkPanel, KitchenSinkWaterDisplayPolicy.WaterRootName);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(display);
        return true;
    }

    static bool WireInteractionController(UnityEngine.SceneManagement.Scene scene, GameObject sinkPanel)
    {
        KitchenInteractionController controller = Object.FindFirstObjectByType<KitchenInteractionController>();
        if (controller == null)
            return false;

        var display = sinkPanel.GetComponent<KitchenSinkWaterDisplay>();
        if (display == null)
            return false;

        var so = new SerializedObject(controller);
        if (so.FindProperty("sinkWaterDisplay").objectReferenceValue == display)
            return false;

        so.FindProperty("sinkWaterDisplay").objectReferenceValue = display;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        return true;
    }

    static bool ReparentIfNeeded(GameObject child, Transform overlay)
    {
        if (child == null || child.transform.parent == overlay)
            return false;

        child.transform.SetParent(overlay, true);
        return true;
    }

    static void StretchFullPanel(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
    }

    static void CopyImageSettings(Image source, Image target)
    {
        target.sprite = source.sprite;
        target.color = source.color;
        target.material = source.material;
        target.raycastTarget = source.raycastTarget;
        target.maskable = source.maskable;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.useSpriteMesh = source.useSpriteMesh;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }

    static GameObject FindInScene(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                if (transform.name == name)
                    return transform.gameObject;
            }
        }

        return null;
    }

    static GameObject FindInChildren(GameObject parent, string name)
    {
        Transform[] transforms = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform.name == name)
                return transform.gameObject;
        }

        return null;
    }
}
