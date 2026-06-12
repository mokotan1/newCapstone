using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 활성 단계 옆 붉은 펄스 점.
/// </summary>
public sealed class QuestTrackerPulseDot : MonoBehaviour
{
    [SerializeField] Image graphic;

    float phase;

    void Awake()
    {
        if (graphic == null)
            graphic = GetComponent<Image>();
    }

    void Update()
    {
        if (graphic == null || !isActiveAndEnabled)
            return;

        phase += Time.unscaledDeltaTime * (Mathf.PI * 2f / 1.4f);
        float alpha = Mathf.Lerp(0.3f, 1f, (Mathf.Sin(phase) + 1f) * 0.5f);
        Color color = QuestTrackerStylePalette.BloodBright;
        color.a = alpha;
        graphic.color = color;
    }

    public void Bind(Image target)
    {
        graphic = target;
    }
}
