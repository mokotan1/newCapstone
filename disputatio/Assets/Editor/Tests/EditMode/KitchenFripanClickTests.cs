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

    private static string FindComponentBeforeCollider(string gameObjectBlock)
    {
        Match match = Regex.Match(
            gameObjectBlock,
            @"m_Component:\r?\n\s+- component: \{fileID: (?<transform>[0-9]+)\}\r?\n\s+- component: \{fileID: (?<sprite>[0-9]+)\}\r?\n\s+- component: \{fileID: (?<collider>[0-9]+)\}\r?\n\s+- component: \{fileID: (?<clickable>[0-9]+)\}");

        Assert.IsTrue(match.Success, "Fripan does not have the expected Transform/SpriteRenderer/Collider2D/Clickable2D component layout.");
        return match.Groups["clickable"].Value;
    }
}
