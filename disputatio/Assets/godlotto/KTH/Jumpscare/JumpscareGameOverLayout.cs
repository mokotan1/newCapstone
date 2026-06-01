using UnityEngine;

/// <summary>
/// Corridor / hall jumpscare game-over backdrop and Retry button layout (world-space, main camera).
/// </summary>
public static class JumpscareGameOverLayout
{
    public const float OverlayPlaneZOffsetFromCamera = 1f;
    public const int BackdropSortingOrder = 32766;
    public const int RetrySortingOrder = 32767;
    public const string UiSortingLayerName = "Ui";

    public const string CenterDarkOverlayObjectName = "JumpscareCenterDarkOverlay";
    public const string SpecialCenterDarkOverlayObjectName = "SpecialJumpscareCenterDarkOverlay";

    /// <summary>Hall_playerble / SpecialJumpscareManager GameOver → Retry (scene-authored).</summary>
    public static readonly Vector3 HallPlayableRetryLocalPosition = new Vector3(0.008f, -0.203f, 0f);

    public static readonly Vector3 HallPlayableRetryLocalScale = new Vector3(0.2f, 0.1f, 1f);

    /// <summary>Matches Hall_playerble GameOver backdrop scale at orthographic size 540.</summary>
    public static readonly Vector3 HallPlayableGameOverLocalScale = new Vector3(1920f, 1080f, 1f);

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

        FitBackdrop(gameOverRoot.GetComponent<SpriteRenderer>());

        if (explicitRetry != null)
        {
            SpriteRenderer retryRenderer = explicitRetry.GetComponent<SpriteRenderer>();
            if (retryRenderer != null)
                ApplyHallPlayableRetryLayout(retryRenderer);
        }

        foreach (SpriteRenderer renderer in gameOverRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer == null || renderer.gameObject == gameOverRoot)
                continue;

            if (!string.Equals(renderer.gameObject.name, "Retry", System.StringComparison.Ordinal))
                continue;

            ApplyHallPlayableRetryLayout(renderer);
        }

        ApplySorting(gameOverRoot);
    }

    public static void FitBackdrop(SpriteRenderer backdrop)
    {
        if (backdrop == null)
            return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
            return;

        Vector3 camPos = mainCam.transform.position;
        float planeZ = camPos.z + OverlayPlaneZOffsetFromCamera;
        backdrop.transform.position = new Vector3(camPos.x, camPos.y, planeZ);
        backdrop.transform.localRotation = Quaternion.identity;

        float worldHeight = mainCam.orthographicSize * 2f;
        float worldWidth = worldHeight * mainCam.aspect;

        if (backdrop.sprite == null)
            return;

        Vector2 spriteSize = backdrop.sprite.bounds.size;
        if (Mathf.Approximately(mainCam.orthographicSize, 540f))
        {
            backdrop.transform.localScale = HallPlayableGameOverLocalScale;
        }
        else
        {
            backdrop.transform.localScale = new Vector3(
                worldWidth / spriteSize.x,
                worldHeight / spriteSize.y,
                1f);
        }
    }

    /// <summary>Same local transform as Hall_playerble Retry under GameOver.</summary>
    public static void ApplyHallPlayableRetryLayout(SpriteRenderer retryRenderer)
    {
        if (retryRenderer == null)
            return;

        retryRenderer.enabled = true;

        Transform retryTransform = retryRenderer.transform;
        retryTransform.localRotation = Quaternion.identity;
        retryTransform.localPosition = HallPlayableRetryLocalPosition;
        retryTransform.localScale = HallPlayableRetryLocalScale;
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
