using System;
using Godlotto.Interaction;
using UnityEngine;

/// <summary>
/// Hall_playerble hub routing without Fungus Flowchart.
/// Preserves CorridorEntrance hub semantics: never reload Hall_animate from IsPlayedAnimation.
/// </summary>
public sealed class HallPlayableController : MonoBehaviour
{
    public const string InteractionRight = "right";
    public const string InteractionLeft = "left";
    public const string InteractionStair = "stair";
    public const string InteractionBasement = "basement";
    public const string InteractionMap = "map";

    [SerializeField] bool enableDebugLogging;

    public void OnInteraction(string interactionId)
    {
        if (string.IsNullOrEmpty(interactionId))
            return;

        if (!SceneInteractionController.TryInteract("hall_playable_" + interactionId))
            return;

        switch (interactionId)
        {
            case InteractionRight:
                RequestLoad(SceneNames.HallRight);
                break;
            case InteractionLeft:
                RequestLoad("Hall_Left");
                break;
            case InteractionStair:
                // Confirmation menu (Yes → 2floorMainHall) remains a follow-up; direct load not used.
                Log("stair interaction received (menu path pending full menu host).");
                break;
            case InteractionBasement:
                Log("basement interaction received (unlock/menu path pending).");
                break;
            case InteractionMap:
                Log("map interaction received (panel path pending).");
                break;
            default:
                Log("unknown interaction: " + interactionId);
                break;
        }
    }

    /// <summary>Hub must not reload Hall_animate (Opening path owns that cinematic).</summary>
    public bool ShouldLoadHallAnimateFromHub() => false;

    void RequestLoad(string sceneName)
    {
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        if (SceneLoadHandlerForTests != null)
        {
            SceneLoadHandlerForTests(sceneName);
            return;
        }

        SceneTransitionService.LoadSceneSafely(sceneName);
    }

    void Log(string message)
    {
        if (enableDebugLogging)
            GameLog.Log("[HallPlayable] " + message);
    }

    internal static Func<string, bool> SceneLoadHandlerForTests;

    internal static void ResetStateForTests()
    {
        SceneLoadHandlerForTests = null;
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        SceneTransitionService.ResetForTests();
    }
}
