using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 트래커 단일 단계 행의 시각 상태.
/// </summary>
public sealed class QuestTrackerStepRowView : MonoBehaviour
{
    [SerializeField] Image markBackground;
    [SerializeField] TextMeshProUGUI markLabel;
    [SerializeField] TextMeshProUGUI stepText;
    [SerializeField] Image strikethroughLine;
    [SerializeField] QuestTrackerPulseDot pulseDot;
    [SerializeField] Image pulseGraphic;

    public QuestStepPhase CurrentPhase { get; private set; }

    public void Bind(Image markBg, TextMeshProUGUI mark, TextMeshProUGUI text, Image strike, Image pulse)
    {
        markBackground = markBg;
        markLabel = mark;
        stepText = text;
        strikethroughLine = strike;
        pulseGraphic = pulse;
        if (pulseGraphic != null)
        {
            pulseDot = pulseGraphic.GetComponent<QuestTrackerPulseDot>();
            if (pulseDot == null)
                pulseDot = pulseGraphic.gameObject.AddComponent<QuestTrackerPulseDot>();
            pulseDot.Bind(pulseGraphic);
        }
    }

    public void Apply(QuestStepPhase phase, string text)
    {
        CurrentPhase = phase;
        if (stepText != null)
        {
            stepText.text = text ?? string.Empty;
            stepText.fontStyle = FontStyles.Normal;
        }

        switch (phase)
        {
            case QuestStepPhase.Completed:
                ApplyCompletedVisuals(text);
                break;
            case QuestStepPhase.Active:
                ApplyActiveVisuals(text);
                break;
            default:
                ApplyPendingVisuals(text);
                break;
        }
    }

    void ApplyPendingVisuals(string text)
    {
        if (stepText != null)
            stepText.color = QuestTrackerStylePalette.InkDim;

        if (markBackground != null)
        {
            markBackground.color = new Color(0f, 0f, 0f, 0.12f);
            markBackground.transform.localScale = Vector3.one;
        }

        if (markLabel != null)
        {
            markLabel.text = string.Empty;
            markLabel.color = Color.clear;
        }

        SetStrikeVisible(false);
        SetPulseVisible(false);
    }

    void ApplyActiveVisuals(string text)
    {
        if (stepText != null)
            stepText.color = QuestTrackerStylePalette.Ink;

        if (markBackground != null)
        {
            markBackground.color = new Color(QuestTrackerStylePalette.BloodBright.r, QuestTrackerStylePalette.BloodBright.g, QuestTrackerStylePalette.BloodBright.b, 0.18f);
        }

        if (markLabel != null)
        {
            markLabel.text = string.Empty;
            markLabel.color = Color.clear;
        }

        SetStrikeVisible(false);
        SetPulseVisible(true);
    }

    void ApplyCompletedVisuals(string text)
    {
        if (stepText != null)
            stepText.color = QuestTrackerStylePalette.InkDone;

        if (markBackground != null)
        {
            Color bg = QuestTrackerStylePalette.Done;
            bg.a = 0.12f;
            markBackground.color = bg;
        }

        if (markLabel != null)
        {
            markLabel.text = "✓";
            markLabel.color = QuestTrackerStylePalette.Done;
        }

        SetStrikeVisible(false);
        SetPulseVisible(false);
    }

    void SetStrikeVisible(bool visible)
    {
        if (strikethroughLine != null)
            strikethroughLine.enabled = visible;
    }

    void SetPulseVisible(bool visible)
    {
        if (pulseGraphic != null)
            pulseGraphic.enabled = visible;
    }
}
