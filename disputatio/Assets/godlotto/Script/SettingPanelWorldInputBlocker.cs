using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Blocks world-space 2D collider input while modal setting UI is open.
/// </summary>
public static class SettingPanelWorldInputBlocker
{
    private static readonly List<Collider2D> DisabledColliders = new List<Collider2D>();
    private static bool isBlocking;

    public static bool IsBlocking => isBlocking;

    public static void Begin(GameObject allowedRoot)
    {
        if (isBlocking)
            End();

        Transform allowedTransform = allowedRoot != null ? allowedRoot.transform : null;
        Collider2D[] colliders = Object.FindObjectsByType<Collider2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (Collider2D collider in colliders)
        {
            if (collider == null || !collider.enabled)
                continue;

            if (allowedTransform != null && collider.transform.IsChildOf(allowedTransform))
                continue;

            collider.enabled = false;
            DisabledColliders.Add(collider);
        }

        isBlocking = true;
    }

    public static void End()
    {
        for (int i = 0; i < DisabledColliders.Count; i++)
        {
            Collider2D collider = DisabledColliders[i];
            if (collider != null)
                collider.enabled = true;
        }

        DisabledColliders.Clear();
        isBlocking = false;
    }
}
