using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Bounded Hall_playerble PlayMode packet: real SayDialog + MenuDialog.CachedButtons,
/// programmatic advance, captured loads (no full scene transition).
/// </summary>
public sealed class HallPlayablePlayModeTests
{
    const string SceneName = "Hall_playerble";
    const float StepTimeoutSeconds = 8f;

    Type controllerType;
    Type hostType;
    Type sceneInteractionType;
    object controller;
    List<string> loads;
    readonly Stack<IEnumerator> sequenceStack = new Stack<IEnumerator>();

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        controllerType = FindType("HallPlayableController");
        hostType = FindType("HallGlobalStateHost");
        sceneInteractionType = FindType("Godlotto.Interaction.SceneInteractionController")
            ?? FindType("SceneInteractionController");
        Assert.IsNotNull(controllerType, "HallPlayableController type missing");
        Assert.IsNotNull(hostType, "HallGlobalStateHost type missing");

        InvokeStatic(controllerType, "ResetStateForTests");
        loads = new List<string>();
        sequenceStack.Clear();

        AsyncOperation load = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
        Assert.IsNotNull(load, "Hall_playerble not in Build Settings");
        while (load != null && !load.isDone)
            yield return null;
        yield return null;

        controller = UnityEngine.Object.FindFirstObjectByType(controllerType, FindObjectsInactive.Include);
        Assert.IsNotNull(controller, "HallPlayableController missing in scene");
        ActivateHierarchy((MonoBehaviour)controller);

        InvokeStatic(hostType, "EnsureInstance");
        SetStaticProperty(sceneInteractionType, "RespectLegacyInteractionLock", false);
        SetStaticProperty(sceneInteractionType, "BlockDuringFungusDialogue", false);
        SetStaticProperty(sceneInteractionType, "BlockDuringSceneTransition", false);

        SetStaticField(controllerType, "SceneLoadHandlerForTests",
            MakeFuncStringBool(scene =>
            {
                loads.Add(scene);
                return true;
            }));
        SetStaticField(controllerType, "FadeThenHandlerForTests",
            MakeActionFloatFloatAction((_, __, done) => done?.Invoke()));
        SetStaticField(controllerType, "SayHandlerForTests", null);
        SetStaticField(controllerType, "MenuHandlerForTests", null);
        SetStaticField(controllerType, "SequenceRunnerForTests",
            MakeActionIEnumerator(seq =>
            {
                sequenceStack.Clear();
                if (seq != null)
                    sequenceStack.Push(seq);
            }));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        sequenceStack.Clear();
        InvokeStatic(controllerType, "ResetStateForTests");
        yield return null;
    }

    [UnityTest]
    public IEnumerator Routes_RightAndStairYes_BoundedRealSayMenu()
    {
        loads.Clear();
        ClearDuplicateClicks();
        InvokeInstance(controller, "OnInteraction", "right");
        yield return DriveUntil(() => loads.Count > 0, advanceSayMenu: false, "right load");
        Assert.AreEqual("Hall_Right", loads[0]);
        Assert.IsFalse(IsBusy(), "busy should release after right");

        loads.Clear();
        ClearDuplicateClicks();
        InvokeInstance(controller, "OnInteraction", "stair");
        yield return DriveUntil(() => loads.Count > 0, advanceSayMenu: true, "stair load");
        Assert.AreEqual("2floorMainHall", loads[0]);
        Assert.IsFalse(IsBusy(), "busy should release after stair yes");
    }

    IEnumerator DriveUntil(Func<bool> done, bool advanceSayMenu, string label)
    {
        float elapsed = 0f;
        while (!done())
        {
            if (elapsed >= StepTimeoutSeconds)
                Assert.Fail("Timed out: " + label);

            StepSequence();
            if (advanceSayMenu)
            {
                if (HasActiveMenuOption())
                    ClickFirstActiveMenuOption();
                else
                    PulseSayAdvance();
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Drain any trailing enumerator frames.
        for (int i = 0; i < 5; i++)
        {
            StepSequence();
            yield return null;
        }
    }

    void StepSequence()
    {
        while (sequenceStack.Count > 0)
        {
            IEnumerator cur = sequenceStack.Peek();
            if (!cur.MoveNext())
            {
                sequenceStack.Pop();
                continue;
            }

            if (cur.Current is IEnumerator nested)
                sequenceStack.Push(nested);
            break;
        }
    }

    static void PulseSayAdvance()
    {
        DialogInput input = UnityEngine.Object.FindFirstObjectByType<DialogInput>();
        input?.SetNextLineFlag();
    }

    static bool HasActiveMenuOption()
    {
        MenuDialog menu = MenuDialog.GetMenuDialog();
        if (menu == null || !menu.IsActive())
            return false;
        Button[] buttons = menu.CachedButtons;
        if (buttons == null)
            return false;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    static void ClickFirstActiveMenuOption()
    {
        MenuDialog menu = MenuDialog.GetMenuDialog();
        Button[] buttons = menu.CachedButtons;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy)
            {
                buttons[i].onClick.Invoke();
                return;
            }
        }
    }

    static void ActivateHierarchy(MonoBehaviour behaviour)
    {
        Transform node = behaviour.transform;
        while (node != null)
        {
            node.gameObject.SetActive(true);
            node = node.parent;
        }

        behaviour.enabled = true;
    }

    bool IsBusy()
    {
        PropertyInfo prop = controllerType.GetProperty("IsBusyForTests",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(prop);
        return (bool)prop.GetValue(controller);
    }

    void ClearDuplicateClicks()
    {
        MethodInfo method = sceneInteractionType?.GetMethod("ClearDuplicateClickHistory",
            BindingFlags.Public | BindingFlags.Static);
        method?.Invoke(null, null);
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
                Type candidate = types[i];
                if (candidate != null && candidate.Name == fullOrShortName)
                    return candidate;
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
        MethodInfo method = instance.GetType().GetMethod(methodName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, methodName);
        method.Invoke(instance, args);
    }

    static void SetStaticField(Type type, string fieldName, object value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(null, value);
    }

    static void SetStaticProperty(Type type, string propertyName, object value)
    {
        if (type == null)
            return;
        PropertyInfo prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        prop?.SetValue(null, value);
    }

    static object MakeFuncStringBool(Func<string, bool> func) => func;

    static object MakeActionFloatFloatAction(Action<float, float, Action> action) => action;

    static object MakeActionIEnumerator(Action<IEnumerator> action) => action;
}
