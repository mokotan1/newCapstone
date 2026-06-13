using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class KitchenFripanClickTests
{
    [Test]
    public void FripanPanel_CanvasGroup_DoesNotBlockChildBurnerClicks()
    {
        string sceneText = ReadKitchenSceneText();
        string panelObject = FindGameObjectBlock(sceneText, "firpan_Panel");
        string canvasGroupComponent = FindComponentBlockOnGameObject(sceneText, panelObject, "225");

        StringAssert.Contains("m_BlocksRaycasts: 1", canvasGroupComponent);
        StringAssert.Contains("m_Interactable: 1", canvasGroupComponent);

        string panelImageComponent = FindComponentBlockOnGameObject(
            sceneText,
            panelObject,
            "114",
            "fe87c0e1cc204ed48ad3b37840f39efc");
        StringAssert.Contains("m_RaycastTarget: 0", panelImageComponent);
    }

    [Test]
    public void FripanPanel_BurnerButton_KeepsRaycastTargetEnabled()
    {
        string sceneText = ReadKitchenSceneText();
        string burnerObject = FindGameObjectBlock(sceneText, "burner");
        string imageComponent = FindComponentBlockOnGameObject(
            sceneText,
            burnerObject,
            "114",
            "fe87c0e1cc204ed48ad3b37840f39efc");
        string buttonComponent = FindComponentBlockOnGameObject(
            sceneText,
            burnerObject,
            "114",
            "4e29b1a8efbd4b44bb3f3716e73f07ff");

        StringAssert.Contains("m_RaycastTarget: 1", imageComponent);
        StringAssert.Contains("m_MethodName: OnInteraction", buttonComponent);
        StringAssert.Contains("m_StringArgument: burner", buttonComponent);
    }

    [Test]
    public void FripanWorldClick_IsRoutedThroughKitchenInteractionController()
    {
        string sceneText = ReadKitchenSceneText();
        string fripanObject = FindGameObjectBlock(sceneText, "Fripan");
        string colliderFileId = FindComponentFileId(fripanObject, 2);
        string clickableFileId = FindComponentFileId(fripanObject, 3);

        StringAssert.Contains("a7c3e8914b2d4f6e9a1c5d8e3f7b2a04", sceneText);
        StringAssert.Contains(
            $"interactionId: fripan",
            sceneText,
            "KitchenInteractionController should register a fripan world click route.");
        StringAssert.Contains(
            $"collider: {{fileID: {colliderFileId}}}",
            sceneText,
            "Fripan collider should be bound on KitchenInteractionController.worldClicks.");
        StringAssert.Contains(
            $"clickable: {{fileID: {clickableFileId}}}",
            sceneText,
            "Fripan Clickable2D should be bound on KitchenInteractionController.worldClicks.");
        StringAssert.DoesNotContain(
            $"clickableObject: {{fileID: {clickableFileId}}}",
            sceneText,
            "Fripan should no longer use a direct Fungus ObjectClicked handler.");
    }

    [Test]
    public void BurnerWorldClick_IsRoutedThroughKitchenInteractionController()
    {
        string sceneText = ReadKitchenSceneText();
        string burnerObject = FindGameObjectBlock(sceneText, "Burner");
        string colliderFileId = FindComponentFileId(burnerObject, 2);
        string clickableFileId = FindComponentFileId(burnerObject, 3);

        StringAssert.Contains(
            $"interactionId: burner",
            sceneText,
            "KitchenInteractionController should register a burner world click route.");
        StringAssert.Contains(
            $"collider: {{fileID: {colliderFileId}}}",
            sceneText,
            "Burner collider should be bound on KitchenInteractionController.worldClicks.");
        StringAssert.Contains(
            $"clickable: {{fileID: {clickableFileId}}}",
            sceneText,
            "Burner Clickable2D should be bound on KitchenInteractionController.worldClicks.");
    }

    [Test]
    public void BurnerAndFripanSetInteractableTargets_DoNotContainMissingReferences()
    {
        string sceneText = ReadKitchenSceneText();
        foreach (Match match in Regex.Matches(sceneText, @"targetObjects:\r?\n(?<items>(?:  - \{fileID: [0-9]+\}\r?\n)+)"))
        {
            string items = match.Groups["items"].Value;
            bool controlsBurnerAndFripan = items.Contains("{fileID: 243801839}")
                && items.Contains("{fileID: 586477519}");

            if (!controlsBurnerAndFripan)
                continue;

            StringAssert.DoesNotContain(
                "{fileID: 0}",
                items,
                "A Set Interactable command for the burner/fripan controls has a missing target.");
        }
    }

    [Test]
    public void MainTagCanvas_UsesOverlayAnd1980ReferenceResolution()
    {
        string sceneText = ReadKitchenSceneText();
        string canvasObject = FindLayeredGameObjectBlock(sceneText, "Canvas", 5);

        string canvasFileId = FindComponentFileId(canvasObject, 1);
        string scalerFileId = FindComponentFileId(canvasObject, 2);

        string canvasComponent = FindObjectBlock(sceneText, "223", canvasFileId);
        StringAssert.Contains("m_RenderMode: 0", canvasComponent);
        StringAssert.Contains("m_Camera: {fileID: 0}", canvasComponent);

        string scalerComponent = FindObjectBlock(sceneText, "114", scalerFileId);
        StringAssert.Contains("m_UiScaleMode: 1", scalerComponent);
        StringAssert.Contains("m_ReferenceResolution: {x: 1980, y: 1080}", scalerComponent);
    }

    [Test]
    public void MigratedFungusBlocks_AreNotCalledByUnityEventExecuteBlock()
    {
        string sceneText = ReadKitchenSceneText();

        foreach (string blockName in KitchenSceneMigrationSpecs.MigratedFungusBlockNames)
        {
            Assert.IsFalse(
                UnityEventCallsExecuteBlock(sceneText, blockName),
                $"Kitchen scene still wires ExecuteBlock('{blockName}') on a Button or drop-zone onUnlock.");
        }
    }

    [Test]
    public void FungusClickTriggers_ForMigratedBlocks_AreDisabled()
    {
        string sceneText = ReadKitchenSceneText();

        foreach (string blockName in KitchenSceneMigrationSpecs.MigratedFungusBlockNames)
            AssertFungusClickTriggerDisabledWhenPresent(sceneText, blockName);
    }

    [Test]
    public void KitchenInteractionController_Routes_AllMigrationTargets()
    {
        string sceneText = ReadKitchenSceneText();
        string controllerBlock = FindInteractionControllerBlock(sceneText);
        var foundRoutes = ParseInteractionRoutes(controllerBlock);

        foreach ((string interactionId, string blockName) in KitchenSceneMigrationSpecs.AllInteractionRoutes())
        {
            Assert.IsTrue(
                foundRoutes.TryGetValue(interactionId, out string actualBlock),
                $"KitchenInteractionController.routes is missing '{interactionId}'.");
            Assert.AreEqual(
                blockName,
                actualBlock,
                $"Route '{interactionId}' should map to '{blockName}'.");
        }
    }

    [Test]
    public void KitchenInteractionController_WorldClicks_CoverWorldClickTargets()
    {
        string sceneText = ReadKitchenSceneText();
        string controllerBlock = FindInteractionControllerBlock(sceneText);
        var foundIds = ParseWorldClickInteractionIds(controllerBlock);

        foreach (string interactionId in KitchenSceneMigrationSpecs.WorldClickInteractionIds)
        {
            Assert.IsTrue(
                foundIds.Contains(interactionId),
                $"KitchenInteractionController.worldClicks is missing '{interactionId}'.");
        }
    }

    [Test]
    public void SinkDropzone_OnUnlock_CallsKitchenInteractionController()
    {
        AssertDropZoneUnlockRoutesToController("SinkDropzone", "bottle_drag");
    }

    [Test]
    public void BurnerDropzone_OnUnlock_CallsKitchenInteractionController()
    {
        AssertDropZoneUnlockRoutesToController("BurnerDropzone", "food_drag");
    }

    [Test]
    public void UiClickRoutes_CallOnInteractionWithExpectedIds()
    {
        string sceneText = ReadKitchenSceneText();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);

        foreach ((string interactionId, _) in KitchenSceneMigrationSpecs.UiClickRoutes)
        {
            if (!seen.Add(interactionId))
                continue;

            Assert.IsTrue(
                Regex.IsMatch(
                    sceneText,
                    $@"m_MethodName: OnInteraction\r?\n\s*m_Mode:[\s\S]*?m_StringArgument: {Regex.Escape(interactionId)}",
                    RegexOptions.Multiline),
                $"Kitchen UI should wire OnInteraction('{interactionId}').");
        }
    }

    static void AssertDropZoneUnlockRoutesToController(string dropZoneName, string interactionId)
    {
        string sceneText = ReadKitchenSceneText();
        string dropZoneComponent = FindMonoBehaviourOnGameObject(
            sceneText,
            dropZoneName,
            KitchenSceneMigrationSpecs.WorldItemDropZoneScriptGuid);

        StringAssert.Contains("m_MethodName: OnInteraction", dropZoneComponent);
        StringAssert.Contains($"m_StringArgument: {interactionId}", dropZoneComponent);
        StringAssert.Contains(
            RoomInteractionSceneMigrationEditor.KitchenControllerTypeName,
            dropZoneComponent);
        StringAssert.DoesNotContain("m_MethodName: ExecuteBlock", dropZoneComponent);
    }

    static string ReadKitchenSceneText() => File.ReadAllText(KitchenScenePath);

    static bool UnityEventCallsExecuteBlock(string sceneText, string blockName)
    {
        return Regex.IsMatch(
            sceneText,
            $@"m_MethodName: ExecuteBlock[\s\S]*?m_StringArgument: {Regex.Escape(blockName)}",
            RegexOptions.Multiline);
    }

    static void AssertFungusClickTriggerDisabledWhenPresent(string sceneText, string blockName)
    {
        string pattern =
            $@"--- !u!114 &[0-9]+\r?\nMonoBehaviour:\r?\n[\s\S]*?guid: {KitchenSceneMigrationSpecs.FungusClickTriggerScriptGuid}[\s\S]*?blockToExecute: {Regex.Escape(blockName)}[\s\S]*?(?=--- !u!)";

        Match match = Regex.Match(sceneText, pattern, RegexOptions.Multiline);
        if (!match.Success)
            return;

        StringAssert.Contains(
            "m_Enabled: 0",
            match.Value,
            $"FungusClickTrigger for '{blockName}' must stay disabled after R6-A migration.");
    }

    static string FindInteractionControllerBlock(string sceneText)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!114 &[0-9]+\r?\nMonoBehaviour:\r?\n[\s\S]*?guid: {KitchenSceneMigrationSpecs.InteractionControllerScriptGuid}[\s\S]*?(?=--- !u!)",
            RegexOptions.Multiline);

        Assert.IsTrue(match.Success, "KitchenInteractionController MonoBehaviour block not found.");
        return match.Value;
    }

    static Dictionary<string, string> ParseInteractionRoutes(string controllerBlock)
    {
        var routes = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(
            controllerBlock,
            @"interactionId: (?<id>[a-z_]+)\r?\n\s*fungusBlockName: (?<block>[A-Za-z0-9_]+)",
            RegexOptions.Multiline))
        {
            routes[match.Groups["id"].Value] = match.Groups["block"].Value;
        }

        return routes;
    }

    static HashSet<string> ParseWorldClickInteractionIds(string controllerBlock)
    {
        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        int worldClicksIndex = controllerBlock.IndexOf("worldClicks:", System.StringComparison.Ordinal);
        int routesIndex = controllerBlock.IndexOf("routes:", System.StringComparison.Ordinal);
        Assert.Greater(worldClicksIndex, -1);
        Assert.Greater(routesIndex, worldClicksIndex);

        string worldClicksSection = controllerBlock.Substring(worldClicksIndex, routesIndex - worldClicksIndex);
        foreach (Match match in Regex.Matches(
            worldClicksSection,
            @"interactionId: (?<id>[a-z_]+)",
            RegexOptions.Multiline))
        {
            ids.Add(match.Groups["id"].Value);
        }

        return ids;
    }

    static string FindMonoBehaviourOnGameObject(string sceneText, string gameObjectName, string scriptGuid)
    {
        string gameObjectBlock = FindGameObjectBlock(sceneText, gameObjectName);
        string gameObjectFileId = Regex.Match(gameObjectBlock, @"--- !u!1 &(?<id>[0-9]+)").Groups["id"].Value;

        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!114 &(?<cid>[0-9]+)\r?\nMonoBehaviour:\r?\n(?:(?!^--- ).)*",
            RegexOptions.Multiline))
        {
            string block = match.Value;
            if (!block.Contains($"m_GameObject: {{fileID: {gameObjectFileId}}}"))
                continue;
            if (!block.Contains($"guid: {scriptGuid}"))
                continue;

            return block;
        }

        Assert.Fail($"Could not find MonoBehaviour guid '{scriptGuid}' on GameObject '{gameObjectName}'.");
        return string.Empty;
    }

    private static string KitchenScenePath
    {
        get
        {
            return Path.Combine(
                Application.dataPath,
                "Scenes",
                "Mokotan",
                "First Floor",
                "1foorLeft",
                "Kitchen.unity");
        }
    }

    private static string FindGameObjectBlock(string sceneText, string objectName)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!1 &[0-9]+\r?\nGameObject:\r?\n(?:(?!^--- ).)*?m_Name: {Regex.Escape(objectName)}(?:(?!^--- ).)*",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.IsTrue(match.Success, $"Could not find GameObject named {objectName}.");
        return match.Value;
    }

    private static string FindLayeredGameObjectBlock(string sceneText, string objectName, int layer)
    {
        foreach (Match match in Regex.Matches(
            sceneText,
            $@"--- !u!1 &[0-9]+\r?\nGameObject:\r?\n(?:(?!^--- ).)*?m_Name: {Regex.Escape(objectName)}(?:(?!^--- ).)*",
            RegexOptions.Multiline | RegexOptions.Singleline))
        {
            if (match.Value.Contains($"m_Layer: {layer}"))
                return match.Value;
        }

        Assert.Fail($"Could not find GameObject named {objectName} on layer {layer}.");
        return string.Empty;
    }

    private static string FindComponentFileId(string gameObjectBlock, int componentIndex)
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

    private static string FindObjectBlock(string sceneText, string unityType, string fileId)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!{Regex.Escape(unityType)} &{Regex.Escape(fileId)}\r?\n(?:(?!^--- ).)*",
            RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.IsTrue(match.Success, $"Could not find Unity object !u!{unityType} &{fileId}.");
        return match.Value;
    }
}
