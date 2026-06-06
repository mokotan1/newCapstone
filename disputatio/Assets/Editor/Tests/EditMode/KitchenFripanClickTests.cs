using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class KitchenFripanClickTests
{
    [Test]
    public void FripanClickable_IsConnectedToObjectClickedEvent()
    {
        string sceneText = File.ReadAllText(KitchenScenePath);
        string fripanObject = FindGameObjectBlock(sceneText, "Fripan");
        string clickableFileId = FindComponentBeforeCollider(fripanObject);

        StringAssert.Contains(
            $"clickableObject: {{fileID: {clickableFileId}}}",
            sceneText,
            "Fripan has a Clickable2D component, but no Fungus ObjectClicked event listens to it.");
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

    private static string FindComponentBeforeCollider(string gameObjectBlock)
    {
        Match match = Regex.Match(
            gameObjectBlock,
            @"m_Component:\r?\n\s+- component: \{fileID: (?<transform>[0-9]+)\}\r?\n\s+- component: \{fileID: (?<sprite>[0-9]+)\}\r?\n\s+- component: \{fileID: (?<collider>[0-9]+)\}\r?\n\s+- component: \{fileID: (?<clickable>[0-9]+)\}");

        Assert.IsTrue(match.Success, "Fripan does not have the expected Transform/SpriteRenderer/Collider2D/Clickable2D component layout.");
        return match.Groups["clickable"].Value;
    }
}
