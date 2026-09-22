using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BasementHallwayInteractionControllerTests
{
    GameObject root;
    BasementHallwayInteractionController controller;

    [SetUp]
    public void SetUp()
    {
        RoomInteractionController.ResetStateForTests();
        GameplayScreenFade.FadeThenHandlerForTests = null;
        root = new GameObject("BasementHallwayTest");
        controller = root.AddComponent<BasementHallwayInteractionController>();
    }

    [TearDown]
    public void TearDown()
    {
        RoomInteractionController.ResetStateForTests();
        GameplayScreenFade.FadeThenHandlerForTests = null;
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void RequestSceneTransition_UsesFadeBeforeLoad()
    {
        float capturedAlpha = -1f;
        bool fadeCalled = false;
        GameplayScreenFade.FadeThenHandlerForTests = (alpha, duration, onComplete) =>
        {
            fadeCalled = true;
            capturedAlpha = alpha;
            onComplete?.Invoke();
        };

        var method = typeof(RoomInteractionController).GetMethod(
            "RequestSceneTransition",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Public);
        Assert.NotNull(method);
        var result = (bool)method.Invoke(controller, new object[] { "BasementBrickRoom" });

        Assert.IsTrue(result);
        Assert.IsTrue(fadeCalled);
        Assert.AreEqual(GameplayScreenFade.BasementDoorFadeTargetAlpha, capturedAlpha);
    }
}
