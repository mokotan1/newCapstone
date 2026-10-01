using System;
using System.Collections;
using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// PlayMode assembly cannot reference game scripts. The five right-hall scenes are
/// loaded for real, then outgoing loads are captured through test hooks.
/// </summary>
public sealed class RightHallRoutePlayModeTests
{
    Type controllerType;
    Type routeType;
    Type hostType;
    Type sceneInteractionType;
    Type sceneNamesType;

    [UnityTest]
    public IEnumerator FiveScenes_HaveOneController_NoFlowchart_AndCaptureOutgoingLoad()
    {
        controllerType = FindType("RightHallCorridorController");
        routeType = FindType("RightHallRoute");
        hostType = FindType("HallGlobalStateHost");
        sceneNamesType = FindType("SceneNames");
        sceneInteractionType = FindType("Godlotto.Interaction.SceneInteractionController")
            ?? FindType("SceneInteractionController");
        Assert.IsNotNull(controllerType);
        Assert.IsNotNull(routeType);
        Assert.IsNotNull(hostType);
        Assert.IsNotNull(sceneNamesType);

        (string scene, string route, string interaction, string destinationField)[] cases =
        {
            ("Hall_Right", "HallRight", "front", "HallRight2"),
            ("Hall_Right2", "HallRight2", "front", "HallRightCross"),
            ("Hall_RightCross", "HallRightCross", "right", "StudyEntrance"),
            ("Hallway_Right", "HallwayRight", "front", "HallwayRight2"),
            ("Hallway_Right2", "HallwayRight2", "front", "HallPlayable"),
        };

        for (int i = 0; i < cases.Length; i++)
        {
            (string scene, string route, string interaction, string destinationField) = cases[i];
            string destination = SceneConst(destinationField);
            string loaded = null;

            InvokeStatic(controllerType, "ResetStateForTests");
            InvokeStatic(hostType, "EnsureInstance");
            SetStaticProperty(sceneInteractionType, "RespectLegacyInteractionLock", false);
            SetStaticProperty(sceneInteractionType, "BlockDuringFungusDialogue", false);
            SetStaticProperty(sceneInteractionType, "BlockDuringSceneTransition", false);
            SetStaticField(controllerType, "SceneLoadHandlerForTests", (Func<string, bool>)(name =>
            {
                loaded = name;
                return true;
            }));
            SetStaticField(controllerType, "FadeThenHandlerForTests", (Action<float, float, Action>)((_, __, onComplete) => onComplete?.Invoke()));
            SetStaticField(controllerType, "SayHandlerForTests", (Action<string, Action>)((_, onComplete) => onComplete?.Invoke()));
            SetStaticField(controllerType, "WaitHandlerForTests", (Action<float>)(_ => { }));
            SetStaticField(controllerType, "AudioHandlerForTests", (Action)(() => { }));
            SetStaticField(controllerType, "ExposureHandlerForTests", (Action<string, float, float>)((_, __, ___) => { }));
            SetStaticField(controllerType, "SequenceRunnerForTests", (Action<IEnumerator>)(sequence =>
            {
                while (sequence != null && sequence.MoveNext())
                {
                    if (sequence.Current is IEnumerator nested)
                    {
                        while (nested.MoveNext())
                        {
                        }
                    }
                }
            }));

            AsyncOperation load = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            Assert.IsNotNull(load, scene + " is not in Build Settings");
            while (load != null && !load.isDone)
                yield return null;
            yield return null;

            UnityEngine.Object[] controllers = UnityEngine.Object.FindObjectsByType(
                controllerType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Assert.AreEqual(1, controllers.Length, scene);
            object actualRoute = controllerType.GetProperty("Route").GetValue(controllers[0]);
            Assert.AreEqual(Enum.Parse(routeType, route), actualRoute, scene);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Flowchart>(FindObjectsSortMode.None).Length, scene);
            AssertNoMissingScripts(scene);

            InvokeInstance(controllers[0], "OnInteraction", interaction);
            Assert.AreEqual(destination, loaded, scene);
            Assert.AreNotEqual(SceneConst("HallAnimate"), loaded, scene);
        }

        InvokeStatic(controllerType, "ResetStateForTests");
    }

    string SceneConst(string fieldName)
    {
        FieldInfo field = sceneNamesType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, fieldName);
        return (string)field.GetValue(null);
    }

    static void AssertNoMissingScripts(string scene)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            Component[] components = objects[i].GetComponents<Component>();
            for (int c = 0; c < components.Length; c++)
                Assert.IsNotNull(components[c], scene + " missing script on " + objects[i].name);
        }
    }

    static Type FindType(string fullOrShortName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = null;
            try
            {
                type = assembly.GetType(fullOrShortName);
            }
            catch
            {
                continue;
            }

            if (type != null)
                return type;
            if (fullOrShortName.Contains("."))
                continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch
            {
                continue;
            }

            if (types == null)
                continue;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] != null && types[i].Name == fullOrShortName)
                    return types[i];
            }
        }

        return null;
    }

    static void InvokeStatic(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, methodName);
        method.Invoke(null, null);
    }

    static void InvokeInstance(object instance, string methodName, params object[] args)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(method, methodName);
        method.Invoke(instance, args);
    }

    static void SetStaticField(Type type, string fieldName, object value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(null, value);
    }

    static void SetStaticProperty(Type type, string propertyName, object value)
    {
        if (type == null)
            return;
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        property?.SetValue(null, value);
    }
}
