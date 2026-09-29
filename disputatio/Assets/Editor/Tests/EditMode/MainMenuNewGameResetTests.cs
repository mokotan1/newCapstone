using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Fungus;
using Godlotto.Interaction;
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
    private List<string> transitionRequests;

    [SetUp]
    public void SetUp()
    {
        SceneTransitionService.ResetForTests();
        mainMenuObject = new GameObject("MainMenu");
        mainMenu = mainMenuObject.AddComponent<MainMenu>();
        dragItem = ScriptableObject.CreateInstance<Item>();
        saveResetRaised = false;
        transitionRequests = new List<string>();
        SetTransitionRequest(sceneName =>
        {
            transitionRequests.Add(sceneName);
            return true;
        });
        SaveManagerSignals.OnSaveReset += HandleSaveReset;
    }

    [TearDown]
    public void TearDown()
    {
        SceneTransitionService.ResetForTests();
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
    public void OnStartButton_ClearsInventoryWithoutFungusSaveReset()
    {
        InventorySlot.draggedItem = dragItem;
        SetPrivateStaticDragIcon(new GameObject("DragIcon"));

        GameObject createdInventory = null;
        if (InventoryManager.Instance == null)
        {
            createdInventory = new GameObject("InventoryManagerNewGameTest");
            InventoryManager added = createdInventory.AddComponent<InventoryManager>();
            if (InventoryManager.Instance == null)
                SetSingletonInstance(added);
        }

        InventoryManager inventory = InventoryManager.Instance;
        Assert.IsNotNull(inventory);
        inventory.ClearItemsForNewGame();
        Item carried = ScriptableObject.CreateInstance<Item>();
        carried.itemId = 4;
        carried.itemName = "NewGameResetItem";
        inventory.AddItem(carried);
        Assert.AreEqual(1, inventory.Items.Count);

        try
        {
            mainMenu.OnStartButton();

            Assert.IsFalse(saveResetRaised, "새 게임은 Fungus SaveReset 신호를 발행하지 않습니다.");
            Assert.AreEqual(0, inventory.Items.Count);
            Assert.IsNull(InventorySlot.draggedItem);
            Assert.IsNull(GetPrivateStaticDragIcon());
        }
        finally
        {
            Object.DestroyImmediate(carried);
            if (createdInventory != null)
                Object.DestroyImmediate(createdInventory);
        }
    }

    [Test]
    public void OnStartButton_RequestsIntroSceneOnceAfterProgressReset()
    {
        PlayerPrefs.SetInt(JunkKey, 99);
        PlayerPrefs.Save();
        InventorySlot.draggedItem = dragItem;
        SetTransitionRequest(sceneName =>
        {
            Assert.IsFalse(PlayerPrefs.HasKey(JunkKey), "Progress must be cleared before the scene transition.");
            Assert.IsNull(InventorySlot.draggedItem, "Drag state must be cleared before the scene transition.");
            transitionRequests.Add(sceneName);
            return true;
        });

        mainMenu.OnStartButton();
        mainMenu.OnStartButton();

        Assert.That(transitionRequests, Is.EqualTo(new[] { SceneNames.IntroScene }));
    }

    [Test]
    public void OnStartButton_AllowsRetryWhenTransitionIsRejected()
    {
        SetTransitionRequest(sceneName =>
        {
            transitionRequests.Add(sceneName);
            return transitionRequests.Count > 1;
        });

        mainMenu.OnStartButton();
        mainMenu.OnStartButton();
        mainMenu.OnStartButton();

        Assert.That(transitionRequests, Is.EqualTo(new[] { SceneNames.IntroScene, SceneNames.IntroScene }));
    }

    [Test]
    public void OnStartButton_DoesNotClearProgressWhileAnotherTransitionIsPending()
    {
        PlayerPrefs.SetInt(JunkKey, 99);
        PlayerPrefs.SetInt(LastBookPageKey, 7);
        PlayerPrefs.Save();
        InventorySlot.draggedItem = dragItem;
        SceneTransitionService.SetTransitionPendingForTests(true, "OtherScene");

        mainMenu.OnStartButton();

        Assert.That(PlayerPrefs.GetInt(JunkKey), Is.EqualTo(99));
        Assert.That(PlayerPrefs.GetInt(LastBookPageKey), Is.EqualTo(7));
        Assert.That(InventorySlot.draggedItem, Is.SameAs(dragItem));
        Assert.That(transitionRequests, Is.Empty);

        SceneTransitionService.SetTransitionPendingForTests(false);
        mainMenu.OnStartButton();

        Assert.That(PlayerPrefs.HasKey(JunkKey), Is.False);
        Assert.That(transitionRequests, Is.EqualTo(new[] { SceneNames.IntroScene }));
    }

    [Test]
    public void MainMenuScene_StartButton_InvokesOnlyOnStartButton()
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
        Assert.AreEqual(1, callEntries.Count, "StartButton must have one persistent scene-start handler.");

        string entry = callEntries[0].Value;
        Assert.That(entry, Does.Contain("m_MethodName: OnStartButton"));
        Assert.That(entry, Does.Contain("m_TargetAssemblyTypeName: MainMenu, Assembly-CSharp"));
    }

    private void SetTransitionRequest(System.Func<string, bool> request)
    {
        FieldInfo field = typeof(MainMenu).GetField(
            "requestSceneTransition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(mainMenu, request);
    }

    static void SetSingletonInstance(InventoryManager inventory)
    {
        FieldInfo field = typeof(SingletonMonoBehaviour<InventoryManager>).GetField(
            "_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field);
        field.SetValue(null, inventory);
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
            RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.IsTrue(match.Success, $"Could not find !u!{unityType} &{fileId}.");
        return match.Value;
    }
}
