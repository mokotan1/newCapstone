using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class ParrotPanelUiFixTests
{
    [Test]
    public void ChoosePanelBackground_ReturnsElectricSprite_WhenElectricOnIsTrue()
    {
        Sprite defaultSprite = CreateSprite("default");
        Sprite electricOnSprite = CreateSprite("electricOn");

        Sprite result = ParrotPanelUiFix.ChoosePanelBackground(defaultSprite, electricOnSprite, true);

        Assert.AreSame(electricOnSprite, result);
    }

    [Test]
    public void ChoosePanelBackground_ReturnsDefaultSprite_WhenElectricOnIsFalse()
    {
        Sprite defaultSprite = CreateSprite("default");
        Sprite electricOnSprite = CreateSprite("electricOn");

        Sprite result = ParrotPanelUiFix.ChoosePanelBackground(defaultSprite, electricOnSprite, false);

        Assert.AreSame(defaultSprite, result);
    }

    [Test]
    public void ChoosePanelBackground_ReturnsDefaultSprite_WhenElectricOnSpriteIsMissing()
    {
        Sprite defaultSprite = CreateSprite("default");

        Sprite result = ParrotPanelUiFix.ChoosePanelBackground(defaultSprite, null, true);

        Assert.AreSame(defaultSprite, result);
    }

    [Test]
    public void OnEnable_BlocksWorldInputUntilDisabled()
    {
        ModalInputGate.ResetForTests();
        var panel = new GameObject("Parret_Panel");
        var world = new GameObject("WorldObject");
        var fix = panel.AddComponent<ParrotPanelUiFix>();

        InvokeLifecycle(fix, "OnEnable");

        Assert.IsTrue(ModalInputGate.IsBlockingWorldInput);
        Assert.IsFalse(ModalInputGate.CanWorldClick(world));

        InvokeLifecycle(fix, "OnDisable");

        Assert.IsFalse(ModalInputGate.IsBlockingWorldInput);

        Object.DestroyImmediate(world);
        Object.DestroyImmediate(panel);
        ModalInputGate.ResetForTests();
    }

    [Test]
    public void OnEnable_HidesDuplicateBackspaceNameplateTargetingSamePanel_AndKeepsItHiddenOnDisable()
    {
        ModalInputGate.ResetForTests();
        var panel = new GameObject("Parret_Panel");
        var childBackspace = new GameObject("BackspaceNameplate", typeof(Button), typeof(PanelBackspaceCloser));
        childBackspace.transform.SetParent(panel.transform, false);

        var duplicateBackspace = new GameObject("BackspaceNameplate", typeof(Button), typeof(PanelBackspaceCloser));
        var duplicateCloser = duplicateBackspace.GetComponent<PanelBackspaceCloser>();
        SetTargetPanel(duplicateCloser, panel);

        var fix = panel.AddComponent<ParrotPanelUiFix>();

        InvokeLifecycle(fix, "OnEnable");

        Assert.IsTrue(childBackspace.activeSelf);
        Assert.IsFalse(duplicateBackspace.activeSelf);

        InvokeLifecycle(fix, "OnDisable");

        Assert.IsFalse(duplicateBackspace.activeSelf);

        Object.DestroyImmediate(duplicateBackspace);
        Object.DestroyImmediate(panel);
        ModalInputGate.ResetForTests();
    }

    private static Sprite CreateSprite(string objectName)
    {
        var texture = new Texture2D(2, 2);
        texture.name = objectName + "_texture";
        return Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
    }

    private static void InvokeLifecycle(ParrotPanelUiFix fix, string methodName)
    {
        MethodInfo method = typeof(ParrotPanelUiFix).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Lifecycle method not found: {methodName}");
        method.Invoke(fix, null);
    }

    private static void SetTargetPanel(PanelBackspaceCloser closer, GameObject panel)
    {
        FieldInfo field = typeof(PanelBackspaceCloser).GetField(
            "targetPanel",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "targetPanel field not found.");
        field.SetValue(closer, panel);
    }
}
