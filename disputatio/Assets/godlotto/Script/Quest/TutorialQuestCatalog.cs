using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 퀘스트 정본 목록. <c>Resources/TutorialQuestCatalog</c>에 배치합니다.
/// </summary>
[CreateAssetMenu(fileName = "TutorialQuestCatalog", menuName = "Disputatio/Tutorial Quest Catalog")]
public class TutorialQuestCatalog : ScriptableObject
{
    public const string ResourcePath = "TutorialQuestCatalog";

    [SerializeField] private List<QuestDefinitionAsset> quests = new List<QuestDefinitionAsset>();

    public IReadOnlyList<QuestDefinitionAsset> QuestAssets => quests ?? (IReadOnlyList<QuestDefinitionAsset>)Array.Empty<QuestDefinitionAsset>();

    public int Count => quests?.Count ?? 0;

    public IReadOnlyList<QuestDefinition> ToDefinitions()
    {
        var definitions = new List<QuestDefinition>();
        if (quests == null)
            return definitions;

        for (int i = 0; i < quests.Count; i++)
        {
            QuestDefinitionAsset asset = quests[i];
            if (asset == null)
                continue;

            definitions.Add(asset.ToDefinition());
        }

        return definitions;
    }

#if UNITY_EDITOR
    public void ReplaceQuests(IReadOnlyList<QuestDefinitionAsset> nextQuests)
    {
        quests = new List<QuestDefinitionAsset>();
        if (nextQuests == null)
            return;

        for (int i = 0; i < nextQuests.Count; i++)
        {
            if (nextQuests[i] != null)
                quests.Add(nextQuests[i]);
        }
    }
#endif

    private static TutorialQuestCatalog _cached;

    internal static void ResetCacheForTest() => _cached = null;

    public static TutorialQuestCatalog GetOrCreate()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<TutorialQuestCatalog>(ResourcePath);
        return _cached;
    }
}
