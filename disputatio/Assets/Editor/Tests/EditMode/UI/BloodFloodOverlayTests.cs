using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class BloodFloodOverlayTests
{
    GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void ComputeSegmentCount_ScalesWithWaveFrequency()
    {
        int low = BloodFloodMeshBuilder.ComputeSegmentCount(1f);
        int high = BloodFloodMeshBuilder.ComputeSegmentCount(4f);

        Assert.Greater(low, 0);
        Assert.Greater(high, low);
        Assert.LessOrEqual(high, BloodFloodMeshBuilder.MaxSegments);
    }

    [Test]
    public void SampleTopWaveY_RisesWithFillAmount()
    {
        float lowFill = BloodFloodMeshBuilder.SampleTopWaveY(
            0.5f,
            0.25f,
            0f,
            200f,
            8f,
            2f,
            0f,
            0.1f,
            1f);
        float highFill = BloodFloodMeshBuilder.SampleTopWaveY(
            0.5f,
            0.75f,
            0f,
            200f,
            8f,
            2f,
            0f,
            0.1f,
            1f);

        Assert.Greater(highFill, lowFill);
    }

    [Test]
    public void PopulateMesh_WithPositiveFill_AddsVertices()
    {
        var vertexHelper = new VertexHelper();
        var rect = new Rect(0f, 0f, 320f, 180f);
        var meshParams = new BloodFloodMeshBuilder.MeshParams
        {
            FillAmount = 0.5f,
            BloodColor = new Color(0.29f, 0.02f, 0.02f, 1f),
            EdgeColor = new Color(0.12f, 0.01f, 0.01f, 1f),
            WaveAmplitude = 6f,
            WaveFrequency = 2f,
            NoisePhase = 0f,
            NoiseStrength = 0.1f,
            NoiseSeed = 2f,
            MaxAlpha = 0.9f,
            MinAlpha = 0.3f,
        };

        BloodFloodMeshBuilder.PopulateMesh(vertexHelper, rect, meshParams);

        Assert.Greater(vertexHelper.currentVertCount, 4);
        Assert.Greater(vertexHelper.currentIndexCount, 6);
    }

    [Test]
    public void PopulateMesh_WithZeroFill_AddsNoVertices()
    {
        var vertexHelper = new VertexHelper();
        var rect = new Rect(0f, 0f, 320f, 180f);
        var meshParams = new BloodFloodMeshBuilder.MeshParams
        {
            FillAmount = 0f,
            BloodColor = Color.red,
            EdgeColor = Color.black,
            WaveAmplitude = 6f,
            WaveFrequency = 2f,
            MaxAlpha = 0.9f,
            MinAlpha = 0.3f,
        };

        BloodFloodMeshBuilder.PopulateMesh(vertexHelper, rect, meshParams);

        Assert.AreEqual(0, vertexHelper.currentVertCount);
    }

    [Test]
    public void FillAmount_ClampsToUnitRange()
    {
        var overlay = CreateOverlay();
        overlay.FillAmount = 1.8f;
        Assert.AreEqual(1f, overlay.FillAmount, 0.0001f);

        overlay.FillAmount = -0.4f;
        Assert.AreEqual(0f, overlay.FillAmount, 0.0001f);
    }

    [Test]
    public void ComputeImpactFillAmount_AppliesStyleMultipliers()
    {
        var overlay = CreateOverlay();

        float simple = overlay.ComputeImpactFillAmount(new BloodDripImpactInfo
        {
            Style = BloodDripStyle.SimpleDrop,
            PoolContribution = 8f,
            DropSize = 8f,
        });
        float streak = overlay.ComputeImpactFillAmount(new BloodDripImpactInfo
        {
            Style = BloodDripStyle.AttachedStreak,
            PoolContribution = 8f,
            DropSize = 8f,
        });

        Assert.Greater(streak, simple);
    }

    [Test]
    public void AddFillOverTime_AccumulatesTargetWithoutDecreasingFill()
    {
        var overlay = CreateOverlay();
        overlay.FillAmount = 0.2f;

        overlay.AddFillOverTime(0.05f, 1f);
        Assert.GreaterOrEqual(overlay.TargetFillAmount, overlay.FillAmount);
        Assert.AreEqual(0.25f, overlay.TargetFillAmount, 0.0001f);

        overlay.AddFillOverTime(0.03f, 1f);
        Assert.GreaterOrEqual(overlay.TargetFillAmount, 0.25f);
        Assert.GreaterOrEqual(overlay.FillAmount, 0.2f);
    }

    [Test]
    public void ResetFlood_ClearsFillAndTarget()
    {
        var overlay = CreateOverlay();
        overlay.AddFillOverTime(0.2f, 1f);
        overlay.ResetFlood();

        Assert.AreEqual(0f, overlay.FillAmount, 0.0001f);
        Assert.AreEqual(0f, overlay.TargetFillAmount, 0.0001f);
    }

    [Test]
    public void FullThreshold_TriggersOnlyOnceUntilReset()
    {
        var overlay = CreateOverlay();
        overlay.FillAmount = 0.99f;

        Assert.IsTrue(overlay.TryTriggerFullIfThresholdMet());
        Assert.IsTrue(overlay.IsFull);
        Assert.IsFalse(overlay.TryTriggerFullIfThresholdMet());

        overlay.ResetFlood();
        Assert.IsFalse(overlay.IsFull);

        overlay.FillAmount = 0.99f;
        Assert.IsTrue(overlay.TryTriggerFullIfThresholdMet());
    }

    [Test]
    public void FullThreshold_DoesNotTriggerBelowThreshold()
    {
        var overlay = CreateOverlay();
        overlay.FillAmount = 0.9f;

        Assert.IsFalse(overlay.TryTriggerFullIfThresholdMet());
        Assert.IsFalse(overlay.IsFull);
    }

    BloodFloodOverlay CreateOverlay()
    {
        root = new GameObject("BloodFloodOverlayTests");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(root.transform, false);
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var overlayGo = new GameObject(
            "BloodFloodOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(BloodFloodOverlayGraphic),
            typeof(BloodFloodOverlay));
        overlayGo.transform.SetParent(canvasGo.transform, false);

        var rect = overlayGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(640f, 360f);

        var overlay = overlayGo.GetComponent<BloodFloodOverlay>();
        overlay.ConfigureForTests(0f, 1f, 0f, false);
        return overlay;
    }
}
