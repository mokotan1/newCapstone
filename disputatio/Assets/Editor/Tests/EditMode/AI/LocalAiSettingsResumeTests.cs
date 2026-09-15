using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class LocalAiSettingsResumeTests
{
    [Test]
    public void Status_SeparatesGpuRequestFromActualCpuAndUsage()
    {
        const string json = "{\"requested_mode\":\"gpu\",\"effective_backend\":\"cpu\",\"state\":\"ready\","
            + "\"gpu\":{\"name\":\"GPU\",\"utilization_percent\":42,\"memory_used_mib\":2048,\"memory_total_mib\":8192}}";
        Assert.IsTrue(LocalAiControlApi.TryParseStatus(json, out var status));
        Assert.AreEqual("cpu", status.EffectiveBackend);
        Assert.AreEqual(42, status.GpuUtilization);
        Assert.IsTrue(status.CanApply);
    }

    [TestCase("externally_managed")]
    [TestCase("draining")]
    [TestCase("warming")]
    public void Status_DisablesApplyDuringTransitionAndExternalOwnership(string state)
    {
        var status = new LocalAiRuntimeStatus("gpu", "unknown", state, false, "");
        Assert.IsFalse(status.CanApply);
    }

    [Test]
    public void InvalidStatusAndMode_AreRejected()
    {
        Assert.IsFalse(LocalAiControlApi.TryParseStatus("{}", out _));
        Assert.IsFalse(LocalAiControlApi.TryParseStatus("{\"state\":{},\"requested_mode\":\"cpu\"}", out _));
        Assert.Throws<System.ArgumentException>(() => LocalAiControlApi.BuildSettingsBody("invalid"));
    }

    [Test]
    public void Factory_AddsOneClosedPanelAndThreeModeButtons()
    {
        var root = new GameObject("Settings", typeof(RectTransform));
        try
        {
            LocalAiSettingsPanel.Ensure(root.transform);
            LocalAiSettingsPanel.Ensure(root.transform);
            var panels = root.GetComponentsInChildren<LocalAiSettingsPanel>(true);
            Assert.AreEqual(1, panels.Length);
            Assert.IsFalse(panels[0].gameObject.activeSelf);
            Assert.AreEqual(4, panels[0].GetComponentsInChildren<Button>(true).Length);
            Assert.IsTrue(panels[0].OwnsIndependentStatusPoll);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void EmbeddedPanel_DoesNotOwnIndependentStatusPoll()
    {
        var root = new GameObject("CheshireAiPage", typeof(RectTransform));
        try
        {
            LocalAiSettingsPanel.EnsureEmbedded(root.transform);
            var panel = root.GetComponentInChildren<LocalAiSettingsPanel>(true);
            Assert.IsNotNull(panel);
            Assert.IsTrue(panel.gameObject.activeSelf);
            Assert.IsFalse(panel.OwnsIndependentStatusPoll);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void EmbeddedPanel_ModeButtonsStayClickableWhenParentIsShown()
    {
        var page = new GameObject("CheshireAiPage", typeof(RectTransform));
        try
        {
            page.SetActive(false);
            LocalAiSettingsPanel.EnsureEmbedded(page.transform);
            page.SetActive(true);

            Assert.IsTrue(FindModeButton(page, "CPU").interactable);
            Assert.IsTrue(FindModeButton(page, "GPU").interactable);
            Assert.IsTrue(FindModeButton(page, "AUTO").interactable);
        }
        finally { Object.DestroyImmediate(page); }
    }

    [Test]
    public void EmbeddedPanel_RestoresClickabilityAfterApplyCompletes()
    {
        var page = new GameObject("CheshireAiPage", typeof(RectTransform));
        try
        {
            page.SetActive(false);
            LocalAiSettingsPanel.EnsureEmbedded(page.transform);
            page.SetActive(true);
            var panel = page.GetComponentInChildren<LocalAiSettingsPanel>(true);
            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
                button.interactable = false;

            panel.NotifyApplyCompleted();

            Assert.IsTrue(FindModeButton(page, "CPU").interactable);
            Assert.IsTrue(FindModeButton(page, "GPU").interactable);
            Assert.IsTrue(FindModeButton(page, "AUTO").interactable);
        }
        finally { Object.DestroyImmediate(page); }
    }

    [Test]
    public void ModeButtons_UseDistinctHoverAndPressedColors()
    {
        var root = new GameObject("CheshireAiPage", typeof(RectTransform));
        try
        {
            LocalAiSettingsPanel.EnsureEmbedded(root.transform);
            Button cpu = FindModeButton(root, "CPU");
            Button gpu = FindModeButton(root, "GPU");
            Button auto = FindModeButton(root, "AUTO");
            AssertModeButtonInteraction(cpu);
            AssertModeButtonInteraction(gpu);
            AssertModeButtonInteraction(auto);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ModeButtons_KeepRequestedModeVisuallySelected()
    {
        var root = new GameObject("CheshireAiPage", typeof(RectTransform));
        try
        {
            LocalAiSettingsPanel.EnsureEmbedded(root.transform);
            var panel = root.GetComponentInChildren<LocalAiSettingsPanel>(true);
            Button cpu = FindModeButton(root, "CPU");
            Button gpu = FindModeButton(root, "GPU");
            Button auto = FindModeButton(root, "AUTO");

            AssertSelected(gpu, true);
            AssertSelected(cpu, false);
            AssertSelected(auto, false);

            panel.HighlightRequestedMode("cpu");
            AssertSelected(cpu, true);
            AssertSelected(gpu, false);
            AssertSelected(auto, false);

            panel.HighlightRequestedMode("AUTO");
            AssertSelected(auto, true);
            AssertSelected(cpu, false);
            AssertSelected(gpu, false);
        }
        finally { Object.DestroyImmediate(root); }
    }

    static Button FindModeButton(GameObject root, string name)
    {
        Transform found = root.transform.Find("LocalAiSettingsPanel/" + name);
        Assert.IsNotNull(found, name);
        return found.GetComponent<Button>();
    }

    static void AssertModeButtonInteraction(Button button)
    {
        Assert.AreEqual(Selectable.Transition.ColorTint, button.transition);
        ColorBlock colors = button.colors;
        Assert.AreEqual(Color.white, button.targetGraphic.color);
        Assert.AreEqual(SettingsWoodPanelSpec.SelectedBorder, colors.highlightedColor);
        Assert.AreEqual(SettingsWoodPanelSpec.PrimaryButtonFace, colors.pressedColor);
        Assert.AreNotEqual(colors.normalColor, colors.highlightedColor);
        Assert.AreNotEqual(colors.normalColor, colors.pressedColor);
        Assert.AreNotEqual(colors.highlightedColor, colors.pressedColor);
        Assert.IsNotNull(button.GetComponent<Outline>());
    }

    static void AssertSelected(Button button, bool selected)
    {
        ColorBlock colors = button.colors;
        Outline outline = button.GetComponent<Outline>();
        Assert.IsNotNull(outline);
        if (selected)
        {
            Assert.AreEqual(SettingsWoodPanelSpec.SelectedBackground, colors.normalColor);
            Assert.AreEqual(SettingsWoodPanelSpec.SelectedBorder, outline.effectColor);
            Assert.AreEqual(SettingsWoodPanelSpec.SelectedBackground, colors.disabledColor);
        }
        else
        {
            Assert.AreEqual(SettingsWoodPanelSpec.ButtonFace, colors.normalColor);
            Assert.AreEqual(SettingsWoodPanelSpec.Border, outline.effectColor);
            Color expectedDisabled = SettingsWoodPanelSpec.ButtonFace;
            expectedDisabled.a = 0.45f;
            Assert.AreEqual(expectedDisabled, colors.disabledColor);
        }
    }
}
