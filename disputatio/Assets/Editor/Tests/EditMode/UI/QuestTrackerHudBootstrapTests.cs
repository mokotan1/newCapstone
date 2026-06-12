using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[TestFixture]
public class QuestTrackerHudBootstrapTests
{
    const string MainMenuScenePath = "Assets/Scenes/godlotto/MainMenuScene.unity";
    const string InventoryUnlockedPrefsKey = "InventoryAccess.UnlockedAfterHallPlayableRetry";

    GameObject systemsObject;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(InventoryUnlockedPrefsKey);
        PlayerPrefs.Save();
        QuestTrackerHudController.ResetInstanceForTests();
        QuestTrackerHudBootstrap.ResetForTests();
        TutorialQuestCatalog.ResetCacheForTest();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(InventoryUnlockedPrefsKey);
        PlayerPrefs.Save();
        QuestTrackerHudBootstrap.ResetForTests();
        QuestTrackerHudController.ResetInstanceForTests();

        if (systemsObject != null)
            Object.DestroyImmediate(systemsObject);

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
    }

    [Test]
    public void AttachHudToActiveScene_ReusesStateAfterHudDestroyed()
    {
        new GameObject("SayDialogCanvas", typeof(RectTransform), typeof(Canvas));
        InventoryAccessState.Unlock();

        systemsObject = new GameObject("TutorialQuestSystems");
        var controller = systemsObject.AddComponent<QuestTrackerHudController>();
        controller.InitializeFromTutorialCatalog();

        Scene gameplayScene = SceneManager.GetActiveScene();
        controller.AttachHudToScene(gameplayScene);
        controller.PresentQuest(TutorialQuestIds.LightTheManor, playIntro: false);
        Assert.IsNotNull(controller.HudView);

        Transform questCanvas = controller.HudView.transform.parent;
        Assert.IsNotNull(questCanvas);
        Assert.AreEqual(QuestTrackerHudHost.FallbackCanvasObjectName, questCanvas.name);

        Object.DestroyImmediate(controller.HudView.gameObject);
        controller.AttachHudToScene(gameplayScene);

        Assert.AreEqual(TutorialQuestIds.LightTheManor, controller.TrackerState.CurrentQuestId);
        Assert.IsNotNull(controller.HudView);
        Assert.AreSame(questCanvas, controller.HudView.transform.parent);
        Assert.AreEqual(1, QuestTrackerHudHost.FindHudRootsInScene(gameplayScene).Count);
    }

    [Test]
    public void AttachHudToActiveScene_DoesNotCreateHudOnMainMenuScene()
    {
        systemsObject = new GameObject("TutorialQuestSystems");
        var controller = systemsObject.AddComponent<QuestTrackerHudController>();
        controller.InitializeFromTutorialCatalog();

        Scene mainMenuScene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        controller.AttachHudToScene(mainMenuScene);

        Assert.IsNull(controller.HudView);
        Assert.AreEqual(0, QuestTrackerHudHost.FindHudRootsInScene(mainMenuScene).Count);
    }
}
