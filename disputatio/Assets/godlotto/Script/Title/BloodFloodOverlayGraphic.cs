using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Custom UI graphic that renders a bottom-up blood flood with an irregular top silhouette.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class BloodFloodOverlayGraphic : MaskableGraphic
{
    [SerializeField] float fillAmount;
    [SerializeField] Color bloodColor = new Color(0.29f, 0.02f, 0.02f, 1f);
    [SerializeField] Color edgeColor = new Color(0.12f, 0.01f, 0.01f, 1f);
    [SerializeField] float waveAmplitude = 10f;
    [SerializeField] float waveFrequency = 2.4f;
    [SerializeField] float maxAlpha = 0.9f;
    [SerializeField] float minAlpha = 0.28f;
    [SerializeField] float noiseStrength = 0.12f;
    [SerializeField] float noiseSeed = 4.7f;

    float noisePhase;

    public float FillAmount
    {
        get => fillAmount;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(fillAmount, clamped))
                return;

            fillAmount = clamped;
            SetVerticesDirty();
        }
    }

    public Color BloodColor
    {
        get => bloodColor;
        set
        {
            bloodColor = value;
            SetVerticesDirty();
        }
    }

    public Color EdgeColor
    {
        get => edgeColor;
        set
        {
            edgeColor = value;
            SetVerticesDirty();
        }
    }

    public float WaveAmplitude
    {
        get => waveAmplitude;
        set
        {
            waveAmplitude = Mathf.Max(0f, value);
            SetVerticesDirty();
        }
    }

    public float WaveFrequency
    {
        get => waveFrequency;
        set
        {
            waveFrequency = Mathf.Max(0.25f, value);
            SetVerticesDirty();
        }
    }

    public float MaxAlpha
    {
        get => maxAlpha;
        set
        {
            maxAlpha = Mathf.Clamp(value, 0f, 1f);
            SetVerticesDirty();
        }
    }

    public float MinAlpha
    {
        get => minAlpha;
        set
        {
            minAlpha = Mathf.Clamp(value, 0f, 1f);
            SetVerticesDirty();
        }
    }

    public float NoisePhase
    {
        get => noisePhase;
        set
        {
            noisePhase = value;
            SetVerticesDirty();
        }
    }

    protected BloodFloodOverlayGraphic()
    {
        useLegacyMeshGeneration = false;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        var meshParams = new BloodFloodMeshBuilder.MeshParams
        {
            FillAmount = fillAmount,
            BloodColor = bloodColor,
            EdgeColor = edgeColor,
            WaveAmplitude = waveAmplitude,
            WaveFrequency = waveFrequency,
            NoisePhase = noisePhase,
            NoiseStrength = noiseStrength,
            NoiseSeed = noiseSeed,
            MaxAlpha = maxAlpha,
            MinAlpha = minAlpha,
        };

        BloodFloodMeshBuilder.PopulateMesh(vertexHelper, GetPixelAdjustedRect(), meshParams);
    }

    public override Texture mainTexture
    {
        get
        {
            Sprite sprite = BloodDripUiHelper.WhiteSprite;
            return sprite != null ? sprite.texture : Texture2D.whiteTexture;
        }
    }
}
