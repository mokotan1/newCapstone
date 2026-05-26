using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class JumpscareSpawnerSceneTransformTests
{
    [Test]
    public void HallRight_UsesScreenshotGhostTransform()
    {
        bool hasOverride = JumpscareSpawner.TryGetSceneSpecificTriggerTransform(
            SceneNames.HallRight,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale);

        Assert.IsTrue(hasOverride);
        Assert.AreEqual(new Vector3(-15f, -260f, 13.331f), position);
        Assert.AreEqual(Quaternion.identity, rotation);
        Assert.AreEqual(new Vector3(50f, 40f, 1f), scale);
    }

    [Test]
    public void HallRight2_UsesScreenshotGhostTransform()
    {
        bool hasOverride = JumpscareSpawner.TryGetSceneSpecificTriggerTransform(
            "Hall_Right2",
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale);

        Assert.IsTrue(hasOverride);
        Assert.AreEqual(new Vector3(-15f, -260f, 13.331f), position);
        Assert.AreEqual(Quaternion.identity, rotation);
        Assert.AreEqual(new Vector3(50f, 40f, 1f), scale);
    }

    [Test]
    public void OtherScenes_DoNotUseScreenshotGhostTransform()
    {
        bool hasOverride = JumpscareSpawner.TryGetSceneSpecificTriggerTransform(
            "Hall_Left",
            out _,
            out _,
            out _);

        Assert.IsFalse(hasOverride);
    }
}
