using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class KitchenFlowchartExecuteUiRaycastPolicyTests
{
    readonly System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null)
                Object.DestroyImmediate(go);
        }

        spawned.Clear();
    }

    [Test]
    public void ConfigureExecuteButton_DisablesRaycast_ForTransparentExecuteGraphic()
    {
        Button button = CreateButton("TransparentExecute", alpha: 0f);

        KitchenFlowchartExecuteUiRaycastPolicy.ConfigureExecuteButton(button);

        Assert.IsFalse(button.targetGraphic.raycastTarget);
    }

    [Test]
    public void ConfigureExecuteButton_KeepsRaycast_ForVisibleExecuteGraphic()
    {
        Button button = CreateButton("VisibleExecute", alpha: 1f);

        KitchenFlowchartExecuteUiRaycastPolicy.ConfigureExecuteButton(button);

        Assert.IsTrue(button.targetGraphic.raycastTarget);
    }

    [Test]
    public void DisableDecorativePanelBackgroundRaycast_TurnsOffBackgroundImage()
    {
        GameObject panel = CreatePanelBackground("Sink_Pannel");

        KitchenFlowchartExecuteUiRaycastPolicy.DisableDecorativePanelBackgroundRaycast(panel);

        Assert.IsFalse(panel.GetComponent<Image>().raycastTarget);
        Assert.IsFalse(panel.GetComponent<CanvasGroup>().blocksRaycasts);
    }

    [Test]
    public void IsExecuteRoutedButton_ReturnsTrue_WhenWiredToKitchenOnInteraction()
    {
        GameObject root = new GameObject("KitchenRoot");
        spawned.Add(root);
        var controller = root.AddComponent<KitchenInteractionController>();
        Button button = CreateButton("Faucet", alpha: 1f);
        UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
            button.onClick,
            controller.OnInteraction,
            "faucet");

        Assert.IsTrue(KitchenFlowchartExecuteUiRaycastPolicy.IsExecuteRoutedButton(button, controller));
    }

    Button CreateButton(string name, float alpha)
    {
        var go = new GameObject(name, typeof(RectTransform));
        spawned.Add(go);
        var image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, alpha);
        image.raycastTarget = true;
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    GameObject CreatePanelBackground(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        spawned.Add(go);
        var image = go.AddComponent<Image>();
        image.raycastTarget = true;
        go.GetComponent<CanvasGroup>().blocksRaycasts = true;
        return go;
    }
}
