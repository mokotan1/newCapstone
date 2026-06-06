using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class KitchenFripanClickTests
{
    [Test]
    public void FripanWorldClick_IsRoutedThroughKitchenInteractionController()
    {
        string sceneText = File.ReadAllText(KitchenScenePath);
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
    public void BurnerAndFripanSetInteractableTargets_DoNotContainMissingReferences()
    {
        string sceneText = File.ReadAllText(KitchenScenePath);
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
        string sceneText = File.ReadAllText(KitchenScenePath);
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
