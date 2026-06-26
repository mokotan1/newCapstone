using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class KitchenAddKeyFlowTests
{
    const string KitchenSceneRelativePath = "Scenes/Mokotan/First Floor/1foorLeft/Kitchen.unity";
    const string AddKeyBlockName = "addKey";
    const string SinkPanelObjectName = "Sink_Pannel";
    const string SinkWaterOverlayObjectName = "SinkWaterOverlay";
    const string MaidRoomKeyObjectName = "MaidRoomKey";
    const string MaidRoomKeySceneFileId = "1172732410";
    const string MaidRoomKeyRectTransformFileId = "1172732411";
    const string SetActiveCommandScriptGuid = "dbd8c931f22994b9d90e2037fffaa770";
    const string AnimatorSetTriggerScriptGuid = "0729d4f369e6a4241a6e7e3b8b9c1933";
    const string InvokeMethodScriptGuid = "688e35811870d403f9e2b1ab2a699d98";
    const string SetVariableScriptGuid = "fb77d0ce495044f6e9feb91b31798e8c";
    const string ItemPickupScriptGuid = "36992d1989d442a48a31cb0b8485dee3";
    const string ButtonScriptGuid = "4e29b1a8efbd4b44bb3f3716e73f07ff";
    const string FungusBlockScriptGuid = "3d3d73aef2cfc4f51abf34ac00241f60";
    const string FaucetBlockName = "Faucet";
    const string BottleYesBlockName = "yes";
    const string FaucetKeyReleaseControllerScriptGuid = "a1d3f4590f2949b9ad7f41d5e01b4e33";

    [Test]
    public void AddKeyBlock_IncludesMaidRoomKeySetActiveTrue()
    {
        string addKeyBlock = FindBlockByName(ReadKitchenSceneText(), AddKeyBlockName);
        Assert.IsTrue(
            CommandListReferencesContainMaidRoomKeySetActive(addKeyBlock, active: true),
            "addKey must SetActive(true) MaidRoomKey so the key can appear.");
    }

    [Test]
    public void AddKeyBlock_SetActiveTrueRunsBeforeMoveTrigger()
    {
        string addKeyBlock = FindBlockByName(ReadKitchenSceneText(), AddKeyBlockName);
        List<string> commandFileIds = ParseCommandListFileIds(addKeyBlock);
        string sceneText = ReadKitchenSceneText();

        int setActiveIndex = FindFirstCommandIndex(
            sceneText,
            commandFileIds,
            command => IsSetActiveOnMaidRoomKey(command, active: true));
        int moveTriggerIndex = FindFirstCommandIndex(
            sceneText,
            commandFileIds,
            command => IsAnimatorMoveTriggerOnMaidRoomKey(command));

        Assert.GreaterOrEqual(setActiveIndex, 0, "addKey must include MaidRoomKey SetActive(true).");
        Assert.GreaterOrEqual(moveTriggerIndex, 0, "addKey must include MaidRoomKey MoveTrigger.");
        Assert.Less(
            setActiveIndex,
            moveTriggerIndex,
            "MaidRoomKey must become active before the float animation trigger runs.");
    }

    [Test]
    public void AddKeyBlock_DoesNotSetMaidRoomKeyInactive()
    {
        string addKeyBlock = FindBlockByName(ReadKitchenSceneText(), AddKeyBlockName);
        Assert.IsFalse(
            CommandListReferencesContainMaidRoomKeySetActive(addKeyBlock, active: false),
            "addKey must not hide MaidRoomKey; pickup happens via player click.");
    }

    [Test]
    public void AddKeyBlock_CommandListContainsOnlySetActiveAndMoveTrigger()
    {
        string addKeyBlock = FindBlockByName(ReadKitchenSceneText(), AddKeyBlockName);
        List<string> commandFileIds = ParseCommandListFileIds(addKeyBlock);

        Assert.AreEqual(2, commandFileIds.Count, "addKey must contain exactly two commands.");
    }

    [Test]
    public void AddKeyBlock_DoesNotSetHaveMaidKey()
    {
        string sceneText = ReadKitchenSceneText();
        string addKeyBlock = FindBlockByName(sceneText, AddKeyBlockName);

        foreach (string commandFileId in ParseCommandListFileIds(addKeyBlock))
        {
            string command = FindObjectBlock(sceneText, "114", commandFileId);
            if (!command.Contains($"guid: {SetVariableScriptGuid}"))
                continue;

            StringAssert.DoesNotContain("key: HaveMaidKey", command);
            StringAssert.DoesNotContain("variable: {fileID: 290853994}", command);
        }
    }

    [Test]
    public void AddKeyBlock_DoesNotReferenceBottleClicked()
    {
        string sceneText = ReadKitchenSceneText();
        string addKeyBlock = FindBlockByName(sceneText, AddKeyBlockName);

        foreach (string commandFileId in ParseCommandListFileIds(addKeyBlock))
        {
            string command = FindObjectBlock(sceneText, "114", commandFileId);
            StringAssert.DoesNotContain("BottleClicked", command);
        }
    }

    [Test]
    public void FaucetBlock_DoesNotReuseAddKeyMoveTriggerCommand()
    {
        string sceneText = ReadKitchenSceneText();
        string faucetBlock = FindBlockByName(sceneText, FaucetBlockName);
        string addKeyBlock = FindBlockByName(sceneText, AddKeyBlockName);

        List<string> faucetCommands = ParseCommandListFileIds(faucetBlock);
        List<string> addKeyCommands = ParseCommandListFileIds(addKeyBlock);

        foreach (string addKeyCommandId in addKeyCommands)
        {
            if (!IsAnimatorMoveTriggerOnMaidRoomKey(FindObjectBlock(sceneText, "114", addKeyCommandId)))
                continue;

            Assert.IsFalse(
                faucetCommands.Contains(addKeyCommandId),
                "Faucet must not share addKey's MaidRoomKey MoveTrigger command.");
        }
    }

    [Test]
    public void AddKeyBlock_DoesNotDirectlyPickUpItem()
    {
        string sceneText = ReadKitchenSceneText();
        string addKeyBlock = FindBlockByName(sceneText, AddKeyBlockName);

        foreach (string commandFileId in ParseCommandListFileIds(addKeyBlock))
        {
            string command = FindObjectBlock(sceneText, "114", commandFileId);
            if (!command.Contains($"guid: {InvokeMethodScriptGuid}"))
                continue;

            StringAssert.DoesNotContain("targetMethod: AddItem", command);
            StringAssert.DoesNotContain("targetMethod: OnPointerClick", command);
            StringAssert.DoesNotContain("targetMethod: PickUpDirect", command);
        }
    }

    [Test]
    public void BottleYesBlock_DoesNotActivateMaidRoomKey()
    {
        string sceneText = ReadKitchenSceneText();
        string yesBlock = FindBlockByName(sceneText, BottleYesBlockName);

        foreach (string commandFileId in ParseCommandListFileIds(yesBlock))
        {
            string command = FindObjectBlock(sceneText, "114", commandFileId);
            Assert.IsFalse(
                IsSetActiveOnMaidRoomKey(command, active: true),
                "Bottle yes must not spawn MaidRoomKey; key spawning belongs to addKey after FaucetClicked.");
            Assert.IsFalse(
                IsAnimatorMoveTriggerOnMaidRoomKey(command),
                "Bottle yes must not trigger MaidRoomKey float animation.");
        }
    }

    [Test]
    public void FaucetKeyReleaseController_ReferencesAddKeyBlockAndFaucetClicked()
    {
        string sceneText = ReadKitchenSceneText();
        string controllerBlock = FindMonoBehaviourByScriptGuid(sceneText, FaucetKeyReleaseControllerScriptGuid);

        StringAssert.Contains("faucetBoolName: FaucetClicked", controllerBlock);
        StringAssert.Contains("keySpawnBlockName: addKey", controllerBlock);
        StringAssert.Contains($"targetFlowchart: {{fileID: 290853876}}", controllerBlock);
    }

    [Test]
    public void FaucetKeyReleaseController_ReferencesDirectMaidRoomKeyTarget()
    {
        string sceneText = ReadKitchenSceneText();
        string controllerBlock = FindMonoBehaviourByScriptGuid(sceneText, FaucetKeyReleaseControllerScriptGuid);

        StringAssert.Contains($"keyObject: {{fileID: {MaidRoomKeySceneFileId}}}", controllerBlock);
        StringAssert.Contains("keyObjectName: MaidRoomKey", controllerBlock);
        StringAssert.Contains("keyAnimator: {fileID: 1172732412}", controllerBlock);
        StringAssert.Contains("keyAnimatorTriggerName: MoveTrigger", controllerBlock);
    }

    [Test]
    public void KitchenInteractionController_RoutesFaucetClickToFaucetBlock()
    {
        string sceneText = ReadKitchenSceneText();
        StringAssert.Contains("interactionId: faucet", sceneText);
        StringAssert.Contains("fungusBlockName: Faucet", sceneText);

        Match routeMatch = Regex.Match(
            sceneText,
            @"interactionId: faucet\r?\n\s+fungusBlockName: Faucet");
        Assert.IsTrue(routeMatch.Success, "Kitchen routes must map faucet -> Faucet.");
    }

    [Test]
    public void FaucetButton_WiresOnInteractionWithFaucetId()
    {
        string sceneText = ReadKitchenSceneText();
        string faucetObject = FindGameObjectBlock(sceneText, "Faucet");
        string buttonBlock = FindComponentBlockOnGameObject(
            sceneText,
            faucetObject,
            "114",
            "4e29b1a8efbd4b44bb3f3716e73f07ff");

        StringAssert.Contains("m_MethodName: OnInteraction", buttonBlock);
        StringAssert.Contains("m_StringArgument: faucet", buttonBlock);
        StringAssert.Contains("m_Interactable: 1", buttonBlock);

        string imageBlock = FindComponentBlockOnGameObject(
            sceneText,
            faucetObject,
            "114",
            "fe87c0e1cc204ed48ad3b37840f39efc");
        StringAssert.Contains("m_RaycastTarget: 1", imageBlock);
    }

    [Test]
    public void FaucetKeyReleaseController_IsHostedOnFlowchartNotFaucetButton()
    {
        string sceneText = ReadKitchenSceneText();
        string controllerBlock = FindMonoBehaviourByScriptGuid(sceneText, FaucetKeyReleaseControllerScriptGuid);

        StringAssert.Contains(
            $"m_GameObject: {{fileID: {KitchenSinkWaterDisplayPolicy.KitchenFlowchartSceneFileId}}}",
            controllerBlock);
        StringAssert.DoesNotContain(
            $"m_GameObject: {{fileID: {KitchenSinkWaterDisplayPolicy.FaucetButtonSceneFileId}}}",
            controllerBlock);
    }

    [Test]
    public void MaidRoomKey_ItemPickupComponent_IsEnabledForClickPickup()
    {
        string sceneText = ReadKitchenSceneText();
        string maidRoomKeyObject = FindGameObjectBlock(sceneText, MaidRoomKeyObjectName);
        string itemPickupBlock = FindComponentBlockOnGameObject(
            sceneText,
            maidRoomKeyObject,
            "114",
            ItemPickupScriptGuid);

        StringAssert.Contains("m_Enabled: 1", itemPickupBlock);
    }

    [Test]
    public void MaidRoomKey_IsLastSinkPanelChildAboveBackgroundAndOverlay()
    {
        string sceneText = ReadKitchenSceneText();
        string sinkPanelObject = FindGameObjectBlock(sceneText, SinkPanelObjectName);
        string sinkPanelRectTransform = FindComponentBlockOnGameObject(sceneText, sinkPanelObject, "224");
        List<string> childFileIds = ParseChildFileIds(sinkPanelRectTransform);

        Assert.AreEqual(
            MaidRoomKeyRectTransformFileId,
            childFileIds.Last(),
            "MaidRoomKey must be the last Sink_Pannel child so panel graphics do not cover it.");
    }

    [Test]
    public void MaidRoomKey_CanvasSortsAboveSinkWaterOverlay()
    {
        string sceneText = ReadKitchenSceneText();
        string maidRoomKeyObject = FindGameObjectBlock(sceneText, MaidRoomKeyObjectName);
        string overlayObject = FindGameObjectBlock(sceneText, SinkWaterOverlayObjectName);
        string keyCanvas = FindComponentBlockOnGameObject(sceneText, maidRoomKeyObject, "223");
        string overlayCanvas = FindComponentBlockOnGameObject(sceneText, overlayObject, "223");

        Assert.Greater(
            ParseSortingOrder(keyCanvas),
            ParseSortingOrder(overlayCanvas),
            "MaidRoomKey canvas must sort above SinkWaterOverlay so it remains visible and clickable.");
    }

    [Test]
    public void MaidRoomKey_DoesNotHaveButtonComponent()
    {
        string sceneText = ReadKitchenSceneText();

        Assert.IsFalse(
            MaidRoomKeyMonoBehaviourBlocks(sceneText).Any(block => block.Contains($"guid: {ButtonScriptGuid}")),
            "MaidRoomKey must not have a Button; pickup uses ItemPickup IPointerClickHandler.");
    }

    [Test]
    public void MaidRoomKey_DoesNotWireFilledBottleInteractionRoute()
    {
        foreach (string componentBlock in MaidRoomKeyMonoBehaviourBlocks(ReadKitchenSceneText()))
        {
            StringAssert.DoesNotContain("m_StringArgument: filled_bottle", componentBlock);
            StringAssert.DoesNotContain("m_MethodName: OnInteraction", componentBlock);
        }
    }

    static IEnumerable<string> MaidRoomKeyMonoBehaviourBlocks(string sceneText)
    {
        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!114 &[0-9]+\r?\nMonoBehaviour:[\s\S]*?(?=--- !u!)",
            RegexOptions.Multiline))
        {
            if (match.Value.Contains($"m_GameObject: {{fileID: {MaidRoomKeySceneFileId}}}"))
                yield return match.Value;
        }
    }

    static bool CommandListReferencesContainMaidRoomKeySetActive(string blockYaml, bool active)
    {
        foreach (string commandFileId in ParseCommandListFileIds(blockYaml))
        {
            string command = FindObjectBlock(ReadKitchenSceneText(), "114", commandFileId);
            if (IsSetActiveOnMaidRoomKey(command, active))
                return true;
        }

        return false;
    }

    static bool IsSetActiveOnMaidRoomKey(string commandYaml, bool active)
    {
        if (!commandYaml.Contains($"guid: {SetActiveCommandScriptGuid}"))
            return false;

        if (!commandYaml.Contains($"gameObjectVal: {{fileID: {MaidRoomKeySceneFileId}}}"))
            return false;

        return commandYaml.Contains(active ? "booleanVal: 1" : "booleanVal: 0");
    }

    static bool IsAnimatorMoveTriggerOnMaidRoomKey(string commandYaml)
    {
        return commandYaml.Contains($"guid: {AnimatorSetTriggerScriptGuid}")
            && commandYaml.Contains("stringVal: MoveTrigger")
            && commandYaml.Contains($"animatorVal: {{fileID: 1172732412}}");
    }

    static int FindFirstCommandIndex(
        string sceneText,
        IReadOnlyList<string> commandFileIds,
        System.Func<string, bool> predicate)
    {
        for (int i = 0; i < commandFileIds.Count; i++)
        {
            string command = FindObjectBlock(sceneText, "114", commandFileIds[i]);
            if (predicate(command))
                return i;
        }

        return -1;
    }

    static List<string> ParseCommandListFileIds(string blockYaml)
    {
        Match commandListMatch = Regex.Match(blockYaml, @"commandList:\r?\n(?<items>(?:  - \{fileID: [0-9]+\}\r?\n)+)");
        Assert.IsTrue(commandListMatch.Success, "Could not parse addKey commandList.");

        return commandListMatch.Groups["items"].Value
            .Split('\n')
            .Select(line => Regex.Match(line, @"\{fileID: (?<id>[0-9]+)\}"))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToList();
    }

    static List<string> ParseChildFileIds(string rectTransformYaml)
    {
        Match childrenMatch = Regex.Match(rectTransformYaml, @"m_Children:\r?\n(?<items>(?:  - \{fileID: [0-9]+\}\r?\n)+)");
        Assert.IsTrue(childrenMatch.Success, "Could not parse RectTransform children.");

        return childrenMatch.Groups["items"].Value
            .Split('\n')
            .Select(line => Regex.Match(line, @"\{fileID: (?<id>[0-9]+)\}"))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToList();
    }

    static int ParseSortingOrder(string canvasYaml)
    {
        Match match = Regex.Match(canvasYaml, @"m_SortingOrder: (?<order>-?[0-9]+)");
        Assert.IsTrue(match.Success, "Could not parse Canvas sorting order.");
        return int.Parse(match.Groups["order"].Value);
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

    static string FindMonoBehaviourByScriptGuid(string sceneText, string scriptGuid)
    {
        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!114 &[0-9]+\r?\nMonoBehaviour:[\s\S]*?(?=--- !u!)",
            RegexOptions.Multiline))
        {
            if (match.Value.Contains($"guid: {scriptGuid}"))
                return match.Value;
        }

        Assert.Fail($"Could not find MonoBehaviour with script guid {scriptGuid}.");
        return string.Empty;
    }

    static string ReadKitchenSceneText()
    {
        return File.ReadAllText(Path.Combine(Application.dataPath, KitchenSceneRelativePath));
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
}
