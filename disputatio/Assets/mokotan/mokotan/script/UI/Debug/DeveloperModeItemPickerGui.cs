using System.Collections.Generic;
using UnityEngine;

/// <summary>개발자 오버레이 IMGUI용 아이템 선택·지급 패널.</summary>
public sealed class DeveloperModeItemPickerGui
{
    readonly List<DeveloperModeItemCatalogEntry> catalogCache = new List<DeveloperModeItemCatalogEntry>();
    readonly List<DeveloperModeItemCatalogEntry> filteredEntries = new List<DeveloperModeItemCatalogEntry>();

    bool sectionExpanded = true;
    bool catalogDirty = true;
    string searchQuery = string.Empty;
    string quantityText = "1";
    int categoryFilterIndex;
    int selectedEntryIndex = -1;
    Vector2 itemListScroll;
    DeveloperModeItemSelectionGrantResult lastResult;

    static readonly DeveloperModeItemCategory[] CategoryFilters =
    {
        DeveloperModeItemCategory.All,
        DeveloperModeItemCategory.Key,
        DeveloperModeItemCategory.Consumable,
        DeveloperModeItemCategory.Material,
        DeveloperModeItemCategory.Seal,
        DeveloperModeItemCategory.Quest,
        DeveloperModeItemCategory.Other,
    };

    static readonly string[] CategoryFilterLabels =
    {
        "전체",
        "열쇠",
        "소비",
        "재료",
        "인장",
        "퀘스트",
        "기타",
    };

    public void Draw(DeveloperModeGuiStyles styles)
    {
        if (styles == null)
            return;

        if (!DeveloperModeItemGrantService.CanGrant)
            return;

        sectionExpanded = GUILayout.Toggle(sectionExpanded, "아이템 선택 지급", styles.ToggleButton);
        if (!sectionExpanded)
            return;

        RefreshCatalogIfNeeded();
        RefreshFilteredEntries();

        GUILayout.BeginVertical(styles.Box);
        GUILayout.Label("검색 (이름 / ID / 카테고리 / 설명)", styles.Label);
        searchQuery = GUILayout.TextField(searchQuery ?? string.Empty, styles.TextField);

        categoryFilterIndex = GUILayout.SelectionGrid(
            categoryFilterIndex,
            CategoryFilterLabels,
            4,
            styles.Button);

        GUILayout.Label($"목록 {filteredEntries.Count} / {catalogCache.Count}", styles.Label);
        itemListScroll = GUILayout.BeginScrollView(
            itemListScroll,
            styles.Box,
            GUILayout.Height(styles.ScaledHeight(140f)));
        for (int i = 0; i < filteredEntries.Count; i++)
        {
            DeveloperModeItemCatalogEntry entry = filteredEntries[i];
            if (entry == null)
                continue;

            bool isSelected = i == selectedEntryIndex;
            GUIStyle rowStyle = isSelected ? styles.Button : styles.Label;
            if (GUILayout.Button(entry.ListLabel, rowStyle))
                selectedEntryIndex = i;

            if (isSelected)
                GUILayout.Label(entry.ShortDescription, styles.Label);
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        GUILayout.Label("수량", styles.Label, GUILayout.Width(styles.ScaledWidth(36f)));
        quantityText = GUILayout.TextField(quantityText ?? "1", styles.TextField, GUILayout.Width(styles.ScaledWidth(48f)));
        if (GUILayout.Button("선택 아이템 지급", styles.Button, GUILayout.Width(styles.ScaledWidth(140f))))
            GrantSelectedEntry();
        GUILayout.EndHorizontal();

        GUILayout.Label("인벤토리는 스택 불가 — 동일 아이템은 1개만 지급됩니다.", styles.Label);

        if (lastResult != null)
            GUILayout.Label(lastResult.ToString(), styles.Label);
        else if (DeveloperModeItemGrantService.LastSelectionResult != null)
            GUILayout.Label(DeveloperModeItemGrantService.LastSelectionResult.ToString(), styles.Label);

        GUILayout.EndVertical();
    }

    public void InvalidateCatalog()
    {
        catalogDirty = true;
    }

    void RefreshCatalogIfNeeded()
    {
        if (!catalogDirty)
            return;

        catalogCache.Clear();
        catalogCache.AddRange(DeveloperModeItemGrantService.GetCatalogEntries());
        catalogDirty = false;
        selectedEntryIndex = -1;
    }

    void RefreshFilteredEntries()
    {
        filteredEntries.Clear();
        DeveloperModeItemCategory category = CategoryFilters[Mathf.Clamp(categoryFilterIndex, 0, CategoryFilters.Length - 1)];
        filteredEntries.AddRange(DeveloperModeItemCatalog.Filter(catalogCache, searchQuery, category));

        if (selectedEntryIndex >= filteredEntries.Count)
            selectedEntryIndex = filteredEntries.Count > 0 ? 0 : -1;
    }

    void GrantSelectedEntry()
    {
        if (selectedEntryIndex < 0 || selectedEntryIndex >= filteredEntries.Count)
        {
            lastResult = new DeveloperModeItemSelectionGrantResult
            {
                FailureReason = "지급할 아이템을 목록에서 선택해 주세요.",
            };
            return;
        }

        DeveloperModeItemCatalogEntry entry = filteredEntries[selectedEntryIndex];
        if (entry?.Item == null)
        {
            lastResult = new DeveloperModeItemSelectionGrantResult
            {
                FailureReason = "선택한 아이템 데이터가 유효하지 않습니다.",
            };
            return;
        }

        if (!TryParseQuantity(quantityText, out int quantity))
        {
            lastResult = new DeveloperModeItemSelectionGrantResult
            {
                ItemId = entry.ItemId,
                ItemName = entry.DisplayName,
                FailureReason = "수량은 1 이상의 정수여야 합니다.",
            };
            return;
        }

        lastResult = DeveloperModeItemGrantService.GrantSelectedItem(entry.Item, quantity);
    }

    internal static bool TryParseQuantity(string raw, out int quantity)
    {
        quantity = DeveloperModeItemGrantService.MinGrantQuantity;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!int.TryParse(raw.Trim(), out int parsed))
            return false;

        if (parsed < DeveloperModeItemGrantService.MinGrantQuantity)
            return false;

        quantity = DeveloperModeItemGrantService.ClampGrantQuantity(parsed);
        return true;
    }
}
