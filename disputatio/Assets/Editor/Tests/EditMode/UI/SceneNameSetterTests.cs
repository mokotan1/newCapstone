using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;

public class SceneNameSetterTests
{
    GameObject variableManager;
    GameObject setterObject;
    Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        variableManager = new GameObject("Variablemanager");
        flowchart = variableManager.AddComponent<Flowchart>();
        AddString(flowchart, "SceneName", "old-scene");
        AddString(flowchart, "SavePointKey", "leave-this");

        setterObject = new GameObject("SceneNameSetterTest");
        setterObject.AddComponent<SceneNameSetter>();
    }

    [TearDown]
    public void TearDown()
    {
        if (setterObject != null)
            Object.DestroyImmediate(setterObject);
        if (variableManager != null)
            Object.DestroyImmediate(variableManager);
    }

    [Test]
    public void UpdateSceneVariables_WritesSceneNameAndLeavesSavePointKey()
    {
        SceneNameSetter setter = setterObject.GetComponent<SceneNameSetter>();
        MethodInfo update = typeof(SceneNameSetter).GetMethod(
            "UpdateSceneVariables",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(update);

        update.Invoke(setter, new object[] { "Kitchen" });

        Assert.AreEqual("Kitchen", flowchart.GetStringVariable("SceneName"));
        Assert.AreEqual("leave-this", flowchart.GetStringVariable("SavePointKey"));
    }

    static void AddString(Flowchart target, string key, string value)
    {
        var variable = target.gameObject.AddComponent<StringVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }
}
