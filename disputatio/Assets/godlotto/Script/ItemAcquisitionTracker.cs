using System.Collections.Generic;
using System.Text;
using Fungus;
using UnityEngine;

/// <summary>
/// Fungus 글로벌 정수 변수 <see cref="FungusVariableKey"/>에 아이템 습득 비트를 OR 저장하고,
/// 체셔 등 AI용 진행 요약 문자열을 생성합니다.
/// <para>인벤토리에서 RemoveItem 해도 비트는 유지됩니다(영구 습득 이력).</para>
/// </summary>
public static class ItemAcquisitionTracker
{
    public const string FungusVariableKey = "AcquiredItemsMask";

    private const int MinId = 1;
    private const int MaxId = 30;

    private static readonly Dictionary<int, string> DisplayNames = new Dictionary<int, string>();

    private static readonly AcquisitionBoolMapping[] AcquisitionBoolMappings =
    {
        new AcquisitionBoolMapping(FungusVariableKeys.GetBottle, 1),
        new AcquisitionBoolMapping(FungusVariableKeys.GetFood, 2),
        new AcquisitionBoolMapping(FungusVariableKeys.GetFilterCard, 4),
        new AcquisitionBoolMapping(FungusVariableKeys.GetBookmarkMirror, 17),
        new AcquisitionBoolMapping(FungusVariableKeys.GetBibleCommentary, 21),
        new AcquisitionBoolMapping(FungusVariableKeys.HaveMaidKey, 8),
        new AcquisitionBoolMapping(FungusVariableKeys.HaveBasementKey, 12),
        new AcquisitionBoolMapping(FungusVariableKeys.HasBible, 19),
    };

    #region Public API

    /// <summary>아이템 습득을 비트마스크에 기록합니다.</summary>
    public static void MarkAcquired(Flowchart flowchart, Item item)
    {
        if (flowchart == null || item == null)
            return;

        if (!IsValidId(item.itemId))
        {
            GameLog.LogWarning($"[ItemAcquisition] itemId 범위 밖({item.itemId}): {item.name}");
            return;
        }

        CacheDisplayName(item.itemId, item.itemName);
        SetBit(flowchart, item.itemId);
        SetLinkedAcquisitionBool(flowchart, item.itemId);
    }

    /// <summary>해당 아이템이 한 번이라도 습득되었는지 확인합니다.</summary>
    public static bool IsAcquired(Flowchart flowchart, int itemId)
    {
        if (flowchart == null || !IsValidId(itemId))
            return false;

        return (ReadMask(flowchart) & (1 << itemId)) != 0;
    }

    /// <summary>체셔 등 AI 프롬프트에 주입할 진행 요약 문자열을 만듭니다.</summary>
    public static string BuildPromptSection(Flowchart flowchart)
    {
        if (flowchart == null)
            return string.Empty;

        int mask = ReadMask(flowchart);
        if (mask == 0)
            return "\n\n[진행] 아직 획득한 단서 아이템이 없습니다.";

        var sb = new StringBuilder(256);
        sb.Append("\n\n[진행] 획득 아이템: ");
        bool first = true;

        for (int id = MinId; id <= MaxId; id++)
        {
            if ((mask & (1 << id)) == 0)
                continue;

            if (!first)
                sb.Append(", ");
            first = false;

            string name = DisplayNames.TryGetValue(id, out var n) ? n : $"#{id}";
            sb.Append(name).Append('(').Append(id).Append(')');
        }

        sb.Append("\n[진행 안내] 위 목록은 플레이어가 한 번이라도 습득한 아이템입니다. 인벤토리에서 소비했어도 습득 이력은 유지됩니다.");
        return sb.ToString();
    }

    /// <summary>프로젝트에 로드된 모든 Item SO에서 id -> 표시명 캐시를 채웁니다.</summary>
    public static void WarmupDisplayNameCache()
    {
        var allItems = ItemLookup.GetAllItems();
        for (int i = 0; i < allItems.Count; i++)
        {
            Item item = allItems[i];
            if (item != null)
                CacheDisplayName(item.itemId, item.itemName);
        }
    }

    /// <summary>기존 Fungus bool 습득 플래그를 비트마스크로 1회 이전합니다.</summary>
    public static void MigrateLegacyBools(Flowchart flowchart)
    {
        if (flowchart == null)
            return;

        for (int i = 0; i < AcquisitionBoolMappings.Length; i++)
        {
            var m = AcquisitionBoolMappings[i];
            if (flowchart.GetBooleanVariable(m.BoolKey))
                SetBit(flowchart, m.ItemId);
        }
    }

    #endregion

    #region Internal Helpers

    private static bool IsValidId(int itemId)
    {
        return itemId >= MinId && itemId <= MaxId;
    }

    private static int ReadMask(Flowchart flowchart)
    {
        return flowchart.GetIntegerVariable(FungusVariableKey);
    }

    private static void SetBit(Flowchart flowchart, int itemId)
    {
        int mask = ReadMask(flowchart);
        int updated = mask | (1 << itemId);
        if (updated != mask)
            flowchart.SetIntegerVariable(FungusVariableKey, updated);
    }

    private static void SetLinkedAcquisitionBool(Flowchart flowchart, int itemId)
    {
        for (int i = 0; i < AcquisitionBoolMappings.Length; i++)
        {
            var m = AcquisitionBoolMappings[i];
            if (m.ItemId == itemId)
                flowchart.SetBooleanVariable(m.BoolKey, true);
        }
    }

    private static void CacheDisplayName(int itemId, string displayName)
    {
        if (IsValidId(itemId) && !string.IsNullOrEmpty(displayName))
            DisplayNames[itemId] = displayName;
    }

    #endregion

    #region Legacy Migration Types

    private readonly struct AcquisitionBoolMapping
    {
        public readonly string BoolKey;
        public readonly int ItemId;

        public AcquisitionBoolMapping(string boolKey, int itemId)
        {
            BoolKey = boolKey;
            ItemId = itemId;
        }
    }

    #endregion
}
