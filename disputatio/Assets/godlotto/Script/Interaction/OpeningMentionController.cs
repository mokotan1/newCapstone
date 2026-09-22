using System;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;

/// <summary>
/// Opening_Mention / Opening_Mention _open 씬의 Bell·fance 상호작용을 C#에서 조율합니다.
/// Fungus 블록은 대사·연출만 담당하고, 클릭 입력·연타 방지·씬 전환 결정은 여기서 처리합니다.
/// </summary>
public class OpeningMentionController : MonoBehaviour
{
    public const string IsCallVariableKey = "isCall";
    public const string InteractionIdBell = "opening_mention_bell";
    public const string InteractionIdFence = "opening_mention_fence";
    public const string BellSequenceGateReason = "opening_mention_bell_sequence";
    const float GameplayPlaneZ = 0f;

    [SerializeField] Flowchart flowchart;
    [SerializeField] Collider2D fenceCollider;
    [SerializeField] Clickable2D fenceClickable;
    [SerializeField] bool enableFenceInteraction = true;
    [SerializeField] string bellBlockName = "Bell_Clicked";
    [SerializeField] string fenceBlockName = "Fance_Clicked";
    [SerializeField] string openSceneName = "Opening_Mention _open";
    [SerializeField] bool enableDebugLogging;

    bool bellSequenceActive;
    bool pendingFenceSceneTransition;

    void Awake()
    {
        if (fenceClickable != null)
            fenceClickable.enabled = false;
    }

    void Update()
    {
        if (!enableFenceInteraction || fenceCollider == null || !fenceCollider.enabled)
            return;

        if (!TryGetPrimaryPressAndScreenPoint(out Vector2 screenPosition))
            return;

        if (!IsFenceColliderUnderPointer(screenPosition))
            return;

        OnFenceClicked();
    }

    void OnEnable()
    {
        BlockSignals.OnBlockEnd -= OnBlockEnd;
        BlockSignals.OnBlockEnd += OnBlockEnd;
    }

    void OnDisable()
    {
        BlockSignals.OnBlockEnd -= OnBlockEnd;
        EndBellSequence();
        pendingFenceSceneTransition = false;
    }

    /// <summary>UI Bell 버튼 OnClick 진입점.</summary>
    public void OnBellClicked()
    {
        if (bellSequenceActive)
        {
            LogIgnored("Bell click ignored: bell sequence already active.");
            return;
        }

        if (!SceneInteractionController.TryInteract(InteractionIdBell))
            return;

        bellSequenceActive = true;
        InteractionInputGate.Block(BellSequenceGateReason);

        if (!FungusDialogueBridge.ExecuteBlockSafely(flowchart, bellBlockName))
            EndBellSequence();
    }

    /// <summary>fance 월드 클릭 진입점.</summary>
    public void OnFenceClicked()
    {
        if (!enableFenceInteraction)
            return;

        if (bellSequenceActive)
        {
            LogIgnored("Fence click ignored: bell sequence is active.");
            return;
        }

        if (!SceneInteractionController.TryInteract(InteractionIdFence))
            return;

        bool isCall = GetIsCall();
        pendingFenceSceneTransition = isCall;

        if (!FungusDialogueBridge.ExecuteBlockSafely(flowchart, fenceBlockName))
            pendingFenceSceneTransition = false;
    }

    void OnBlockEnd(Block block)
    {
        if (block == null || flowchart == null || block.GetFlowchart() != flowchart)
            return;

        if (block.BlockName == bellBlockName)
        {
            EndBellSequence();
            return;
        }

        if (block.BlockName == fenceBlockName && pendingFenceSceneTransition)
        {
            pendingFenceSceneTransition = false;
            RequestOpenSceneTransition();
        }
    }

    void RequestOpenSceneTransition()
    {
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart, resetWindowClicked: false);

        if (SceneLoadHandlerForTests != null)
        {
            SceneLoadHandlerForTests(openSceneName);
            return;
        }

        if (!SceneTransitionService.LoadSceneSafely(openSceneName))
            DeferredClickCleanup.Run(flowchart, resetWindowClicked: false);
    }

    void EndBellSequence()
    {
        if (!bellSequenceActive && !InteractionInputGate.IsBlocked)
            return;

        bellSequenceActive = false;
        InteractionInputGate.Unblock(BellSequenceGateReason);
        InteractionLock.ForceUnlock();
        ClickInteractionCleanup.ResetAfterUiBoundary(flowchart, resetWindowClicked: false);
        DeferredClickCleanup.Run(flowchart, resetWindowClicked: false);
    }

    bool GetIsCall()
    {
        if (flowchart == null)
            return false;

        return flowchart.GetBooleanVariable(IsCallVariableKey);
    }

    void LogIgnored(string message)
    {
        if (enableDebugLogging)
            GameLog.Log($"[OpeningMention] {message}");
    }

    internal static Func<string, bool> SceneLoadHandlerForTests;

    internal static void ResetStateForTests()
    {
        SceneLoadHandlerForTests = null;
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        FungusDialogueBridge.ResetForTests();
        SceneTransitionService.ResetForTests();
    }

    internal bool IsBellSequenceActiveForTests => bellSequenceActive;

    internal bool IsPendingFenceSceneTransitionForTests => pendingFenceSceneTransition;

    internal void SimulateBellSequenceStartForTests()
    {
        bellSequenceActive = true;
        InteractionInputGate.Block(BellSequenceGateReason);
    }

    internal void SimulateBellSequenceEndForTests() => EndBellSequence();

    internal void SimulateFenceTransitionPendingForTests(bool pending) =>
        pendingFenceSceneTransition = pending;

    internal void InvokeBlockEndForTests(Block block) => OnBlockEnd(block);

    bool IsFenceColliderUnderPointer(Vector2 screenPosition)
    {
        Vector2 world = ScreenToWorldOnGameplayPlane(screenPosition);
        return fenceCollider.OverlapPoint(world);
    }

    static bool TryGetPrimaryPressAndScreenPoint(out Vector2 screenPosition)
    {
        screenPosition = default;

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        var touch = UnityEngine.InputSystem.Touchscreen.current;
        if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touch.primaryTouch.position.ReadValue();
            return true;
        }
#endif
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                screenPosition = t.position;
                return true;
            }
        }

        return false;
    }

    static Vector2 ScreenToWorldOnGameplayPlane(Vector2 screenPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return Vector2.zero;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        if (Mathf.Abs(ray.direction.z) > 1e-5f)
        {
            float t = (GameplayPlaneZ - ray.origin.z) / ray.direction.z;
            Vector3 p = ray.GetPoint(t);
            return new Vector2(p.x, p.y);
        }

        Vector3 fallback = cam.ScreenToWorldPoint(new Vector3(
            screenPosition.x,
            screenPosition.y,
            Mathf.Abs(cam.transform.position.z)));
        return fallback;
    }
}
