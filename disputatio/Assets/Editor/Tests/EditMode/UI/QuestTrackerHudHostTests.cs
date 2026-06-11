using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[TestFixture]
public class QuestTrackerHudHostTests
{
    Scene testScene;
    GameObject sceneCanvasObject;
    GameObject extraHudObject;

    [SetUp]
    public void SetUp()
    {
        testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [TearDown]
    public void TearDown()
    {
        if (extraHudObject != null)
            Object.DestroyImmediate(extraHudObject);

        if (sceneCanvasObject != null)
            Object.DestroyImmediate(sceneCanvasObject);

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
    }

    [Test]
    public void ResolveCanvasParent_UsesExistingSceneCanvas()
    {
        sceneCanvasObject = CreateCanvasInActiveScene("GameplayCanvas");

        Transform parent = QuestTrackerHudHost.ResolveCanvasParent(() => sceneCanvasObject.GetComponent<Canvas>());

        Assert.AreSame(sceneCanvasObject.transform, parent);
    }

    [Test]
    public void ResolveCanvasParent_CreatesOverlayCanvasWhenMissing()
    {
        Transform parent = QuestTrackerHudHost.ResolveCanvasParent(() => null);

        Assert.IsNotNull(parent);
        var canvas = parent.GetComponent<Canvas>();
        Assert.IsNotNull(canvas);
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
        Assert.AreEqual(QuestTrackerHudHost.FallbackCanvasObjectName, parent.name);
    }

    [Test]
    public void DestroyExtraHudRoots_RemovesDuplicatesButKeepsManagedHud()
    {
        sceneCanvasObject = CreateCanvasInActiveScene("GameplayCanvas");
        GameObject keepHud = CreateHudRoot(sceneCanvasObject.transform);
        extraHudObject = CreateHudRoot(sceneCanvasObject.transform);

        int destroyed = QuestTrackerHudHost.DestroyExtraHudRoots(testScene, keepHud);

        Assert.AreEqual(1, destroyed);
        Assert.AreEqual(1, QuestTrackerHudHost.FindHudRootsInScene(testScene).Count);
        Assert.AreSame(keepHud, QuestTrackerHudHost.FindHudRootsInScene(testScene).Single());
    }

    [Test]
    public void ShouldAttachHud_HidesOnMainMenuOnly()
    {
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
