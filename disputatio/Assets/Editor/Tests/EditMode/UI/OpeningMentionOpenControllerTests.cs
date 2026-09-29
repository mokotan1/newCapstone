using System.Collections;
using System.Collections.Generic;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class OpeningMentionOpenControllerTests
{
    GameObject root;
    OpeningMentionOpenController controller;
    readonly List<(float alpha, float duration)> fades = new List<(float, float)>();
    string loaded;
    int playedSfx = -1;

    [SetUp]
    public void SetUp()
    {
        OpeningMentionOpenController.ResetStateForTests();
        SceneInteractionController.RespectLegacyInteractionLock = false;
        SceneInteractionController.BlockDuringFungusDialogue = false;
        SceneInteractionController.BlockDuringSceneTransition = false;
        fades.Clear();
        loaded = null;
        playedSfx = -1;

        GameplayScreenFade.FadeThenHandlerForTests = (alpha, duration, onComplete) =>
        {
            fades.Add((alpha, duration));
            onComplete?.Invoke();
        };
        OpeningMentionOpenController.PlaySfxHandlerForTests = index => playedSfx = index;
        OpeningMentionOpenController.SceneLoadHandlerForTests = scene =>
        {
            loaded = scene;
            return true;
        };
        OpeningMentionOpenController.SequenceRunnerForTests = sequence =>
        {
            while (sequence.MoveNext())
            {
                // Drain WaitForSecondsRealtime / yield null immediately for EditMode.
            }
        };

        root = new GameObject("OpeningMentionOpenTests");
        controller = root.AddComponent<OpeningMentionOpenController>();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayScreenFade.FadeThenHandlerForTests = null;
        OpeningMentionOpenController.ResetStateForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void OnDoorClicked_WhileBusy_PlaysSfxButDoesNotLoad()
    {
        controller.SimulateBusyForTests(true);
        controller.OnDoorClicked();

        Assert.AreEqual(7, playedSfx);
        Assert.IsNull(loaded);
        CollectionAssert.IsEmpty(fades);
    }

    [Test]
    public void OnDoorClicked_WhenIdle_FadesAndLoadsHallAnimate_WithoutIsCall()
    {
        controller.SimulateIsCallForTests(false);
        controller.SimulateBusyForTests(false);

        controller.OnDoorClicked();

        Assert.AreEqual(7, playedSfx);
        CollectionAssert.AreEqual(new[] { (1f, 0.5f) }, fades);
        Assert.AreEqual(SceneNames.HallAnimate, loaded);
        Assert.IsFalse(controller.IsCallForTests);
    }

    [Test]
    public void OnBellClicked_WhileBusy_IsIgnored()
    {
        controller.SimulateBusyForTests(true);
        controller.OnBellClicked();
        Assert.IsTrue(controller.IsBusyForTests);
        Assert.IsFalse(controller.IsCallForTests);
        Assert.AreEqual(-1, playedSfx);
    }
}
