using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MapPlayerLocationMarkerTests
{
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
}
