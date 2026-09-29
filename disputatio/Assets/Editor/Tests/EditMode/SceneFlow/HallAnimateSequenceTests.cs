using System.Collections.Generic;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class HallAnimateSequenceTests
{
    GameObject root;
    HallAnimateSequence sequence;
    Transform parrot;
    Transform destination;
    readonly List<(float alpha, float duration)> fades = new List<(float, float)>();
    string loadedScene;
    int playedSfx = -1;

    [SetUp]
    public void SetUp()
    {
        SceneRouteState.ResetForTests();
        GameplayScreenFade.FadeThenHandlerForTests = (alpha, duration, onComplete) =>
        {
            fades.Add((alpha, duration));
            onComplete?.Invoke();
        };
        HallAnimateSequence.PlaySfxHandlerForTests = index => playedSfx = index;
        HallAnimateSequence.LoadSceneHandlerForTests = scene => loadedScene = scene;

        root = new GameObject("HallAnimateSequenceTests");
        parrot = new GameObject("Parret_Animate").transform;
        parrot.SetParent(root.transform);
        parrot.position = Vector3.zero;
        destination = new GameObject("destination").transform;
        destination.SetParent(root.transform);
        destination.position = new Vector3(10f, 0f, 0f);

        sequence = root.AddComponent<HallAnimateSequence>();
        SerializedObject so = new SerializedObject(sequence);
        so.FindProperty("parrot").objectReferenceValue = parrot;
        so.FindProperty("destination").objectReferenceValue = destination;
        so.FindProperty("moveDurationSeconds").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        GameplayScreenFade.FadeThenHandlerForTests = null;
        HallAnimateSequence.PlaySfxHandlerForTests = null;
        HallAnimateSequence.LoadSceneHandlerForTests = null;
        SceneRouteState.ResetForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void Play_FadesMovesSetsPreviousAndLoadsHallPlayable()
    {
        sequence.RunSynchronouslyForTests();

        CollectionAssert.AreEqual(
            new[] { (0f, 1f), (1f, 0.25f) },
            fades);
        Assert.AreEqual(9, playedSfx);
        Assert.AreEqual(destination.position, parrot.position);
        Assert.IsTrue(SceneRouteState.TryGetPreviousScene(out string previous));
        Assert.AreEqual(SceneNames.HallAnimate, previous);
        Assert.AreEqual(SceneNames.HallPlayable, loadedScene);
    }
}
