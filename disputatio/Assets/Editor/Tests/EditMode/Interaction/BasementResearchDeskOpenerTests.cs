using Fungus;
using Godlotto.Interaction;
using Godlotto.ModalInput;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BasementResearchDeskOpenerTests
{
    GameObject desk;
    GameObject panel;
    BasementResearchDeskOpener opener;
    PanelBackspaceCloser closer;

    [SetUp]
    public void SetUp()
    {
        InteractionLock.ForceUnlock();
        desk = new GameObject("Desk", typeof(BoxCollider2D));
        panel = new GameObject("Panel");
        panel.SetActive(false);
        panel.AddComponent<ModalInputScope>();
        opener = desk.AddComponent<BasementResearchDeskOpener>();
        SerializedObject openerSo = new SerializedObject(opener);
        openerSo.FindProperty("panel").objectReferenceValue = panel;
        openerSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject button = new GameObject("Backspace", typeof(RectTransform), typeof(Image), typeof(Button));
        button.transform.SetParent(panel.transform);
        closer = button.AddComponent<PanelBackspaceCloser>();
        SerializedObject closerSo = new SerializedObject(closer);
        closerSo.FindProperty("targetPanel").objectReferenceValue = panel;
        closerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(panel);
        Object.DestroyImmediate(desk);
        InteractionLock.ForceUnlock();
    }

    [Test]
    public void DeskPanelOpensOnceAndBackspaceRestoresWorldInput()
    {
        Assert.That(opener.TryOpenPanel(Vector2.zero), Is.True);
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(opener.TryOpenPanel(Vector2.zero), Is.False);

        closer.ClosePanel();

        Assert.That(panel.activeSelf, Is.False);
        Assert.That(opener.TryOpenPanel(Vector2.zero), Is.True);
    }

    [Test]
    public void InputGateBlocksDeskPanel()
    {
        InteractionInputGate.Block("research-room-test");
        try
        {
            Assert.That(opener.TryOpenPanel(Vector2.zero), Is.False);
            Assert.That(panel.activeSelf, Is.False);
        }
        finally
        {
            InteractionInputGate.Unblock("research-room-test");
        }
    }
}
