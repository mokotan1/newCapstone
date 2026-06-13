using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// <see cref="DeveloperModeController.CanUseDeveloperModeRuntime"/> + 개발자 모드에서만 <see cref="Item"/>을 인벤토리에 지급합니다.
/// </summary>
public static class DeveloperModeItemGrantService
{
    public const int MinGrantableItemId = ItemGrantRules.MinItemId;
    public const int MaxGrantableItemId = ItemGrantRules.MaxItemId;
    public const int MaxDeveloperInventorySlots = 30;
    public const int MinGrantQuantity = 1;
    public const int MaxGrantQuantity = 99;

    public static DeveloperModeItemGrantReport LastReport { get; private set; }
    public static DeveloperModeItemSelectionGrantResult LastSelectionResult { get; private set; }

    public static bool CanGrant =>
        DeveloperModeController.CanUseDeveloperModeRuntime && DeveloperModeController.IsDeveloperModeEnabled;

    public static List<DeveloperModeItemCatalogEntry> GetCatalogEntries()
    {
        if (!CanGrant)
            return new List<DeveloperModeItemCatalogEntry>();

        return DeveloperModeItemCatalog.BuildGrantableEntries(ItemLookup.GetAllItems());
    }

    public static int ClampGrantQuantity(int quantity)
    {
        return Mathf.Clamp(quantity, MinGrantQuantity, MaxGrantQuantity);
    }

    public static DeveloperModeItemSelectionGrantResult GrantSelectedItem(Item item, int quantity)
    {
        int requestedQuantity = ClampGrantQuantity(quantity);
        var result = new DeveloperModeItemSelectionGrantResult
        {
            RequestedQuantity = requestedQuantity,
        };

        if (item != null)
        {
            result.ItemId = item.itemId;
            result.ItemName = string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        }

        if (!CanGrant)
        {
            result.WasBlockedByDevMode = true;
            result.FailureReason = "Debug 빌드가 아니거나 개발자 모드가 꺼져 있습니다.";
            LastSelectionResult = result;
            GameLog.LogWarning($"[DeveloperModeItemGrant] {result}");
            return result;
        }

        if (!IsGrantableItem(item, out string skipReason))
        {
            result.FailureReason = string.IsNullOrEmpty(skipReason) ? "유효하지 않은 아이템입니다." : skipReason;
            LastSelectionResult = result;
            GameLog.LogWarning($"[DeveloperModeItemGrant] {result}");
            return result;
        }

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            result.FailureReason = "InventoryManager.Instance가 null입니다.";
            result.FailedQuantity = requestedQuantity;
            LastSelectionResult = result;
            GameLog.LogWarning($"[DeveloperModeItemGrant] {result}");
            return result;
        }

        inventory.EnsureDeveloperSlotCapacity(
            Mathf.Min(inventory.Items.Count + 1, MaxDeveloperInventorySlots));

        for (int i = 0; i < requestedQuantity; i++)
        {
            if (inventory.TryAddItemForDeveloperMode(item))
            {
                result.GrantedQuantity++;
                continue;
            }

            if (inventory.Items.Contains(item))
                result.SkippedDuplicateQuantity++;
            else
                result.FailedQuantity++;
        }

        result.Succeeded = result.GrantedQuantity > 0;
        if (!result.Succeeded && string.IsNullOrEmpty(result.FailureReason))
        {
            if (result.SkippedDuplicateQuantity > 0)
                result.FailureReason = "이미 인벤토리에 보유 중입니다.";
            else
                result.FailureReason = "인벤토리에 추가할 수 없습니다.";
        }

        LastSelectionResult = result;
        GameLog.Log($"[DeveloperModeItemGrant] {result}");
        return result;
    }

    public static DeveloperModeItemGrantReport GrantAllItems()
    {
        var report = new DeveloperModeItemGrantReport();

        if (!CanGrant)
        {
            report.WasBlockedByDevMode = true;
            LastReport = report;
            GameLog.LogWarning("[DeveloperModeItemGrant] 거부: 개발자 모드 런타임이 허용되지 않거나 개발자 모드가 꺼져 있습니다.");
            return report;
        }

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            report.FailedCount = 1;
            LastReport = report;
            GameLog.LogWarning("[DeveloperModeItemGrant] InventoryManager.Instance가 null입니다.");
            return report;
        }

        List<Item> candidates = CollectGrantableItems(ItemLookup.GetAllItems(), report);
        report.CandidateCount = candidates.Count;

        int targetSlotCount = Mathf.Min(Mathf.Max(candidates.Count, inventory.Items.Count), MaxDeveloperInventorySlots);
        inventory.EnsureDeveloperSlotCapacity(targetSlotCount);

        var owned = new HashSet<Item>(inventory.Items);
        foreach (Item item in candidates)
        {
            if (owned.Contains(item))
            {
                report.SkippedDuplicateCount++;
                continue;
            }

            if (inventory.TryAddItemForDeveloperMode(item))
            {
                report.GrantedCount++;
                owned.Add(item);
            }
            else
            {
                report.FailedCount++;
                GameLog.LogWarning(
                    $"[DeveloperModeItemGrant] 지급 실패: {item.itemName} (id={item.itemId}, asset={item.name})");
            }
        }

        LastReport = report;
        GameLog.Log($"[DeveloperModeItemGrant] 완료 — {report}");
        return report;
    }

    internal static List<Item> CollectGrantableItems(IEnumerable<Item> source, DeveloperModeItemGrantReport report)
    {
        var grantable = new List<Item>();
        var seenIds = new HashSet<int>();

        if (source == null)
            return grantable;

        foreach (Item item in source)
        {
            if (!IsGrantableItem(item, out string skipReason))
            {
                report.SkippedInvalidCount++;
                if (!string.IsNullOrEmpty(skipReason))
                    GameLog.LogWarning($"[DeveloperModeItemGrant] 무효 아이템 스킵: {skipReason}");
                continue;
            }

            if (!seenIds.Add(item.itemId))
            {
                report.SkippedInvalidCount++;
                GameLog.LogWarning(
                    $"[DeveloperModeItemGrant] 중복 itemId 스킵: id={item.itemId}, asset={item.name}");
                continue;
            }

            grantable.Add(item);
        }

        grantable.Sort((a, b) => a.itemId.CompareTo(b.itemId));
        return grantable;
    }

    internal static bool IsGrantableItem(Item item, out string skipReason)
    {
        return ItemGrantRules.IsValidInventoryItem(item, out skipReason);
    }
}
