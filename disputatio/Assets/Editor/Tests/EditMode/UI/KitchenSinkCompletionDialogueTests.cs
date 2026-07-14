using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Kitchen sink/faucet completion dialogue must gate on HaveMaidKey (key picked up),
/// not FaucetClicked (water already running). Also Kitchen chatbot must not override ServerConfig URL.
/// </summary>
[TestFixture]
public class KitchenSinkCompletionDialogueTests
{
    const string KitchenSceneRelativePath = "Scenes/Mokotan/First Floor/1foorLeft/Kitchen.unity";
    const string CompletionSayText = "더이상 볼 일은 없는 것 같다.";
    const string CompletionSayUnicodeEscape = "\\uB354\\uC774\\uC0C1 \\uBCFC \\uC77C\\uC740 \\uC5C6\\uB294 \\uAC83 \\uAC19\\uB2E4.";
    const string CompletionIfCommandFileId = "290854250";
    const string CompletionSayCommandFileId = "290854224";
    const string HaveMaidKeyVariableFileId = "290853994";
    const string FaucetClickedVariableFileId = "290854235";
    const string FungusBlockScriptGuid = "3d3d73aef2cfc4f51abf34ac00241f60";
    const string KitchenChatbotScriptGuid = "00647ada9aa5d9f469a07fbc68bb5425";

    [Test]
    public void CompletionSay_IsNestedUnderIfOnHaveMaidKey_NotFaucetClicked()
    {
        string sceneText = ReadKitchenSceneText();
        string sayCommand = FindObjectBlock(sceneText, "114", CompletionSayCommandFileId);
        string ifCommand = FindObjectBlock(sceneText, "114", CompletionIfCommandFileId);

        Assert.IsTrue(
            sayCommand.Contains($"storyText: \"{CompletionSayUnicodeEscape}\"")
            || sayCommand.Contains(CompletionSayText),
            "Completion Say command must contain the finished-dialogue text.");

        StringAssert.Contains(
            $"variable: {{fileID: {HaveMaidKeyVariableFileId}}}",
            ifCommand,
            "Final sink dialogue If must branch on HaveMaidKey.");
        StringAssert.DoesNotContain(
            $"variable: {{fileID: {FaucetClickedVariableFileId}}}",
            ifCommand,
            "Final sink dialogue If must not branch on FaucetClicked.");

        Assert.IsTrue(
            CommandListContainsAdjacentIfThenSay(sceneText),
            "Completion If and Say must appear together in a sink-flow block commandList.");
    }

    [Test]
    public void KitchenChatbot_LocalServerUrl_IsEmptySoServerConfigIsSoleSource()
    {
        string chatbotBlock = FindMonoBehaviourByScriptGuid(
            ReadKitchenSceneText(),
            KitchenChatbotScriptGuid);

        Match urlMatch = Regex.Match(chatbotBlock, @"localServerUrl: (?<url>.*)");
        Assert.IsTrue(urlMatch.Success, "KitchenChatbot must serialize localServerUrl.");
        Assert.AreEqual(
            string.Empty,
            urlMatch.Groups["url"].Value.Trim(),
            "KitchenChatbot.localServerUrl must be empty so ServerConfig is the sole URL source.");
    }

    static bool CommandListContainsAdjacentIfThenSay(string sceneText)
    {
        foreach (Match match in Regex.Matches(
            sceneText,
            @"--- !u!114 &[0-9]+\r?\nMonoBehaviour:(?:(?!^--- ).)*",
            RegexOptions.Multiline | RegexOptions.Singleline))
        {
            string block = match.Value;
            if (!block.Contains($"guid: {FungusBlockScriptGuid}"))
                continue;

            List<string> commandIds = ParseCommandListFileIds(block);
            for (int i = 0; i < commandIds.Count - 1; i++)
            {
                if (commandIds[i] == CompletionIfCommandFileId
                    && commandIds[i + 1] == CompletionSayCommandFileId)
                    return true;
            }
        }

        return false;
    }

    static List<string> ParseCommandListFileIds(string blockYaml)
    {
        Match commandListMatch = Regex.Match(
            blockYaml,
            @"commandList:\r?\n(?<items>(?:  - \{fileID: [0-9]+\}\r?\n)+)");
        if (!commandListMatch.Success)
            return new List<string>();

        return commandListMatch.Groups["items"].Value
            .Split('\n')
            .Select(line => Regex.Match(line, @"\{fileID: (?<id>[0-9]+)\}"))
            .Where(match => match.Success)
            .Select(match => match.Groups["id"].Value)
            .ToList();
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
