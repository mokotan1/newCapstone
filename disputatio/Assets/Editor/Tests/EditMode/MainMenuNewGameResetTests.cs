using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Fungus;
using NUnit.Framework;
using UnityEngine;

public class MainMenuNewGameResetTests
{
    const string JunkKey = "__MainMenuNewGameReset_Junk__";
    const string LastBookPageKey = "LastBookPage_TestBook";
    const string MainMenuSceneRelativePath = "Scenes/godlotto/MainMenuScene.unity";
    const string StartButtonObjectName = "StartButton";
    const string UnityButtonScriptGuid = "4e29b1a8efbd4b44bb3f3716e73f07ff";

    private GameObject mainMenuObject;
    private MainMenu mainMenu;
    private Item dragItem;
    private bool saveResetRaised;

    [SetUp]
    public void SetUp()
    {
        mainMenuObject = new GameObject("MainMenu");
        mainMenu = mainMenuObject.AddComponent<MainMenu>();
        dragItem = ScriptableObject.CreateInstance<Item>();
        saveResetRaised = false;
        SaveManagerSignals.OnSaveReset += HandleSaveReset;
    }

    [TearDown]
    public void TearDown()
    {
        SaveManagerSignals.OnSaveReset -= HandleSaveReset;
        InventorySlot.ClearDragState();

        PlayerPrefs.DeleteKey(JunkKey);
        PlayerPrefs.DeleteKey(LastBookPageKey);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.BgmVolume);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.SfxVolume);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.Fullscreen);
        PlayerPrefs.DeleteKey(SettingPlayerPrefsKeys.ResolutionIndex);
        PlayerPrefs.Save();

        if (dragItem != null)
            Object.DestroyImmediate(dragItem);
        if (mainMenuObject != null)
            Object.DestroyImmediate(mainMenuObject);
    }

    [Test]
    public void OnStartButton_ClearsProgressAndPreservesAudioVideoSettings()
    {
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.BgmVolume, 0.42f);
        PlayerPrefs.SetFloat(SettingPlayerPrefsKeys.SfxVolume, 0.55f);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.Fullscreen, 0);
        PlayerPrefs.SetInt(SettingPlayerPrefsKeys.ResolutionIndex, 3);
        PlayerPrefs.SetInt(JunkKey, 99);
        PlayerPrefs.SetInt(LastBookPageKey, 7);
        PlayerPrefs.Save();

        mainMenu.OnStartButton();

        Assert.That(PlayerPrefs.HasKey(JunkKey), Is.False);
        Assert.That(PlayerPrefs.HasKey(LastBookPageKey), Is.False);
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.BgmVolume), Is.EqualTo(0.42f).Within(0.001f));
        Assert.That(PlayerPrefs.GetFloat(SettingPlayerPrefsKeys.SfxVolume), Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(PlayerPrefs.GetInt(SettingPlayerPrefsKeys.Fullscreen), Is.EqualTo(0));
        Assert.That(PlayerPrefs.GetInt(SettingPlayerPrefsKeys.ResolutionIndex), Is.EqualTo(3));
    }

    [Test]
    public void OnStartButton_RaisesSaveResetAndClearsInventoryDragState()
    {
        InventorySlot.draggedItem = dragItem;
        SetPrivateStaticDragIcon(new GameObject("DragIcon"));

        mainMenu.OnStartButton();

        Assert.IsTrue(saveResetRaised, "새 게임 시작 시 Fungus SaveReset 신호가 발행되어야 합니다.");
        Assert.IsNull(InventorySlot.draggedItem);
        Assert.IsNull(GetPrivateStaticDragIcon());
    }

    [Test]
    public void MainMenuScene_StartButton_InvokesOnStartButtonBeforeFlowchartStartButton()
    {
        string sceneText = ReadMainMenuSceneText();
        string startButtonObject = FindGameObjectBlock(sceneText, StartButtonObjectName);
        string buttonComponent = FindComponentBlockOnGameObject(
            sceneText,
            startButtonObject,
            "114",
            UnityButtonScriptGuid);

        Match callsMatch = Regex.Match(
            buttonComponent,
            @"m_OnClick:\r?\n\s*m_PersistentCalls:\r?\n\s*m_Calls:\r?\n(?<calls>(?:      - m_Target:[\s\S]*?)(?=\r?\n--- !u!|\z))",
            RegexOptions.Multiline);
        Assert.IsTrue(callsMatch.Success, "StartButton Button must declare m_OnClick.m_PersistentCalls.m_Calls.");

        string callsYaml = callsMatch.Groups["calls"].Value;
        MatchCollection callEntries = Regex.Matches(
            callsYaml,
            @"- m_Target:[\s\S]*?(?=\r?\n      - m_Target:|\z)",
            RegexOptions.Multiline);
        Assert.GreaterOrEqual(
            callEntries.Count,
            2,
            "StartButton must wire both MainMenu.OnStartButton and Flowchart.ExecuteBlock.");

        int onStartIndex = -1;
        int executeBlockIndex = -1;
        for (int i = 0; i < callEntries.Count; i++)
        {
            string entry = callEntries[i].Value;
            if (entry.Contains("m_MethodName: OnStartButton")
                && entry.Contains("m_TargetAssemblyTypeName: MainMenu, Assembly-CSharp"))
            {
                onStartIndex = i;
            }

            if (entry.Contains("m_MethodName: ExecuteBlock")
                && entry.Contains("m_TargetAssemblyTypeName: Fungus.Flowchart, Fungus")
                && entry.Contains("m_StringArgument: StartButton"))
            {
                executeBlockIndex = i;
            }
        }

        Assert.GreaterOrEqual(onStartIndex, 0, "StartButton must invoke MainMenu.OnStartButton.");
        Assert.GreaterOrEqual(
            executeBlockIndex,
            0,
            "StartButton must invoke Flowchart.ExecuteBlock(\"StartButton\").");
        Assert.Less(
            onStartIndex,
            executeBlockIndex,
            "MainMenu.OnStartButton must run before Flowchart.ExecuteBlock(\"StartButton\") so prefs/runtime reset completes before the opening scene loads.");
    }

    private void HandleSaveReset()
    {
        saveResetRaised = true;
    }

    private static void SetPrivateStaticDragIcon(GameObject value)
    {
        GetDragIconField().SetValue(null, value);
    }

    private static GameObject GetPrivateStaticDragIcon()
    {
        return (GameObject)GetDragIconField().GetValue(null);
    }

    private static FieldInfo GetDragIconField()
    {
        return typeof(InventorySlot).GetField("dragIcon", BindingFlags.NonPublic | BindingFlags.Static);
    }

    static string ReadMainMenuSceneText()
    {
        return File.ReadAllText(Path.Combine(Application.dataPath, MainMenuSceneRelativePath));
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

    static string FindComponentBlockOnGameObject(
        string sceneText,
        string gameObjectBlock,
        string unityType,
        string scriptGuid)
    {
        foreach (Match match in Regex.Matches(gameObjectBlock, @"- component: \{fileID: (?<id>[0-9]+)\}"))
        {
            string fileId = match.Groups["id"].Value;
            if (!Regex.IsMatch(sceneText, $@"--- !u!{Regex.Escape(unityType)} &{Regex.Escape(fileId)}\r?\n"))
                continue;

            string block = FindObjectBlock(sceneText, unityType, fileId);
            if (!block.Contains($"guid: {scriptGuid}"))
                continue;

            return block;
        }

        Assert.Fail($"Could not find component !u!{unityType} guid '{scriptGuid}' on GameObject.");
        return string.Empty;
    }

    static string FindObjectBlock(string sceneText, string unityType, string fileId)
    {
        Match match = Regex.Match(
            sceneText,
            $@"--- !u!{Regex.Escape(unityType)} &{Regex.Escape(fileId)}\r?\n(?:(?!^--- ).)*",
            RegexOptions.Multiline);
        Assert.IsTrue(match.Success, $"Could not find !u!{unityType} &{fileId}.");
        return match.Value;
    }
}
