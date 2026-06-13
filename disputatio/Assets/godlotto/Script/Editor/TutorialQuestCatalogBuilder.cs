#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 튜토리얼 퀘스트 SO와 <see cref="TutorialQuestCatalog"/>를 생성·갱신합니다.
/// </summary>
public static class TutorialQuestCatalogBuilder
{
    const string QuestFolder = "Assets/godlotto/Quest";
    const string CatalogAssetPath = "Assets/Resources/TutorialQuestCatalog.asset";
    const string LightQuestAssetPath = QuestFolder + "/Tutorial_LightTheManor.asset";
    const string BottleQuestAssetPath = QuestFolder + "/Tutorial_BottleKey.asset";

    [MenuItem("Disputatio/Quest/Build Tutorial Quest Catalog")]
    public static void BuildTutorialQuestCatalog()
    {
        EnsureFolder("Assets/godlotto", "Quest");
        EnsureFolder("Assets", "Resources");

        QuestDefinitionAsset lightQuest = LoadOrCreateQuest(
            LightQuestAssetPath,
            TutorialQuestIds.LightTheManor,
            "저택에 불을 밝혀라",
            "불이 없으면 핏자국이 보이지 않는다…",
            new[]
            {
                new QuestStepData(TutorialQuestIds.LightTheManorSteps.GoKitchen, "주방으로 이동한다"),
                new QuestStepData(TutorialQuestIds.LightTheManorSteps.RaiseBreaker, "다용도실 차단기를 올린다"),
                new QuestStepData(TutorialQuestIds.LightTheManorSteps.InspectHall, "불 켜진 복도를 살핀다"),
            });

        QuestDefinitionAsset bottleQuest = LoadOrCreateQuest(
            BottleQuestAssetPath,
            TutorialQuestIds.BottleKey,
            "병 속 열쇠를 꺼내라",
            "물이 차오르자 열쇠가 떠오른다.",
            new[]
            {
                new QuestStepData(TutorialQuestIds.BottleKeySteps.FindBottle, "화분 속 병을 발견한다"),
                new QuestStepData(TutorialQuestIds.BottleKeySteps.FillBottle, "싱크대에서 병에 물을 채운다"),
                new QuestStepData(TutorialQuestIds.BottleKeySteps.TakeKey, "떠오른 열쇠를 집는다"),
            });

        TutorialQuestCatalog catalog = LoadOrCreateCatalog();
        catalog.ReplaceQuests(new List<QuestDefinitionAsset> { lightQuest, bottleQuest });
        EditorUtility.SetDirty(lightQuest);
        EditorUtility.SetDirty(bottleQuest);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!QuestCatalogValidator.TryValidate(catalog, out string error))
            Debug.LogError($"[TutorialQuestCatalogBuilder] Validation failed after build: {error}");
        else
            Debug.Log("[TutorialQuestCatalogBuilder] Tutorial quest catalog built and validated.");
    }

    static QuestDefinitionAsset LoadOrCreateQuest(
        string assetPath,
        string questId,
        string title,
        string hint,
        IReadOnlyList<QuestStepData> steps)
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinitionAsset>(assetPath);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestDefinitionAsset>();
            AssetDatabase.CreateAsset(quest, assetPath);
        }

        quest.SetContentForEditor(questId, title, hint, steps);
        return quest;
    }

    static TutorialQuestCatalog LoadOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<TutorialQuestCatalog>(CatalogAssetPath);
        if (catalog != null)
            return catalog;

        catalog = ScriptableObject.CreateInstance<TutorialQuestCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        return catalog;
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
