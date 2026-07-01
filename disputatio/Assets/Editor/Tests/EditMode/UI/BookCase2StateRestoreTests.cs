using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

[TestFixture]
public class BookCase2StateRestoreTests
{
    const string BookCase2ScenePath = "Assets/Scenes/Mokotan/First Floor/1floorRight/BookCase2.unity";
    const string StartBlockName = "Start";
    const string BlueMidButtonFileId = "2143532138";
    const string PrisonButtonFileId = "823372011";
    const string ButtonClickedVariableFileId = "687597805";
    const string IfScriptGuid = "70c5622b8a80845c980954170295f292";
    const string ElseScriptGuid = "3fa968f01a7f9496bb50e13dfe16760d";
    const string EndScriptGuid = "93cb9773f2ca04e2bbf7a68ccfc23267";
    const string SetActiveScriptGuid = "dbd8c931f22994b9d90e2037fffaa770";
    const string SetInteractableScriptGuid = "ab0d0ed4a2ca94c81a230a0ecce6e6e4";

    [Test]
    public void StartBlock_RestoresPrisonButtonWhenBlueMidButtonWasAlreadyClicked()
    {
        string sceneText = File.ReadAllText(BookCase2ScenePath);
        string startBlock = FindBlockByName(sceneText, StartBlockName);
        List<string> commandIds = ParseCommandListFileIds(startBlock);

        int ifIndex = FindCommandIndex(
            sceneText,
            commandIds,
            command => IsButtonClickedFalseIf(command));
        int elseIndex = FindCommandIndexAfter(
            sceneText,
            commandIds,
            ifIndex,
            command => HasScriptGuid(command, ElseScriptGuid));
        int endIndex = FindCommandIndexAfter(
            sceneText,
            commandIds,
            elseIndex,
            command => HasScriptGuid(command, EndScriptGuid));

        Assert.GreaterOrEqual(ifIndex, 0, "Start must branch on ButtonClicked == false.");
        Assert.GreaterOrEqual(elseIndex, 0, "Start must have an else branch for ButtonClicked == true.");
        Assert.Greater(endIndex, elseIndex, "ButtonClicked true branch must end before normal startup continues.");

        List<string> clickedBranchCommands = commandIds
            .Skip(elseIndex + 1)
            .Take(endIndex - elseIndex - 1)
            .Select(id => FindObjectBlock(sceneText, "114", id))
            .ToList();

        Assert.IsTrue(
            clickedBranchCommands.Any(command => IsSetActive(command, PrisonButtonFileId, active: true)),
            "Re-entering after BlueMidButton was clicked must show PrisonButton.");
        Assert.IsTrue(
            clickedBranchCommands.Any(command => IsSetActive(command, BlueMidButtonFileId, active: false)),
            "Re-entering after BlueMidButton was clicked must keep BlueMidButton hidden.");
        Assert.IsTrue(
            clickedBranchCommands.Any(command => IsSetInteractable(command, PrisonButtonFileId, interactable: true)),
            "Restored PrisonButton must be clickable.");
    }

    static bool IsButtonClickedFalseIf(string command)
    {
        return HasScriptGuid(command, IfScriptGuid)
            && command.Contains($"variable: {{fileID: {ButtonClickedVariableFileId}}}")
            && command.Contains("booleanVal: 0");
    }

    static bool IsSetActive(string command, string targetFileId, bool active)
    {
        return HasScriptGuid(command, SetActiveScriptGuid)
            && command.Contains($"gameObjectVal: {{fileID: {targetFileId}}}")
            && command.Contains($"booleanVal: {(active ? 1 : 0)}");
    }

    static bool IsSetInteractable(string command, string targetFileId, bool interactable)
    {
        return HasScriptGuid(command, SetInteractableScriptGuid)
            && command.Contains($"- {{fileID: {targetFileId}}}")
            && command.Contains($"booleanVal: {(interactable ? 1 : 0)}");
    }

    static bool HasScriptGuid(string command, string guid)
    {
        return command.Contains($"guid: {guid}");
    }

    static int FindCommandIndex(string sceneText, List<string> commandIds, System.Func<string, bool> predicate)
    {
        return FindCommandIndexAfter(sceneText, commandIds, -1, predicate);
    }

    static int FindCommandIndexAfter(
        string sceneText,
        List<string> commandIds,
        int startIndex,
        System.Func<string, bool> predicate)
    {
        for (int i = startIndex + 1; i < commandIds.Count; i++)
        {
            string command = FindObjectBlock(sceneText, "114", commandIds[i]);
            if (predicate(command))
                return i;
        }

        return -1;
    }

    static string FindBlockByName(string sceneText, string blockName)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!114 &[0-9]+\r?\nMonoBehaviour:\r?\n[\s\S]*?blockName: {Regex.Escape(blockName)}\r?\n[\s\S]*?(?=--- !u!114 &|\Z)",
            RegexOptions.Multiline);

        Assert.IsTrue(match.Success, $"Could not find Fungus block '{blockName}'.");
        return match.Value;
    }

    static List<string> ParseCommandListFileIds(string blockYaml)
    {
        Match match = Regex.Match(blockYaml, @"commandList:\r?\n(?<items>(?:  - \{fileID: [0-9]+\}\r?\n)+)");
        Assert.IsTrue(match.Success, "Block must contain a commandList.");

        return Regex.Matches(match.Groups["items"].Value, @"\{fileID: (?<id>[0-9]+)\}")
            .Cast<Match>()
            .Select(item => item.Groups["id"].Value)
            .ToList();
    }

    static string FindObjectBlock(string sceneText, string unityType, string fileId)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!{Regex.Escape(unityType)} &{Regex.Escape(fileId)}\r?\n[\s\S]*?(?=--- !u!|\Z)",
            RegexOptions.Multiline);

        Assert.IsTrue(match.Success, $"Could not find object block {unityType} &{fileId}.");
        return match.Value;
    }
}
