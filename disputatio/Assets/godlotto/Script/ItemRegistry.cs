using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 인벤토리 <see cref="Item"/> 정본 목록. <c>Resources/ItemRegistry</c>에 배치합니다.
/// </summary>
[CreateAssetMenu(fileName = "ItemRegistry", menuName = "Inventory/Item Registry")]
public class ItemRegistry : ScriptableObject
{
    public const string ResourcePath = "ItemRegistry";

    [SerializeField] private List<Item> items = new List<Item>();

    public IReadOnlyList<Item> Items => items;

    public int Count => items.Count;

    public Item FindById(int itemId)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item != null && item.itemId == itemId)
                return item;
        }

        return null;
    }

#if UNITY_EDITOR
    public void ReplaceItems(IReadOnlyList<Item> nextItems)
    {
        items.Clear();
        if (nextItems == null)
            return;

        for (int i = 0; i < nextItems.Count; i++)
        {
            if (nextItems[i] != null)
                items.Add(nextItems[i]);
        }
    }
#endif

    private static ItemRegistry _cached;

    internal static void ResetCacheForTest()
    {
        _cached = null;
    }

    public static ItemRegistry GetOrCreate()
    {
        if (_cached != null)
            return _cached;

        _cached = Resources.Load<ItemRegistry>(ResourcePath);
        return _cached;
    }
}
