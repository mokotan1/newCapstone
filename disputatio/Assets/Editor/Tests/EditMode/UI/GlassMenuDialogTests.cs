using Fungus;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class GlassMenuDialogTests
{
    GameObject root;
    GlassMenuDialog dialog;
    Button buttonPrefab;

    [SetUp]
    public void SetUp()
    {
        GlassMenuDialog.BlockExecutorForTests = null;

        root = new GameObject("GlassMenuRoot", typeof(RectTransform));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var container = new GameObject("Container", typeof(RectTransform));
        container.transform.SetParent(panel.transform, false);

        var btnGo = new GameObject("OptionButton", typeof(RectTransform), typeof(Button));
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(btnGo.transform, false);
        buttonPrefab = btnGo.GetComponent<Button>();

        dialog = root.AddComponent<GlassMenuDialog>();
        SetPrivate(dialog, "panelRoot", panel.GetComponent<RectTransform>());
        SetPrivate(dialog, "optionContainer", container.GetComponent<RectTransform>());
        SetPrivate(dialog, "optionButtonPrefab", buttonPrefab);
    }

    [TearDown]
    public void TearDown()
    {
        GlassMenuDialog.BlockExecutorForTests = null;
        if (root != null) Object.DestroyImmediate(root);
        if (buttonPrefab != null) Object.DestroyImmediate(buttonPrefab.gameObject);
    }

    [Test]
    public void ApplyPlacement_SetsPanelAnchorAndOffset()
    {
        dialog.ApplyPlacement(MenuAnchor.BottomCenter, new Vector2(10f, 80f));

        var panel = (RectTransform)GetPrivate(dialog, "panelRoot");
        Assert.AreEqual(new Vector2(0.5f, 0f), panel.anchorMin);
        Assert.AreEqual(new Vector2(10f, 80f), panel.anchoredPosition);
    }

    [Test]
    public void AddOption_SpawnsButtonWithText()
    {
        dialog.AddOption("조심스럽게 펼쳐 읽는다", true, null);

        var container = (RectTransform)GetPrivate(dialog, "optionContainer");
        Assert.AreEqual(1, container.childCount);
        var label = container.GetChild(0).GetComponentInChildren<TextMeshProUGUI>();
        Assert.AreEqual("조심스럽게 펼쳐 읽는다", label.text);
    }

    [Test]
    public void Clear_RemovesSpawnedButtons()
    {
        dialog.AddOption("A", true, null);
        dialog.AddOption("B", true, null);

        dialog.Clear();

        var container = (RectTransform)GetPrivate(dialog, "optionContainer");
        Assert.AreEqual(0, container.childCount);
    }

    [Test]
    public void OptionClick_ExecutesTargetBlockThroughSeam()
    {
        Block executed = null;
        GlassMenuDialog.BlockExecutorForTests = b => executed = b;
        var block = root.AddComponent<Block>();
        block.BlockName = "Target";

        dialog.AddOption("go", true, block);
        var container = (RectTransform)GetPrivate(dialog, "optionContainer");
        container.GetChild(0).GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(block, executed);
    }

    static void SetPrivate(object t, string name, object value)
    {
        var f = t.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        f.SetValue(t, value);
    }

    static object GetPrivate(object t, string name)
    {
        var f = t.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return f.GetValue(t);
    }
}
