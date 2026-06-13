using System.IO;
using System.Text.RegularExpressions;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenSinkWaterDisplayTests
{
    const string KitchenSinkWaterDisplayScriptGuid = "5ebeae31a93cf4045b106be4e653a678";
    const string WaterPrefabGuid = "b049ab383604a304e99d17a60ee7ab3d";
    const string SetActiveCommandScriptGuid = "dbd8c931f22994b9d90e2037fffaa770";

    [Test]
    public void SinkPanel_BackgroundChild_IsFirstSibling_BeforeWaterDisplayObjects()
    {
        string sceneText = ReadKitchenSceneText();
        string sinkPanelRect = FindRectTransformBlock(sceneText, KitchenSinkWaterDisplayPolicy.SinkPanelName);
        string backgroundRect = FindRectTransformBlock(sceneText, KitchenSinkWaterDisplayPolicy.BackgroundChildName);
        string overlayRect = FindRectTransformBlock(sceneText, KitchenSinkWaterDisplayPolicy.OverlayChildName);

        int backgroundIndex = GetChildSiblingIndex(sinkPanelRect, ExtractFileId(backgroundRect));
        int overlayIndex = GetChildSiblingIndex(sinkPanelRect, ExtractFileId(overlayRect));

        Assert.Less(
            backgroundIndex,
            overlayIndex,
            "SinkBackground must render before the water overlay layer.");
        StringAssert.Contains(
            $"m_TransformParent: {{fileID: {ExtractFileId(overlayRect)}}}",
            FindWaterPrefabInstanceBlock(sceneText),
            "Water prefab instance must live under SinkWaterOverlay.");
    }

    [Test]
    public void SinkPanel_WaterOverlayCanvas_UsesOverrideSortingAbovePanelRoot()
    {
        string sceneText = ReadKitchenSceneText();
        string overlayObject = FindGameObjectBlock(sceneText, KitchenSinkWaterDisplayPolicy.OverlayChildName);
        string canvasBlock = FindComponentBlockOnGameObject(sceneText, overlayObject, "223");

        StringAssert.Contains("m_OverrideSorting: 1", canvasBlock);
        StringAssert.Contains(
            $"m_SortingOrder: {KitchenSinkWaterDisplayPolicy.OverlaySortingOrder}",
            canvasBlock);
    }

    [Test]
    public void SinkPanel_WaterLineRenderer_SortingOrderMatchesOverlayPolicy()
    {
        string sceneText = ReadKitchenSceneText();
        string prefabInstance = FindWaterPrefabInstanceBlock(sceneText);

        StringAssert.IsMatch(
            $@"propertyPath: m_SortingOrder\r?\n\s+value: {KitchenSinkWaterDisplayPolicy.OverlaySortingOrder}",
            prefabInstance);
    }

    [Test]
    public void SinkPanel_HasKitchenSinkWaterDisplayComponent()
    {
        string sceneText = ReadKitchenSceneText();
        FindMonoBehaviourOnGameObject(
            sceneText,
            KitchenSinkWaterDisplayPolicy.SinkPanelName,
            KitchenSinkWaterDisplayScriptGuid);
    }

    [Test]
    public void SinkPanel_WaterOverlayCanvas_SortingLayerMatchesRootPanelCanvas()
    {
        string sceneText = ReadKitchenSceneText();
        string sinkPanelRect = FindRectTransformBlock(sceneText, KitchenSinkWaterDisplayPolicy.SinkPanelName);
        string overlayObject = FindGameObjectBlock(sceneText, KitchenSinkWaterDisplayPolicy.OverlayChildName);
        string overlayCanvasBlock = FindComponentBlockOnGameObject(sceneText, overlayObject, "223");
        string rootCanvasBlock = FindParentCanvasBlock(sceneText, sinkPanelRect);

        Match overlayLayerMatch = Regex.Match(overlayCanvasBlock, @"m_SortingLayerID: (?<id>-?[0-9]+)");
        Match rootLayerMatch = Regex.Match(rootCanvasBlock, @"m_SortingLayerID: (?<id>-?[0-9]+)");

        Assert.IsTrue(overlayLayerMatch.Success, "SinkWaterOverlay canvas sorting layer id missing.");
        Assert.IsTrue(rootLayerMatch.Success, "Sink panel root canvas sorting layer id missing.");
        Assert.AreEqual(
            rootLayerMatch.Groups["id"].Value,
            overlayLayerMatch.Groups["id"].Value,
            "SinkWaterOverlay sortingLayerID must match the root panel canvas.");
    }

    [Test]
    public void FaucetFilledBottleAndBottleDraggedBlocks_DoNotDirectlySetActiveWaterRoot()
    {
        string sceneText = ReadKitchenSceneText();

        foreach (string blockName in KitchenSinkWaterDisplayPolicy.SinkWaterDisplayFungusBlockNames)
        {
            string block = FindBlockByName(sceneText, blockName);
            foreach (Match match in Regex.Matches(
                block,
                @"--- !u!114 &[0-9]+\r?\nMonoBehaviour:[\s\S]*?(?=--- !u!)",
                RegexOptions.Multiline))
            {
                if (!match.Value.Contains($"guid: {SetActiveCommandScriptGuid}"))
                    continue;

                if (!match.Value.Contains($"gameObjectVal: {{fileID: {KitchenSinkWaterDisplayPolicy.WaterRootSceneFileId}}}"))
                    continue;

                StringAssert.Contains("m_Enabled: 0", match.Value,
                    $"Block '{blockName}' must not keep an enabled SetActive on Water root.");
            }
        }
    }

    [Test]
    public void SyncFromFaucetClicked_AppliesSortingLayerToLineRenderer()
    {
        var panel = new GameObject(KitchenSinkWaterDisplayPolicy.SinkPanelName);
        var rootCanvasGo = new GameObject("RootCanvas");
        var rootCanvas = rootCanvasGo.AddComponent<Canvas>();
        rootCanvas.sortingLayerID = SortingLayer.NameToID("UI");

        var background = new GameObject(KitchenSinkWaterDisplayPolicy.BackgroundChildName);
        var overlayGo = new GameObject(KitchenSinkWaterDisplayPolicy.OverlayChildName);
        var overlayCanvas = overlayGo.AddComponent<Canvas>();
        var faucetClosed = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetClosedName);
        var faucetOpen = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetOpenName);
        var water = new GameObject(KitchenSinkWaterDisplayPolicy.WaterRootName);
        var lineRendererGo = new GameObject("Line");
        var lineRenderer = lineRendererGo.AddComponent<LineRenderer>();

        panel.transform.SetParent(rootCanvasGo.transform, false);
        background.transform.SetParent(panel.transform, false);
        overlayGo.transform.SetParent(panel.transform, false);
        faucetClosed.transform.SetParent(panel.transform, false);
        faucetOpen.transform.SetParent(overlayGo.transform, false);
        water.transform.SetParent(overlayGo.transform, false);
        lineRendererGo.transform.SetParent(water.transform, false);

        var display = panel.AddComponent<KitchenSinkWaterDisplay>();
        display.SetReferencesForTests(background, overlayCanvas, faucetClosed, faucetOpen, water);

        display.SyncFromFaucetClicked(true);

        Assert.AreEqual(rootCanvas.sortingLayerID, overlayCanvas.sortingLayerID);
        Assert.AreEqual(rootCanvas.sortingLayerID, lineRenderer.sortingLayerID);
        Assert.AreEqual(KitchenSinkWaterDisplayPolicy.OverlaySortingOrder, lineRenderer.sortingOrder);

        Object.DestroyImmediate(rootCanvasGo);
    }

    [Test]
    public void SyncFromFaucetClicked_TogglesFaucetAndWaterObjects()
    {
        var panel = new GameObject(KitchenSinkWaterDisplayPolicy.SinkPanelName);
        var background = new GameObject(KitchenSinkWaterDisplayPolicy.BackgroundChildName);
        var overlayGo = new GameObject(KitchenSinkWaterDisplayPolicy.OverlayChildName);
        var overlayCanvas = overlayGo.AddComponent<Canvas>();
        var faucetClosed = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetClosedName);
        var faucetOpen = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetOpenName);
        var water = new GameObject(KitchenSinkWaterDisplayPolicy.WaterRootName);

        background.transform.SetParent(panel.transform, false);
        overlayGo.transform.SetParent(panel.transform, false);
        faucetClosed.transform.SetParent(panel.transform, false);
        faucetOpen.transform.SetParent(overlayGo.transform, false);
        water.transform.SetParent(overlayGo.transform, false);

        var display = panel.AddComponent<KitchenSinkWaterDisplay>();
        display.SetReferencesForTests(background, overlayCanvas, faucetClosed, faucetOpen, water);

        display.SyncFromFaucetClicked(true);

        Assert.IsFalse(faucetClosed.activeSelf);
        Assert.IsTrue(faucetOpen.activeSelf);
        Assert.IsTrue(water.activeSelf);

        display.SyncFromFaucetClicked(false);

        Assert.IsTrue(faucetClosed.activeSelf);
        Assert.IsFalse(faucetOpen.activeSelf);
        Assert.IsFalse(water.activeSelf);

        Object.DestroyImmediate(panel);
    }

    [Test]
    public void OnEnable_SyncsLayoutAndRunningWaterFromPuzzleState()
    {
        var puzzleRoot = new GameObject("KitchenPuzzleStateTest");
        var puzzleState = puzzleRoot.AddComponent<KitchenPuzzleState>();
        puzzleState.SetSinkFlagsForTests(
            hasBottle: true,
            bottleClicked: false,
            faucetClicked: true,
            bottleDragged: false);

        var panel = new GameObject(KitchenSinkWaterDisplayPolicy.SinkPanelName);
        panel.SetActive(false);

        var rootCanvasGo = new GameObject("RootCanvas");
        var rootCanvas = rootCanvasGo.AddComponent<Canvas>();
        rootCanvas.sortingLayerID = SortingLayer.NameToID("UI");

        var background = new GameObject(KitchenSinkWaterDisplayPolicy.BackgroundChildName);
        var overlayGo = new GameObject(KitchenSinkWaterDisplayPolicy.OverlayChildName);
        var overlayCanvas = overlayGo.AddComponent<Canvas>();
        var faucetClosed = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetClosedName);
        var faucetOpen = new GameObject(KitchenSinkWaterDisplayPolicy.FaucetOpenName);
        var water = new GameObject(KitchenSinkWaterDisplayPolicy.WaterRootName);

        panel.transform.SetParent(rootCanvasGo.transform, false);
        background.transform.SetParent(panel.transform, false);
        overlayGo.transform.SetParent(panel.transform, false);
        faucetClosed.transform.SetParent(panel.transform, false);
        faucetOpen.transform.SetParent(overlayGo.transform, false);
        water.transform.SetParent(overlayGo.transform, false);

        var display = panel.AddComponent<KitchenSinkWaterDisplay>();
        display.SetReferencesForTests(background, overlayCanvas, faucetClosed, faucetOpen, water);
        display.SetPuzzleStateForTests(puzzleState);
        display.RunEnableSyncForTests();

        Assert.AreEqual(0, background.transform.GetSiblingIndex());
        Assert.AreEqual(rootCanvas.sortingLayerID, overlayCanvas.sortingLayerID);
        Assert.IsFalse(faucetClosed.activeSelf);
        Assert.IsTrue(faucetOpen.activeSelf);
        Assert.IsTrue(water.activeSelf);

        Object.DestroyImmediate(rootCanvasGo);
        Object.DestroyImmediate(puzzleRoot);
    }

    static string FindParentCanvasBlock(string sceneText, string childRectBlock)
    {
        Match parentMatch = Regex.Match(childRectBlock, @"m_Father: \{fileID: (?<id>[0-9]+)\}");
        Assert.IsTrue(parentMatch.Success, "Could not find parent transform for sink panel.");

        string parentRectId = parentMatch.Groups["id"].Value;
        string parentRectBlock = FindObjectBlock(sceneText, "224", parentRectId);
        string parentGameObjectBlock = FindGameObjectBlockByRectTransform(sceneText, parentRectBlock);
        return FindComponentBlockOnGameObject(sceneText, parentGameObjectBlock, "223");
    }

    static string FindGameObjectBlockByRectTransform(string sceneText, string rectBlock)
    {
        string rectId = ExtractFileId(rectBlock);
        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!1 &[0-9]+\r?\nGameObject:[\s\S]*?(?=--- !u!)",
            RegexOptions.Multiline))
        {
            if (match.Value.Contains($"component: {{fileID: {rectId}}}"))
                return match.Value;
        }

        Assert.Fail($"Could not find GameObject owning RectTransform {rectId}.");
        return string.Empty;
    }

    static string FindBlockByName(string sceneText, string blockName)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!114 &[0-9]+\r?\nMonoBehaviour:[\s\S]*?blockName: {Regex.Escape(blockName)}[\s\S]*?(?=--- !u!114 &|\Z)",
            RegexOptions.Multiline);

        Assert.IsTrue(match.Success, $"Could not find Fungus block '{blockName}'.");
        return match.Value;
    }

    static string ReadKitchenSceneText()
    {
        return File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scenes",
            "Mokotan",
            "First Floor",
            "1foorLeft",
            "Kitchen.unity"));
    }

    static string FindWaterPrefabInstanceBlock(string sceneText)
    {
        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!1001 &[0-9]+\r?\nPrefabInstance:\r?\n[\s\S]*?(?=--- !u!)",
            RegexOptions.Multiline))
        {
            if (match.Value.Contains(
                    $"m_SourcePrefab: {{fileID: 100100000, guid: {WaterPrefabGuid}, type: 3}}"))
                return match.Value;
        }

        Assert.Fail("Could not find Water prefab instance in Kitchen scene.");
        return string.Empty;
    }

    static string FindMonoBehaviourOnGameObject(string sceneText, string gameObjectName, string scriptGuid)
    {
        string gameObjectBlock = FindGameObjectBlock(sceneText, gameObjectName);
        return FindComponentBlockOnGameObject(sceneText, gameObjectBlock, "114", scriptGuid);
    }

    static string FindGameObjectBlock(string sceneText, string objectName)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!1 &[0-9]+\r?\nGameObject:\r?\n(?:(?!^--- ).)*?m_Name: {Regex.Escape(objectName)}(?:(?!^--- ).)*",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.IsTrue(match.Success, $"Could not find GameObject named {objectName}.");
        return match.Value;
    }

    static string FindRectTransformBlock(string sceneText, string objectName)
    {
        string gameObjectBlock = FindGameObjectBlock(sceneText, objectName);
        string rectFileId = FindComponentFileId(gameObjectBlock, 0);
        return FindObjectBlock(sceneText, "224", rectFileId);
    }

    static int GetChildSiblingIndex(string parentTransformBlock, string childTransformFileId)
    {
        MatchCollection matches = Regex.Matches(parentTransformBlock, @"- \{fileID: (?<id>[0-9]+)\}");
        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i].Groups["id"].Value == childTransformFileId)
                return i;
        }

        Assert.Fail($"Child transform {childTransformFileId} was not listed under its parent.");
        return -1;
    }

    static string ExtractFileId(string block)
    {
        Match match = Regex.Match(block, @"--- !u!\d+ &(?<id>[0-9]+)");
        Assert.IsTrue(match.Success, "Could not extract file id from Unity YAML block.");
        return match.Groups["id"].Value;
    }

    static string FindComponentFileId(string gameObjectBlock, int componentIndex)
    {
        MatchCollection matches = Regex.Matches(gameObjectBlock, @"- component: \{fileID: (?<id>[0-9]+)\}");
        Assert.Greater(matches.Count, componentIndex, $"GameObject did not have component index {componentIndex}.");
        return matches[componentIndex].Groups["id"].Value;
    }

    static string FindComponentBlockOnGameObject(
        string sceneText,
        string gameObjectBlock,
        string unityType,
        string scriptGuid = null)
    {
        foreach (Match match in Regex.Matches(gameObjectBlock, @"- component: \{fileID: (?<id>[0-9]+)\}"))
        {
            string fileId = match.Groups["id"].Value;
            if (!Regex.IsMatch(sceneText, $@"--- !u!{Regex.Escape(unityType)} &{Regex.Escape(fileId)}\r?\n"))
                continue;

            string block = FindObjectBlock(sceneText, unityType, fileId);
            if (scriptGuid != null && !block.Contains($"guid: {scriptGuid}"))
                continue;

            return block;
        }

        Assert.Fail($"Could not find component !u!{unityType} on GameObject.");
        return string.Empty;
    }

    static string FindObjectBlock(string sceneText, string unityType, string fileId)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!{Regex.Escape(unityType)} &{Regex.Escape(fileId)}\r?\n(?:(?!^--- ).)*",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.IsTrue(match.Success, $"Could not find Unity object !u!{unityType} &{fileId}.");
        return match.Value;
    }
}
