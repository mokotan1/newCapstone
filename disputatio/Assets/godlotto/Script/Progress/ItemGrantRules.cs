using UnityEngine;

/// <summary>인벤토리 지급·레지스트리 포함 가능 여부 판단.</summary>
public static class ItemGrantRules
{
    public const int MinItemId = 1;
    public const int MaxItemId = 30;

    public static bool IsValidInventoryItem(Item item, out string skipReason)
    {
        skipReason = null;

        if (item == null)
        {
            skipReason = "null Item";
            return false;
        }

        if ((item.hideFlags & HideFlags.HideAndDontSave) != 0)
        {
            skipReason = $"{item.name}: HideAndDontSave";
            return false;
        }

        if (item.itemId < MinItemId || item.itemId > MaxItemId)
        {
            skipReason = $"{item.name}: itemId={item.itemId} (허용 범위 {MinItemId}~{MaxItemId})";
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.itemName))
        {
            skipReason = $"{item.name}: itemName 비어 있음";
            return false;
        }

        return true;
    }
}
