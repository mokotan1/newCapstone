using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[TestFixture]
public class QuestTrackerHudHostTests
{
    const string InventoryUnlockedPrefsKey = "InventoryAccess.UnlockedAfterHallPlayableRetry";

    Scene testScene;
    GameObject sceneCanvasObject;
    GameObject questTrackerCanvasObject;
    GameObject extraHudObject;

    [SetUp]
    public void SetUp()
    {
        PlayerPrefs.DeleteKey(InventoryUnlockedPrefsKey);
        PlayerPrefs.Save();
        testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TearDown]
    public void TearDown()
    {
        PlayerPrefs.DeleteKey(InventoryUnlockedPrefsKey);
        PlayerPrefs.Save();

        if (extraHudObject != null)
            Object.DestroyImmediate(extraHudObject);

        if (questTrackerCanvasObject != null)
            Object.DestroyImmediate(questTrackerCanvasObject);

        if (sceneCanvasObject != null)
            Object.DestroyImmediate(sceneCanvasObject);

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
    }

    [Test]
    public void ResolveCanvasParent_ReusesDedicatedQuestTrackerCanvas()
    {
        questTrackerCanvasObject = QuestTrackerHudHost.CreateOverlayCanvas();

        Transform parent = QuestTrackerHudHost.ResolveCanvasParent(() => questTrackerCanvasObject.GetComponent<Canvas>());

        Assert.AreSame(questTrackerCanvasObject.transform, parent);
    }

    [Test]
    public void ResolveCanvasParent_CreatesDedicatedOverlayCanvasWhenMissing()
    {
        sceneCanvasObject = CreateCanvasInActiveScene("SayDialogCanvas");

        Transform parent = QuestTrackerHudHost.ResolveCanvasParent(() => null);

        Assert.IsNotNull(parent);
        Assert.AreNotSame(sceneCanvasObject.transform, parent);
        var canvas = parent.GetComponent<Canvas>();
        Assert.IsNotNull(canvas);
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        Assert.IsTrue(canvas.overrideSorting);
        Assert.AreEqual(QuestTrackerHudHost.CanvasSortingOrder, canvas.sortingOrder);
        Assert.AreEqual(QuestTrackerHudHost.FallbackCanvasObjectName, parent.name);
    }

    [Test]
    public void DestroyExtraHudRoots_RemovesDuplicatesButKeepsManagedHud()
    {
        questTrackerCanvasObject = QuestTrackerHudHost.CreateOverlayCanvas();
        GameObject keepHud = CreateHudRoot(questTrackerCanvasObject.transform);
        extraHudObject = CreateHudRoot(questTrackerCanvasObject.transform);

        int destroyed = QuestTrackerHudHost.DestroyExtraHudRoots(testScene, keepHud);

        Assert.AreEqual(1, destroyed);
        Assert.AreEqual(1, QuestTrackerHudHost.FindHudRootsInScene(testScene).Count);
        Assert.AreSame(keepHud, QuestTrackerHudHost.FindHudRootsInScene(testScene).Single());
    }

    [Test]
    public void ShouldAttachHud_HidesOnMainMenuAndBeforeInventoryUnlock()
    {
        Assert.IsFalse(QuestTrackerHudHost.ShouldAttachHud(SceneNames.MainMenu));
        Assert.IsFalse(QuestTrackerHudHost.ShouldAttachHud(SceneNames.Kitchen));

        InventoryAccessState.Unlock();

        Assert.IsFalse(QuestTrackerHudHost.ShouldAttachHud(SceneNames.MainMenu));
        Assert.IsTrue(QuestTrackerHudHost.ShouldAttachHud(SceneNames.Kitchen));
    }

    static GameObject CreateCanvasInActiveScene(string objectName)
    {
        return new GameObject(objectName, typeof(RectTransform), typeof(Canvas));
    }

    static GameObject CreateHudRoot(Transform parent)
    {
        var hudObject = new GameObject(
            QuestTrackerHudFactory.RootObjectName,
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(QuestTrackerHudView));

        hudObject.transform.SetParent(parent, false);
        return hudObject;
    }
}
