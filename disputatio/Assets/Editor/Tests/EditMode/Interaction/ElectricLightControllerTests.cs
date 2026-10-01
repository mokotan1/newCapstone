using System.Collections.Generic;
using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[TestFixture]
public sealed class ElectricLightControllerTests
{
    readonly List<GameObject> owned = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        HallGlobalStateHost.ResetForTests();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            if (owned[i] != null)
                Object.DestroyImmediate(owned[i]);
        }

        owned.Clear();
        HallGlobalStateHost.ResetForTests();
    }

    [Test]
    public void NullFlowchart_UsesHallGlobalElectricOn()
    {
        HallGlobalStateHost host = HallGlobalStateHost.EnsureInstance();
        host.SetBool(FungusVariableKeys.ElectricOn, true);
        ElectricLightController controller = CreateController(out Light2D light, out SpriteRenderer sprite);
        controller.targetFlowchart = null;

        Invoke(controller, "Start");
        Assert.IsTrue(light.enabled);
        Assert.IsTrue(sprite.enabled);

        host.SetBool(FungusVariableKeys.ElectricOn, false);
        Invoke(controller, "Update");
        Assert.IsFalse(light.enabled);
        Assert.IsFalse(sprite.enabled);
    }

    [Test]
    public void LegacyFlowchart_RemainsSupported()
    {
        GameObject flowchartObject = Track(new GameObject("LegacyElectricFlowchart"));
        Flowchart flowchart = flowchartObject.AddComponent<Flowchart>();
        BooleanVariable variable = flowchartObject.AddComponent<BooleanVariable>();
        variable.Key = FungusVariableKeys.ElectricOn;
        variable.Value = true;
        flowchart.Variables.Add(variable);

        ElectricLightController controller = CreateController(out Light2D light, out SpriteRenderer sprite);
        controller.targetFlowchart = flowchart;
        controller.variableName = FungusVariableKeys.ElectricOn;

        Invoke(controller, "Start");
        Assert.IsTrue(light.enabled);
        Assert.IsTrue(sprite.enabled);

        variable.Value = false;
        Invoke(controller, "Update");
        Assert.IsFalse(light.enabled);
        Assert.IsFalse(sprite.enabled);
    }

    ElectricLightController CreateController(out Light2D light, out SpriteRenderer sprite)
    {
        GameObject host = Track(new GameObject("ElectricLight"));
        light = host.AddComponent<Light2D>();
        light.enabled = false;
        sprite = host.AddComponent<SpriteRenderer>();
        sprite.enabled = false;
        ElectricLightController controller = host.AddComponent<ElectricLightController>();
        controller.targetLights = new List<Light2D> { light };
        controller.targetSprites = new List<SpriteRenderer> { sprite };
        return controller;
    }

    GameObject Track(GameObject gameObject)
    {
        owned.Add(gameObject);
        return gameObject;
    }

    static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName);
        method.Invoke(target, null);
    }
}
