using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pure mesh math for <see cref="BloodFloodOverlayGraphic"/> wave silhouettes.
/// </summary>
public static class BloodFloodMeshBuilder
{
    public const int MinSegments = 12;
    public const int MaxSegments = 72;

    public struct MeshParams
    {
        public float FillAmount;
        public Color BloodColor;
        public Color EdgeColor;
        public float WaveAmplitude;
        public float WaveFrequency;
        public float NoisePhase;
        public float NoiseStrength;
        public float NoiseSeed;
        public float MaxAlpha;
        public float MinAlpha;
    }

    public static int ComputeSegmentCount(float waveFrequency)
    {
        int segments = Mathf.RoundToInt(Mathf.Clamp(waveFrequency, 0.5f, 6f) * 16f);
        return Mathf.Clamp(segments, MinSegments, MaxSegments);
    }

    public static float SampleTopWaveY(
        float normalizedX,
        float fillLevel,
        float rectYMin,
        float rectHeight,
        float waveAmplitude,
        float waveFrequency,
        float noisePhase,
        float noiseStrength,
        float noiseSeed)
    {
        float fillTop = rectYMin + rectHeight * fillLevel;
        float wave = Mathf.Sin((normalizedX * waveFrequency * Mathf.PI * 2f) + noisePhase) * waveAmplitude;
        float noise = (Mathf.PerlinNoise(
                           normalizedX * 2.8f + noiseSeed,
                           noisePhase * 0.17f + noiseSeed * 0.31f) - 0.5f)
                      * waveAmplitude * noiseStrength;
        return fillTop + wave + noise;
    }

    public static void PopulateMesh(VertexHelper vertexHelper, Rect rect, in MeshParams meshParams)
    {
        vertexHelper.Clear();

        float fillAmount = Mathf.Clamp01(meshParams.FillAmount);
        if (fillAmount <= 0.0001f || rect.width <= 0.01f || rect.height <= 0.01f)
            return;

        int segments = ComputeSegmentCount(meshParams.WaveFrequency);
        float alpha = Mathf.Lerp(meshParams.MinAlpha, meshParams.MaxAlpha, fillAmount);
        Color32 bodyColor = (Color32)(meshParams.BloodColor * alpha);
        Color32 topColor = (Color32)(meshParams.EdgeColor * alpha * 0.82f);

        float left = rect.xMin;
        float width = rect.width;
        float bottomY = rect.yMin;

        for (int segment = 0; segment <= segments; segment++)
        {
            float normalizedX = segment / (float)segments;
            float x = left + width * normalizedX;
            float topY = SampleTopWaveY(
                normalizedX,
                fillAmount,
                bottomY,
                rect.height,
                meshParams.WaveAmplitude,
                meshParams.WaveFrequency,
                meshParams.NoisePhase,
                meshParams.NoiseStrength,
                meshParams.NoiseSeed);

            float edgeBlend = Mathf.InverseLerp(
                bottomY + rect.height * fillAmount * 0.55f,
                bottomY + rect.height * fillAmount,
                topY);
            Color32 topVertexColor = Color32.Lerp(bodyColor, topColor, edgeBlend);

            vertexHelper.AddVert(new UIVertex
            {
                position = new Vector3(x, bottomY, 0f),
                color = bodyColor,
                uv0 = new Vector2(normalizedX, 0f),
            });
            vertexHelper.AddVert(new UIVertex
            {
                position = new Vector3(x, topY, 0f),
                color = topVertexColor,
                uv0 = new Vector2(normalizedX, fillAmount),
            });
        }

        for (int segment = 0; segment < segments; segment++)
        {
            int bottomLeft = segment * 2;
            int topLeft = bottomLeft + 1;
            int bottomRight = bottomLeft + 2;
            int topRight = bottomRight + 1;

            vertexHelper.AddTriangle(bottomLeft, topLeft, topRight);
            vertexHelper.AddTriangle(bottomLeft, topRight, bottomRight);
        }
    }
}
