using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared policy + execution for wiping DontDestroyOnLoad (DDOL) gameplay roots
/// (Fungus globals, quest tracker systems, etc.) whenever the player returns to
/// the main menu. Every return-to-menu entry point (in-game settings panel, end
/// scene, standalone/popup setting UI, quick setting buttons) must apply the
/// exact same preservation rule so a second New Game never inherits stale
/// Fungus or quest state. Extracted from <see cref="InGameSettingsPanel"/> so
/// the policy has a single owner (DRY) instead of N near-identical copies.
/// </summary>
public static class DontDestroyGameplayCleanup
{
    /// <summary>
    /// True if a DontDestroyOnLoad root must survive a return-to-menu cleanup.
    /// Only the caller's own root (so it can safely finish its own
    /// coroutine/scene load) and <see cref="GlobalSettingManager"/> (BGM/SFX/
    /// Fullscreen/Resolution) are preserved. Fungus globals
    /// (GlobalVariables/Variablemanager) and quest tracker systems are
    /// intentionally NOT preserved — they must reset for the next New Game.
    /// </summary>
    public static bool ShouldPreserveRoot(GameObject root, GameObject objectToPreserve)
    {
        if (root == null)
            return false;

        if (root == objectToPreserve)
            return true;

        return root.GetComponent<GlobalSettingManager>() != null;
    }

    /// <summary>
    /// Destroys every root in <paramref name="roots"/> that
    /// <see cref="ShouldPreserveRoot"/> does not protect. Kept separate from
    /// <see cref="CleanupGameplayRoots"/> (which discovers roots via
    /// DontDestroyOnLoad, a Play-Mode-only API) so EditMode tests can exercise
    /// the destruction policy against synthetic roots and an injected
    /// <paramref name="destroyRoot"/> callback (e.g. DestroyImmediate).
    /// </summary>
    public static void DestroyUnpreservedRoots(
        IList<GameObject> roots,
        GameObject objectToPreserve,
        Action<GameObject> destroyRoot = null)
    {
        if (roots == null)
            return;

        Action<GameObject> destroy = destroyRoot ?? DefaultDestroy;
        for (int i = 0; i < roots.Count; i++)
        {
            GameObject root = roots[i];
            if (ShouldPreserveRoot(root, objectToPreserve))
                continue;

            destroy(root);
        }
    }

    /// <summary>
    /// Finds every current DontDestroyOnLoad root GameObject. Runtime-only:
    /// relies on <see cref="GameObject.DontDestroyOnLoad"/>, which is a no-op
    /// outside Play Mode.
    /// </summary>
    public static List<GameObject> FindDontDestroyOnLoadRoots()
    {
        var temp = new GameObject("TempSceneProbe");
        UnityEngine.Object.DontDestroyOnLoad(temp);
        var ddScene = temp.scene;
        UnityEngine.Object.Destroy(temp);

        var roots = new List<GameObject>();
        ddScene.GetRootGameObjects(roots);
        return roots;
    }

    /// <summary>
    /// Convenience combining <see cref="FindDontDestroyOnLoadRoots"/> and
    /// <see cref="DestroyUnpreservedRoots"/> for production return-to-menu call
    /// sites. Runtime-only; call from gameplay code, not EditMode tests.
    /// </summary>
    public static void CleanupGameplayRoots(GameObject objectToPreserve, Action<GameObject> destroyRoot = null)
    {
        DestroyUnpreservedRoots(FindDontDestroyOnLoadRoots(), objectToPreserve, destroyRoot);
    }

    private static void DefaultDestroy(GameObject root)
    {
        UnityEngine.Object.Destroy(root);
    }
}
