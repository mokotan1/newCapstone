using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Reflection;

[TestFixture]
public class MapPlayerLocationMarkerTests
{
    private Scene previousScene;
    private Scene testScene;
    private GameObject mapRoot;

    [TearDown]
    public void TearDown()
    {
        if (mapRoot != null)
            Object.DestroyImmediate(mapRoot);

        if (previousScene.IsValid())
            SceneManager.SetActiveScene(previousScene);

        if (testScene.IsValid())
            SceneManager.UnloadSceneAsync(testScene);
    }

    [Test]
    public void MainSceneMarker_IsCenteredInFirstFloorHall()
    {
        Assert.IsTrue(MapPlayerLocationMarker.TryGetLocationForScene(SceneNames.MainScene, out int floor, out Vector2 position));
        Assert.AreEqual(1, floor);
        Assert.AreEqual(Vector2.zero, position);
    }

    [Test]
    public void KitchenMarker_RemainsOnKitchenRoom()
    {
        Assert.IsTrue(MapPlayerLocationMarker.TryGetLocationForScene(SceneNames.Kitchen, out int floor, out Vector2 position));
        Assert.AreEqual(1, floor);
        Assert.AreEqual(new Vector2(-321f, 293.42548f), position);
    }

    [Test]
    public void Refresh_OnSecondFloorScene_SyncsFloorNavigationButtons()
    {
        previousScene = SceneManager.GetActiveScene();
        testScene = SceneManager.CreateScene("2floorMainHall");
        SceneManager.SetActiveScene(testScene);

        mapRoot = new GameObject("MapRoot");
        var firstFloor = new GameObject("1Floor");
        var secondFloor = new GameObject("2Floor");
        var upButton = new GameObject("UpButton");
        var downButton = new GameObject("DownButton");

        firstFloor.transform.SetParent(mapRoot.transform, false);
        secondFloor.transform.SetParent(mapRoot.transform, false);
        upButton.transform.SetParent(mapRoot.transform, false);
        downButton.transform.SetParent(mapRoot.transform, false);

        upButton.SetActive(true);
        downButton.SetActive(false);

        ControlFloor controlFloor = mapRoot.AddComponent<ControlFloor>();
        SetPrivateField(controlFloor, "floor1Object", firstFloor);
        SetPrivateField(controlFloor, "floor2Object", secondFloor);
        SetPrivateField(controlFloor, "up", upButton);
        SetPrivateField(controlFloor, "down", downButton);

        MapPlayerLocationMarker marker = mapRoot.AddComponent<MapPlayerLocationMarker>();

        marker.Refresh();

        Assert.IsFalse(upButton.activeSelf);
        Assert.IsTrue(downButton.activeSelf);
    }

    [Test]
    public void ShouldBlockSceneLoad_ReturnsTrue_WhenTargetIsCurrentScene()
    {
        Assert.IsTrue(MapSceneNavigationGuard.ShouldBlockSceneLoad(SceneNames.Kitchen, SceneNames.Kitchen));
    }

    [Test]
    public void ShouldBlockSceneLoad_ReturnsFalse_WhenTargetIsDifferentScene()
    {
        Assert.IsFalse(MapSceneNavigationGuard.ShouldBlockSceneLoad(SceneNames.Kitchen, SceneNames.MaidRoom));
    }

    [Test]
    public void ShouldBlockSceneLoad_ReturnsFalse_WhenTargetIsEmpty()
    {
        Assert.IsFalse(MapSceneNavigationGuard.ShouldBlockSceneLoad(SceneNames.Kitchen, ""));
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
