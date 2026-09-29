using System;
using System.Collections;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;

/// <summary>
/// Opening_Mention Bell·fence 상호작용과 Start 입장 페이드를 Fungus Flowchart 없이 실행합니다.
/// </summary>
public class OpeningMentionController : MonoBehaviour
{
    public const string IsCallVariableKey = "isCall";
    public const string InteractionIdBell = "opening_mention_bell";
    public const string InteractionIdFence = "opening_mention_fence";
    public const string BellSequenceGateReason = "opening_mention_bell_sequence";
    const float GameplayPlaneZ = 0f;

    static readonly string[] BellLines =
    {
        "반응이 없다.  \n아무도 살지 않는 것 같다.\n",
        "일단 철창이 열리는지 확인해 보자"
    };

    const string FenceBeforeBellLine = "일단 초인종을 눌러보자.";
    const string FenceAfterBellLine = "문이 잠겨 있지 않는 듯 하다";

    [SerializeField] Collider2D fenceCollider;
    [SerializeField] Clickable2D fenceClickable;
    [SerializeField] bool enableFenceInteraction = true;
    [SerializeField] string openSceneName = "Opening_Mention _open";
    [SerializeField] bool enableDebugLogging;
    [SerializeField] float startFadeDurationSeconds = 1f;
    [SerializeField] float startFadeTargetAlpha = 0f;
    [SerializeField] float exitFadeDurationSeconds = 1f;
    [SerializeField] float exitFadeTargetAlpha = 1f;
    [SerializeField] int fenceOpenSfxIndex = 21;

    bool bellSequenceActive;
    bool isCall;
    bool started;

    void Awake()
    {
        if (fenceClickable != null)
            fenceClickable.enabled = false;
    }

    void Start()
    {
        if (started)
            return;
        started = true;
        StartCoroutine(PlayStartFade());
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

    void OnDisable()
    {
        EndBellSequence();
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
        StartCoroutine(PlayBellSequence());
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

        StartCoroutine(PlayFenceSequence());
    }

    IEnumerator PlayStartFade()
    {
        bool done = false;
        GameplayScreenFade.FadeThen(startFadeTargetAlpha, startFadeDurationSeconds, () => done = true);
        while (!done)
            yield return null;
        isCall = false;
    }

    IEnumerator PlayBellSequence()
    {
        for (int i = 0; i < BellLines.Length; i++)
            yield return SayLine(BellLines[i]);

        isCall = true;
        EndBellSequence();
    }

    IEnumerator PlayFenceSequence()
    {
        if (!isCall)
        {
            yield return SayLine(FenceBeforeBellLine);
            yield break;
        }

        yield return SayLine(FenceAfterBellLine);
        PlaySfx(fenceOpenSfxIndex);

        bool fadeDone = false;
        GameplayScreenFade.FadeThen(exitFadeTargetAlpha, exitFadeDurationSeconds, () => fadeDone = true);
        while (!fadeDone)
            yield return null;

        RequestOpenSceneTransition();
    }

    IEnumerator SayLine(string line)
    {
        bool done = false;
        SayDialog dialog = SayDialog.GetSayDialog();
        if (dialog == null)
        {
            GameLog.LogWarning("[OpeningMention] SayDialog not found.");
            yield break;
        }

        SayDialog.ActiveSayDialog = dialog;
        dialog.SetCharacterName(string.Empty, Color.white);
        dialog.Say(
            line,
            clearPrevious: true,
            waitForInput: true,
            fadeWhenDone: true,
            stopVoiceover: true,
            waitForVO: false,
            voiceOverClip: null,
            onComplete: () => done = true);

        while (!done)
            yield return null;
    }

    internal void RequestOpenSceneTransitionForTests() => RequestOpenSceneTransition();

    void RequestOpenSceneTransition()
    {
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);

        if (SceneLoadHandlerForTests != null)
        {
            SceneLoadHandlerForTests(openSceneName);
            return;
        }

        if (!SceneTransitionService.LoadSceneSafely(openSceneName))
            DeferredClickCleanup.Run(null, resetWindowClicked: false);
    }

    void EndBellSequence()
    {
        if (!bellSequenceActive && !InteractionInputGate.IsBlocked)
            return;

        bellSequenceActive = false;
        InteractionInputGate.Unblock(BellSequenceGateReason);
        InteractionLock.ForceUnlock();
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        DeferredClickCleanup.Run(null, resetWindowClicked: false);
    }

    static void PlaySfx(int index)
    {
        if (SfxController.Instance != null)
            SfxController.Instance.PlaySFX(index);
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

    internal bool IsCallForTests => isCall;

    internal void SimulateBellSequenceStartForTests()
    {
        bellSequenceActive = true;
        InteractionInputGate.Block(BellSequenceGateReason);
    }

    internal void SimulateBellSequenceEndForTests() => EndBellSequence();

    internal void SimulateIsCallForTests(bool value) => isCall = value;

    internal void CompleteBellMarkingCallForTests()
    {
        isCall = true;
        EndBellSequence();
    }

    internal IEnumerator PlayFenceSequenceForTests() => PlayFenceSequence();

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
