using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class FungusBlockProgressTests
{
    GameObject root;
    Block block;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("FungusBlockProgressTestRoot");
        root.AddComponent<Flowchart>();
        block = root.AddComponent<Block>();
        block.BlockName = "Progress";
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void LeanTween_Init_RecreatesHostAfterItWasDestroyed()
    {
        LeanTween.init();
        GameObject host = LeanTween.tweenEmpty;
        Assert.IsNotNull(host);

        Object.DestroyImmediate(host);

        LeanTween.init();
        Assert.IsNotNull(LeanTween.tweenEmpty);
        Assert.IsNotNull(LeanTween.value(0f, 1f, 0.05f));

        if (LeanTween.tweenEmpty != null)
            Object.DestroyImmediate(LeanTween.tweenEmpty);
    }

    [Test]
    public void InvokeMethod_OnEnter_WhenTargetMethodMissing_ContinuesBlock()
    {
        InvokeMethod command = root.AddComponent<InvokeMethod>();
        command.ParentBlock = block;
        command.IsExecuting = true;
        SetPrivateField(command, "targetObject", root);
        SetPrivateField(command, "targetComponentAssemblyName", "MissingComponent, Assembly-CSharp");
        SetPrivateField(command, "targetMethod", "MissingMethod");

        command.OnEnter();

        Assert.AreEqual(1, ReadJump(block));
    }

    static int ReadJump(Block target)
    {
        FieldInfo field = typeof(Block).GetField(
            "jumpToCommandIndex",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return (int)field.GetValue(target);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }
}
