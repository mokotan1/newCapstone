using System;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 카탈로그 데이터 무결성 검사.
/// </summary>
public static class QuestCatalogValidator
{
    public static bool TryValidate(TutorialQuestCatalog catalog, out string errorMessage)
    {
        if (catalog == null)
        {
            errorMessage = "TutorialQuestCatalog is null.";
            return false;
        }

        return TryValidate(catalog.ToDefinitions(), out errorMessage);
    }

    public static bool TryValidate(IReadOnlyList<QuestDefinition> quests, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (quests == null || quests.Count == 0)
        {
            errorMessage = "Quest catalog is empty.";
            return false;
        }

        var questIds = new HashSet<string>(StringComparer.Ordinal);
        var stepIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinition quest = quests[i];
            if (quest == null)
            {
                errorMessage = $"Quest at index {i} is null.";
                return false;
            }

            if (!quest.HasValidId)
            {
                errorMessage = $"Quest at index {i} has an empty id.";
                return false;
            }

            if (!questIds.Add(quest.Id))
            {
                errorMessage = $"Duplicate quest id '{quest.Id}'.";
                return false;
            }

            if (!quest.HasPlayableSteps)
            {
                errorMessage = $"Quest '{quest.Id}' has no steps.";
                return false;
            }

            for (int stepIndex = 0; stepIndex < quest.Steps.Count; stepIndex++)
            {
                QuestStep step = quest.Steps[stepIndex];
                if (string.IsNullOrWhiteSpace(step.Id))
                {
                    errorMessage = $"Quest '{quest.Id}' step at index {stepIndex} has an empty id.";
                    return false;
                }

                if (!stepIds.Add(step.Id))
                {
                    errorMessage = $"Duplicate step id '{step.Id}'.";
                    return false;
                }
            }
        }

        return true;
    }
}
