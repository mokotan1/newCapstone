using System;
using System.Collections;
using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Door clicks go through Update overlap and the original click guards.
/// The ribbon choice returns by calling BackNavigator.GoBack.
/// </summary>
public sealed class SecondFloorRoomEntrancePlayModeTests
{
    [UnityTest]
    public IEnumerator WifeAndBed_DoorClickAndRibbonBack()
    {
        Type controllerType = FindType("SecondFloorRoomEntranceController");
        Type hostType = FindType("HallGlobalStateHost");
        Type keysType = FindType("FungusVariableKeys");
        Type sceneNamesType = FindType("SceneNames");
        Type sceneInteractionType = FindType("Godlotto.Interaction.SceneInteractionController")
            ?? FindType("SceneInteractionController");
        Assert.IsNotNull(controllerType);

        yield return Exercise("WifeEntrance", "Wife_Door", "UsedWifeKey", "WifeRoom", controllerType, hostType, keysType, sceneNamesType, sceneInteractionType);
        yield return Exercise("BedEntrance", "Bed_Door", "UsedBedKey", "BedRoom", controllerType, hostType, keysType, sceneNamesType, sceneInteractionType);
        InvokeStatic(controllerType, "ResetStateForTests");
    }

    IEnumerator Exercise(
        string sceneName,
        string doorName,
        string keyField,
        string destinationField,
        Type controllerType,
        Type hostType,
        Type keysType,
        Type sceneNamesType,
        Type sceneInteractionType)
    {
        string loaded = null;
        var said = new System.Collections.Generic.List<string>();

        InvokeStatic(controllerType, "ResetStateForTests");
        InvokeStatic(hostType, "EnsureInstance");
        SetStaticProperty(sceneInteractionType, "RespectLegacyInteractionLock", false);
        SetStaticProperty(sceneInteractionType, "BlockDuringFungusDialogue", false);
        SetStaticProperty(sceneInteractionType, "BlockDuringSceneTransition", false);
        SetStaticProperty(sceneInteractionType, "DuplicateClickCooldownSeconds", 0f);
        SetStaticField(controllerType, "SceneLoadHandlerForTests", (Func<string, bool>)(name =>
        {
            loaded = name;
            return true;
        }));
        SetStaticField(controllerType, "FadeThenHandlerForTests", (Action<float, float, Action>)((_, __, onComplete) => onComplete?.Invoke()));
        SetStaticField(controllerType, "SayHandlerForTests", (Action<string, Action>)((line, onComplete) =>
        {
            said.Add(line);
            onComplete?.Invoke();
        }));
        SetStaticField(controllerType, "WaitHandlerForTests", (Action<float>)(_ => { }));
        SetStaticField(controllerType, "AudioHandlerForTests", (Action)(() => { }));
        SetStaticField(controllerType, "ExposureHandlerForTests", (Action<string, float, float>)((_, __, ___) => { }));
        SetStaticField(controllerType, "MenuHandlerForTests", (Action<string[], Action<int>>)((_, onChoice) => onChoice?.Invoke(0)));
        SetStaticField(controllerType, "SequenceRunnerForTests", (Action<IEnumerator>)(RunToEnd));

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        Assert.IsNotNull(load, sceneName + " is not in Build Settings");
        while (load != null && !load.isDone)
            yield return null;
        yield return null;

        UnityEngine.Object[] controllers = UnityEngine.Object.FindObjectsByType(
            controllerType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Assert.AreEqual(1, controllers.Length, sceneName);
        Assert.AreEqual(0, UnityEngine.Object.FindObjectsByType<Flowchart>(FindObjectsSortMode.None).Length, sceneName);

        GameObject door = GameObject.Find(doorName);
        Assert.IsNotNull(door, doorName);
        Collider2D collider = door.GetComponent<Collider2D>();
        Clickable2D clickable = door.GetComponent<Clickable2D>();
        Assert.IsNotNull(collider);
        Assert.IsTrue(collider.enabled, doorName);
        Assert.IsNotNull(clickable);
        Assert.IsFalse(clickable.enabled, doorName);

        MethodInfo update = controllerType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(update);
        PressDoor(controllerType, controllers[0], update, collider);
        Assert.IsNull(loaded, sceneName + " locked door");
        Assert.AreEqual("잠겨있다", said[0], sceneName);

        object host = hostType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        MethodInfo setBool = hostType.GetMethod("SetBool", BindingFlags.Public | BindingFlags.Instance);
        setBool.Invoke(host, new object[] { Const(keysType, keyField), true });
        said.Clear();
        PressDoor(controllerType, controllers[0], update, collider);
        Assert.AreEqual(Const(sceneNamesType, destinationField), loaded, sceneName);
        Assert.AreNotEqual(Const(sceneNamesType, "HallAnimate"), loaded, sceneName);

        Type navigatorType = FindType("BackNavigator");
        Assert.IsNotNull(navigatorType);
        Assert.GreaterOrEqual(
            UnityEngine.Object.FindObjectsByType(navigatorType, FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length,
            1,
            sceneName);

        Button ribbon = null;
        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].onClick.GetPersistentEventCount() > 0
                && buttons[i].onClick.GetPersistentMethodName(0) == "OnInteraction")
                ribbon = buttons[i];
        }

        Assert.IsNotNull(ribbon, sceneName + " ribbon");
        Type rightType = FindType("SecondFloorRightController");
        if (rightType != null)
        {
            SetStaticField(rightType, "SceneLoadHandlerForTests", (Func<string, bool>)(_ => true));
            SetStaticField(rightType, "FadeThenHandlerForTests", (Action<float, float, Action>)((_, __, onComplete) => onComplete?.Invoke()));
            SetStaticField(rightType, "SayHandlerForTests", (Action<string, Action>)((_, onComplete) => onComplete?.Invoke()));
            SetStaticField(rightType, "WaitHandlerForTests", (Action<float>)(_ => { }));
            SetStaticField(rightType, "AudioHandlerForTests", (Action)(() => { }));
            SetStaticField(rightType, "BgmHandlerForTests", (Action<int>)(_ => { }));
            SetStaticField(rightType, "ExposureHandlerForTests", (Action<string, float, float>)((_, __, ___) => { }));
            SetStaticField(rightType, "MenuHandlerForTests", (Action<string[], Action<int>>)((_, onChoice) => onChoice?.Invoke(1)));
            SetStaticField(rightType, "SequenceRunnerForTests", (Action<IEnumerator>)(RunToEnd));
        }

        setBool.Invoke(host, new object[] { Const(keysType, "IsClicked"), false });
        loaded = null;
        ribbon.onClick.Invoke();
        for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name == sceneName; frame++)
            yield return null;
        Assert.AreEqual("2floorHallway_Right", SceneManager.GetActiveScene().name, sceneName);
        Assert.IsNull(loaded, sceneName);
        yield return null;
    }

    static void PressDoor(Type controllerType, UnityEngine.Object controller, MethodInfo update, Collider2D collider)
    {
        SetStaticField(controllerType, "PressForTests", ScreenPointInside(collider));
        SetStaticField(controllerType, "HasPressForTests", true);
        update.Invoke(controller, null);
    }

    static Vector2 ScreenPointInside(Collider2D collider)
    {
        Camera camera = Camera.main;
        Assert.IsNotNull(camera, "entrance scene has no main camera");
        Vector2 world = collider.bounds.center;
        var polygon = collider as PolygonCollider2D;
        if (polygon != null)
        {
            Vector2 sum = Vector2.zero;
            Vector2[] points = polygon.points;
            for (int i = 0; i < points.Length; i++)
                sum += (Vector2)polygon.transform.TransformPoint(points[i]);
            if (points.Length > 0)
                world = sum / points.Length;
        }

        if (!collider.OverlapPoint(world))
            world = collider.bounds.center;
        Assert.IsTrue(collider.OverlapPoint(world), collider.name);
        return camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
    }

    static string Const(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, fieldName);
        return (string)field.GetValue(null);
    }

    static void RunToEnd(IEnumerator sequence)
    {
        while (sequence != null && sequence.MoveNext())
        {
            if (sequence.Current is IEnumerator nested)
                RunToEnd(nested);
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
