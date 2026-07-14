using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// MaidRoom.unity Fungus/씬 배선 회귀 잠금.
/// food 획득 시 pickup+effect 비활성, PuzzleBook_SelectYes는 컨트롤러 openPanel에 위임.
/// </summary>
[TestFixture]
public class MaidRoomSceneFlowTests
{
    const string MaidRoomSceneRelativePath =
        "Scenes/Mokotan/First Floor/1floorRight/MaidRoom.unity";
    const string FoodBlockName = "food";
    const string PuzzleBookSelectYesBlockName = "PuzzleBook_SelectYes";
    const string FoodPickupFileId = "302541021";
    const string FoodItemEffectFileId = "1934550032";
    const string PuzzlePanelFileId = "1407025573";
    const string DisabledPuzzleBookSetActivePanelCommandId = "285511175";
    const string DisabledPuzzleBookSetActiveDiaryCommandId = "285511291";
    const string SetActiveCommandScriptGuid = "dbd8c931f22994b9d90e2037fffaa770";
    const string FungusBlockScriptGuid = "3d3d73aef2cfc4f51abf34ac00241f60";

    [Test]
    public void FoodBlock_DeactivatesFoodPickupAndFoodItemEffect()
    {
        string sceneText = ReadMaidRoomSceneText();
        string foodBlock = FindBlockByName(sceneText, FoodBlockName);
        List<string> commandIds = ParseCommandListFileIds(foodBlock);

        Assert.IsTrue(
            CommandListContainsSetActive(sceneText, commandIds, FoodPickupFileId, active: false),
            "food must SetActive(false) the food pickup GameObject.");
        Assert.IsTrue(
            CommandListContainsSetActive(sceneText, commandIds, FoodItemEffectFileId, active: false),
            "food must SetActive(false) FoodItemEffect (fileID 1934550032).");
    }

    [Test]
    public void FoodBlock_FoodItemEffectSetActiveFalseIsEnabled()
    {
        string sceneText = ReadMaidRoomSceneText();
        string foodBlock = FindBlockByName(sceneText, FoodBlockName);

        foreach (string commandId in ParseCommandListFileIds(foodBlock))
        {
            string command = FindObjectBlock(sceneText, "114", commandId);
            if (!IsSetActiveOnTarget(command, FoodItemEffectFileId, active: false))
                continue;

            Assert.IsTrue(
                Regex.IsMatch(command, @"m_Enabled: 1\r?\n"),
                "FoodItemEffect SetActive(false) command must remain enabled.");
            return;
        }

        Assert.Fail("food block has no enabled SetActive(false) targeting FoodItemEffect.");
    }

    [Test]
    public void PuzzleBookSelectYes_DoesNotReferenceDisabledSetActiveCommands()
    {
        string sceneText = ReadMaidRoomSceneText();
        string block = FindBlockByName(sceneText, PuzzleBookSelectYesBlockName);
        List<string> commandIds = ParseCommandListFileIds(block);

        CollectionAssert.DoesNotContain(
            commandIds,
            DisabledPuzzleBookSetActivePanelCommandId,
            "PuzzleBook_SelectYes must not keep disabled Set Active for PuzzlePanel; controller owns open.");
        CollectionAssert.DoesNotContain(
            commandIds,
            DisabledPuzzleBookSetActiveDiaryCommandId,
            "PuzzleBook_SelectYes must not keep disabled Set Active for diary; controller owns open.");
    }

    [Test]
    public void MaidRoomPuzzleController_PuzzleBookSelectYes_OpensPuzzlePanel()
    {
        string sceneText = ReadMaidRoomSceneText();
        string controller = FindMaidRoomPuzzleController(sceneText);

        StringAssert.IsMatch(
            $@"blockName: PuzzleBook_SelectYes\r?\n\s*openPanel: \{{fileID: {PuzzlePanelFileId}\}}",
            controller,
            "blockOutcomes must open PuzzlePanel fileID 1407025573 on PuzzleBook_SelectYes.");
    }

    [Test]
    public void CookBookPanel_NextPageClickArea_BindsRightClickArea()
    {
        string sceneText = ReadMaidRoomSceneText();
        string cookBookPanel = FindGameObjectBlock(sceneText, "CookBook_Panel");
        string bookPanel = FindComponentBlockOnGameObject(
            sceneText,
            cookBookPanel,
            "114",
            scriptGuid: "009665f9edab7ce4fa70d08f9cbff703");

        StringAssert.Contains(
            "nextPageClickArea: {fileID: 1164327270}",
            bookPanel,
            "CookBook_Panel BookPanelController must bind nextPageClickArea to RightClickArea.");
    }

    static bool CommandListContainsSetActive(
        string sceneText,
        IReadOnlyList<string> commandIds,
        string targetFileId,
        bool active)
    {
        foreach (string commandId in commandIds)
        {
            string command = FindObjectBlock(sceneText, "114", commandId);
            if (IsSetActiveOnTarget(command, targetFileId, active))
                return true;
        }

        return false;
    }

    static bool IsSetActiveOnTarget(string commandYaml, string targetFileId, bool active)
    {
        if (!commandYaml.Contains($"guid: {SetActiveCommandScriptGuid}"))
            return false;
        if (!commandYaml.Contains($"gameObjectVal: {{fileID: {targetFileId}}}"))
            return false;
        return commandYaml.Contains(active ? "booleanVal: 1" : "booleanVal: 0");
    }

    static List<string> ParseCommandListFileIds(string blockYaml)
    {
        Match emptyList = Regex.Match(blockYaml, @"commandList:\s*\[\s*\]");
        if (emptyList.Success)
            return new List<string>();

        Match commandListMatch = Regex.Match(
            blockYaml,
            @"commandList:\r?\n(?<items>(?:  - \{fileID: [0-9]+\}\r?\n)*)");
        Assert.IsTrue(commandListMatch.Success, "Could not parse commandList.");

        return commandListMatch.Groups["items"].Value
            .Split('\n')
            .Select(line => Regex.Match(line, @"\{fileID: (?<id>[0-9]+)\}"))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToList();
    }

    static string FindBlockByName(string sceneText, string blockName)
    {
        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!114 &[0-9]+\r?\nMonoBehaviour:(?:(?!^--- ).)*",
            RegexOptions.Multiline | RegexOptions.Singleline))
        {
            string block = match.Value;
            if (!block.Contains($"guid: {FungusBlockScriptGuid}"))
                continue;
            if (!Regex.IsMatch(block, $@"blockName: {Regex.Escape(blockName)}\r?\n"))
                continue;
            return block;
        }

        Assert.Fail($"Could not find Fungus block '{blockName}'.");
        return string.Empty;
    }

    static string FindMaidRoomPuzzleController(string sceneText)
    {
        // Prefer known controller type name in YAML; fall back to openPanel wiring block.
        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!114 &[0-9]+\r?\nMonoBehaviour:[\s\S]*?(?=--- !u!)",
            RegexOptions.Multiline))
        {
            if (match.Value.Contains("blockName: PuzzleBook_SelectYes")
                && match.Value.Contains("blockOutcomes:")
                && match.Value.Contains($"openPanel: {{fileID: {PuzzlePanelFileId}}}"))
                return match.Value;
        }

        Assert.Fail("Could not find MaidRoomPuzzleController blockOutcomes YAML.");
        return string.Empty;
    }

    static string FindGameObjectBlock(string sceneText, string objectName)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!1 &[0-9]+\r?\nGameObject:[\s\S]*?m_Name: {Regex.Escape(objectName)}[\s\S]*?(?=--- !u!)",
            RegexOptions.Multiline);
        Assert.IsTrue(match.Success, $"Could not find GameObject named {objectName}.");
        return match.Value;
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

    static string ReadMaidRoomSceneText()
    {
        return File.ReadAllText(Path.Combine(Application.dataPath, MaidRoomSceneRelativePath));
    }
}
