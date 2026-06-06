using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class FungusDialogueBridgeTests
{
    GameObject root;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        FungusDialogueBridge.ResetForTests();
        root = new GameObject("FungusDialogueBridgeTestRoot");
        flowchart = root.AddComponent<Flowchart>();
    }

    [TearDown]
    public void TearDown()
    {
        FungusDialogueBridge.ResetForTests();
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void ExecuteBlockSafely_ActivatesInactiveFlowchartBeforeHandler()
    {
        root.SetActive(false);

        FungusDialogueBridge.ExecuteBlockHandlerForTests = (fc, _) =>
        {
            Assert.IsTrue(fc.gameObject.activeSelf);
            return true;
        };

        Assert.IsTrue(FungusDialogueBridge.ExecuteBlockSafely(flowchart, "AnyBlock"));
        Assert.IsTrue(root.activeSelf);
    }
}
