using UnityEngine;

/// <summary>
/// Corridor / hall jumpscare game-over backdrop and Retry button layout (world-space, main camera).
/// </summary>
public static class JumpscareGameOverLayout
{
    public const int BackdropSortingOrder = 32766;
    public const int RetrySortingOrder = 32767;
    public const string UiSortingLayerName = "Ui";

    public const string CenterDarkOverlayObjectName = "JumpscareCenterDarkOverlay";
    public const string SpecialCenterDarkOverlayObjectName = "SpecialJumpscareCenterDarkOverlay";

    public static void DeactivateCenterDarkOverlayCirclesInScene()
    {
        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            string objectName = renderer.gameObject.name;
            if (objectName != CenterDarkOverlayObjectName
                && objectName != SpecialCenterDarkOverlayObjectName)
                continue;

            renderer.enabled = false;
            renderer.gameObject.SetActive(false);
        }
    }

    public static void FitGameOverScreen(GameObject gameOverRoot, GameObject explicitRetry = null)
    {
        if (gameOverRoot == null)
            return;

        if (explicitRetry != null)
        {
            SpriteRenderer retryRenderer = explicitRetry.GetComponent<SpriteRenderer>();
            if (retryRenderer != null)
                PrepareRetryRenderer(retryRenderer);
        }

        foreach (SpriteRenderer renderer in gameOverRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || renderer.gameObject == gameOverRoot)
                continue;

            if (!string.Equals(renderer.gameObject.name, "Retry", System.StringComparison.Ordinal))
                continue;

            PrepareRetryRenderer(renderer);
        }

        ApplySorting(gameOverRoot);
    }

    public static void PrepareRetryRenderer(SpriteRenderer retryRenderer)
    {
        if (retryRenderer == null)
            return;

        retryRenderer.enabled = true;
    }

    public static void ApplySorting(GameObject gameOverRoot)
    {
        if (gameOverRoot == null)
            return;

        int sortingLayerId = ResolveUiSortingLayerId();

        SpriteRenderer backdrop = gameOverRoot.GetComponent<SpriteRenderer>();
        if (backdrop != null)
        {
            backdrop.sortingLayerID = sortingLayerId;
            backdrop.sortingOrder = BackdropSortingOrder;
        }

        foreach (SpriteRenderer child in gameOverRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (child == null || child == backdrop)
                continue;

            child.sortingLayerID = sortingLayerId;
            child.sortingOrder = RetrySortingOrder;
        }
    }

    public static int ResolveUiSortingLayerId()
    {
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == UiSortingLayerName)
                return layer.id;
        }

        return 0;
    }
}
