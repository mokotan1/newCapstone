#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <c>Assets/godlotto/Item</c> 폴더의 Item SO를 <see cref="ItemRegistry"/>에 동기화합니다.
/// </summary>
public static class ItemRegistrySync
{
    const string RegistryAssetPath = "Assets/Resources/ItemRegistry.asset";
    const string IllustratedBibleAssetPath = ItemLookup.ProductionItemFolder + "/IllustratedBible.asset";

    [MenuItem("Disputatio/Inventory/Sync Item Registry")]
    public static void SyncFromProductionFolder()
    {
        EnsureIllustratedBibleAsset();

        string[] guids = AssetDatabase.FindAssets("t:Item", new[] { ItemLookup.ProductionItemFolder });
        var items = new List<Item>(guids.Length);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<Item>(path);
            if (item != null)
                items.Add(item);
        }

        items = items
            .OrderBy(x => x.itemId)
            .ThenBy(x => x.name)
            .ToList();

        ValidateUniqueIds(items);

        ItemRegistry registry = LoadOrCreateRegistryAsset();
        registry.ReplaceItems(items);
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ItemRegistrySync] {items.Count}개 Item을 {RegistryAssetPath}에 동기화했습니다.");
    }

    static void EnsureIllustratedBibleAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Item>(IllustratedBibleAssetPath);
        if (existing != null)
            return;

        var bible = ScriptableObject.CreateInstance<Item>();
        bible.itemId = 19;
        bible.itemName = "IllustratedBible";
        bible.itemDescription = "일러스트가 들어간 성경. 서재 책장 단서.";

        Sprite icon = LoadDefaultBibleIcon();
        if (icon != null)
            bible.icon = icon;

        AssetDatabase.CreateAsset(bible, IllustratedBibleAssetPath);
        Debug.Log($"[ItemRegistrySync] {IllustratedBibleAssetPath} (itemId=19)를 생성했습니다.");
    }

    static Sprite LoadDefaultBibleIcon()
    {
        var filterCard = AssetDatabase.LoadAssetAtPath<Item>(ItemLookup.ProductionItemFolder + "/FilterCard.asset");
        if (filterCard != null && filterCard.icon != null)
            return filterCard.icon;

        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/godlotto/Resources/BibleSpreadReference.png");
    }

    static ItemRegistry LoadOrCreateRegistryAsset()
    {
        var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(RegistryAssetPath);
        if (registry != null)
            return registry;

        EnsureResourcesFolder();
        registry = ScriptableObject.CreateInstance<ItemRegistry>();
        AssetDatabase.CreateAsset(registry, RegistryAssetPath);
        return registry;
    }

    static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }

    static void ValidateUniqueIds(IReadOnlyList<Item> items)
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item == null)
                continue;

            if (!seen.Add(item.itemId))
            {
                throw new System.InvalidOperationException(
                    $"중복 itemId={item.itemId} — {item.name}. ID를 고유하게 수정한 뒤 다시 동기화하세요.");
            }
        }
    }
}
#endif
