using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Fungus;

/// <summary>
/// Proves every return-to-menu entry point (EndSceneManager / IntegratedSettingUI /
/// SettingPanelButtonActions) wipes DontDestroyOnLoad Fungus globals and quest
/// tracker roots using the exact same preservation policy as
/// <see cref="InGameSettingsPanel"/> (see
/// <see cref="InGameSettingsPanelCleanupPolicyTests"/>), so a second New Game
/// after BetaEnd/settings-menu return cannot inherit stale state.
/// </summary>
public class ReturnToMenuDdolCleanupTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in spawned)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }
        spawned.Clear();
    }

    private GameObject Spawn(string name)
    {
        var go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private GameObject SpawnGlobalSettingManagerRoot()
    {
        var go = Spawn("GlobalSettingManager");
        go.SetActive(false);
        go.AddComponent<GlobalSettingManager>();
        return go;
    }

    private GameObject SpawnGlobalVariablesRoot()
    {
        var go = Spawn("GlobalVariables");
        go.SetActive(false);
        go.AddComponent<GlobalVariables>();
        return go;
    }

    private GameObject SpawnQuestTrackerRoot()
    {
        return Spawn("QuestTrackerSystems");
    }

    [Test]
    public void EndSceneManager_CleanupDontDestroyGameplayRoots_DestroysFungusGlobalsAndQuestTracker()
    {
        var selfRoot = Spawn("EndSceneManagerRoot");
        var endSceneManager = selfRoot.AddComponent<EndSceneManager>();

        var globalSettings = SpawnGlobalSettingManagerRoot();
        var globalVariables = SpawnGlobalVariablesRoot();
        var questTracker = SpawnQuestTrackerRoot();
        var roots = new List<GameObject> { selfRoot, globalSettings, globalVariables, questTracker };
        var destroyed = new List<GameObject>();

        endSceneManager.CleanupDontDestroyGameplayRoots(roots, destroyed.Add);

        Assert.IsTrue(destroyed.Contains(globalVariables), "Fungus GlobalVariables DDOL root must be wiped when leaving BetaEnd to the main menu.");
        Assert.IsTrue(destroyed.Contains(questTracker), "Quest tracker DDOL root must be wiped when leaving BetaEnd to the main menu.");
        Assert.IsFalse(destroyed.Contains(globalSettings), "GlobalSettingManager (BGM/SFX/Fullscreen/Resolution) must be preserved.");
        Assert.IsFalse(destroyed.Contains(selfRoot), "The caller's own root must not be destroyed mid-transition.");
    }

    [Test]
    public void IntegratedSettingUI_CleanupDontDestroyGameplayRoots_DestroysFungusGlobalsAndQuestTracker()
    {
        var selfRoot = Spawn("IntegratedSettingUIRoot");
        var settingUi = selfRoot.AddComponent<IntegratedSettingUI>();

        var globalSettings = SpawnGlobalSettingManagerRoot();
        var globalVariables = SpawnGlobalVariablesRoot();
        var questTracker = SpawnQuestTrackerRoot();
        var roots = new List<GameObject> { selfRoot, globalSettings, globalVariables, questTracker };
        var destroyed = new List<GameObject>();

        settingUi.CleanupDontDestroyGameplayRoots(roots, destroyed.Add);

        Assert.IsTrue(destroyed.Contains(globalVariables), "Fungus GlobalVariables DDOL root must be wiped on IntegratedSettingUI return-to-menu.");
        Assert.IsTrue(destroyed.Contains(questTracker), "Quest tracker DDOL root must be wiped on IntegratedSettingUI return-to-menu.");
        Assert.IsFalse(destroyed.Contains(globalSettings), "GlobalSettingManager (BGM/SFX/Fullscreen/Resolution) must be preserved.");
        Assert.IsFalse(destroyed.Contains(selfRoot), "The caller's own root must not be destroyed mid-transition.");
    }

    [Test]
    public void SettingPanelButtonActions_CleanupDontDestroyGameplayRoots_DestroysFungusGlobalsAndQuestTracker()
    {
        var selfRoot = Spawn("SettingPanelButtonActionsRoot");
        var buttonActions = selfRoot.AddComponent<SettingPanelButtonActions>();

        var globalSettings = SpawnGlobalSettingManagerRoot();
        var globalVariables = SpawnGlobalVariablesRoot();
        var questTracker = SpawnQuestTrackerRoot();
        var roots = new List<GameObject> { selfRoot, globalSettings, globalVariables, questTracker };
        var destroyed = new List<GameObject>();

        buttonActions.CleanupDontDestroyGameplayRoots(roots, destroyed.Add);

        Assert.IsTrue(destroyed.Contains(globalVariables), "Fungus GlobalVariables DDOL root must be wiped on SettingPanelButtonActions return-to-menu.");
        Assert.IsTrue(destroyed.Contains(questTracker), "Quest tracker DDOL root must be wiped on SettingPanelButtonActions return-to-menu.");
        Assert.IsFalse(destroyed.Contains(globalSettings), "GlobalSettingManager (BGM/SFX/Fullscreen/Resolution) must be preserved.");
        Assert.IsFalse(destroyed.Contains(selfRoot), "The caller's own root must not be destroyed mid-transition.");
    }
}
