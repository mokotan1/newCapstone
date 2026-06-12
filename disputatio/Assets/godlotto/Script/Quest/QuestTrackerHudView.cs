using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// <see cref="QuestTrackerState"/> 스냅샷을 HUD 위젯에 반영한다.
/// </summary>
public sealed class QuestTrackerHudView : MonoBehaviour
{
    Image leftAccent;
    TextMeshProUGUI questNameText;
    RectTransform stepsContainer;
    TextMeshProUGUI hintText;
    TextMeshProUGUI clearedBannerText;
    CanvasGroup canvasGroup;
    RectTransform rootRect;

    readonly List<QuestTrackerStepRowView> stepRows = new List<QuestTrackerStepRowView>();

    public CanvasGroup CanvasGroup => canvasGroup;
    public RectTransform RootRect => rootRect;
    public IReadOnlyList<QuestTrackerStepRowView> StepRows => stepRows;

    public void Bind(
        Image accent,
        TextMeshProUGUI headerText,
        TextMeshProUGUI questName,
        RectTransform stepsRoot,
        TextMeshProUGUI hint,
        TextMeshProUGUI clearedBanner,
        CanvasGroup group,
        RectTransform root)
    {
        leftAccent = accent;
        questNameText = questName;
        stepsContainer = stepsRoot;
        hintText = hint;
        clearedBannerText = clearedBanner;
        canvasGroup = group;
        rootRect = root;
    }

    public void RefreshFromState(QuestTrackerState state)
    {
        if (state == null || state.CurrentQuest == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        QuestDefinition quest = state.CurrentQuest;
        questNameText.text = quest.Title;
        hintText.text = quest.Hint;
        hintText.gameObject.SetActive(!state.IsQuestCleared && !string.IsNullOrWhiteSpace(quest.Hint));

        EnsureStepRows(quest.Steps.Count);
        for (int i = 0; i < quest.Steps.Count; i++)
        {
            QuestStepPhase phase = state.GetStepPhase(i);
            stepRows[i].Apply(phase, quest.Steps[i].Text);
        }

        SetClearedVisuals(state.IsQuestCleared);
    }

    public void SetClearedVisuals(bool cleared)
    {
        if (leftAccent != null)
            leftAccent.color = cleared ? QuestTrackerStylePalette.Done : QuestTrackerStylePalette.Blood;

        if (clearedBannerText != null)
            clearedBannerText.gameObject.SetActive(cleared);

        if (hintText != null && cleared)
            hintText.gameObject.SetActive(false);
    }

    public QuestStepPhase GetStepPhaseAt(int index)
    {
        if (index < 0 || index >= stepRows.Count)
            return QuestStepPhase.Pending;

        return stepRows[index].CurrentPhase;
    }

    void EnsureStepRows(int count)
    {
        while (stepRows.Count < count)
        {
            QuestTrackerStepRowView row = QuestTrackerHudFactory.CreateStepRow(stepsContainer, gameObject.layer);
            stepRows.Add(row);
        }

        for (int i = 0; i < stepRows.Count; i++)
            stepRows[i].gameObject.SetActive(i < count);
    }
}
