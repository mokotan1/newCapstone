using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps visible world sprites in a single depth stack: lower world Y is rendered in front.
/// </summary>
public static class WorldSpriteDepthSorter2D
{
    private const string WorldSortingLayerName = "Default";
    private const int BackgroundOrder = -10000;
    private const int BaseOrder = 10000;
    private const float UnitsToOrder = 10f;

    private static readonly HashSet<string> SortableLayerNames = new HashSet<string>
    {
        // World depth sorter must not touch Ui/UiBackGround sprites — those layers
        // are an intentional choice by the scene author and own their own ordering.
        "Default"
    };

    public static void SortActiveSceneSprites()
    {
        int worldLayerId = SortingLayer.NameToID(WorldSortingLayerName);
        var renderers = Object.FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var sr in renderers)
        {
            if (!ShouldSort(sr))
                continue;

            sr.sortingLayerID = worldLayerId;
            sr.sortingOrder = IsBackgroundLike(sr)
                ? BackgroundOrder
                : BaseOrder - Mathf.RoundToInt(sr.transform.position.y * UnitsToOrder);
        }
    }

    private static bool ShouldSort(SpriteRenderer sr)
    {
        if (sr == null || !sr.enabled || !sr.gameObject.activeInHierarchy)
            return false;

        if (!SortableLayerNames.Contains(sr.sortingLayerName))
            return false;

        if (sr.color.a <= 0.01f)
            return false;

        if (sr.GetComponent<SnapTarget>() != null)
            return false;

        return true;
    }

    private static bool IsBackgroundLike(SpriteRenderer sr)
    {
        string objectName = sr.gameObject.name.ToLowerInvariant();
        return objectName.Contains("background")
            || objectName.Contains("floor");
    }
}
