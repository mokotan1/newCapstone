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
        }
        finally { Object.DestroyImmediate(root); }
    }
}
