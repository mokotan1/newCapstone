using System;
using System.Collections;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;

/// <summary>
/// Opening_Mention _open without Fungus Flowchart.
/// Mirrors Start / Bell_Clicked / Door: busy flag = isClicked; Door does not gate on isCall.
/// </summary>
public sealed class OpeningMentionOpenController : MonoBehaviour
{
    public const string InteractionIdBell = "opening_mention_open_bell";
    public const string InteractionIdDoor = "opening_mention_open_door";
    public const string SequenceGateReason = "opening_mention_open_sequence";
    const float GameplayPlaneZ = 0f;

    const string TrapLine = "주인공: “문이 열린다? 이건 친절이 아니라 함정이지.”";
    const string BellNoResponseLine = "반응이 없다.  \n아무도 살지 않는 것 같다.\n";
    const string BellEnterLine = "철창이 열렸으니 들어가보자.";

    [SerializeField] Collider2D doorCollider;
    [SerializeField] Clickable2D doorClickable;
    [SerializeField] GameObject lightObject;
    [SerializeField] GameObject mansionLightsObject;
    [SerializeField] AudioClip bellSoundClip;
    [SerializeField] float bellSoundVolume = 1f;
    [SerializeField] float startFadeDurationSeconds = 1f;
    [SerializeField] float startFadeTargetAlpha = 0f;
    [SerializeField] float exitFadeDurationSeconds = 0.5f;
    [SerializeField] float exitFadeTargetAlpha = 1f;
    [SerializeField] int doorSfxIndex = 7;
    [SerializeField] bool enableDebugLogging;

    bool busy;
    bool isCall;
    bool started;
    bool doorSequenceActive;

    void Awake()
    {
        if (doorClickable != null)
            doorClickable.enabled = false;
    }

    void Start()
    {
        if (started)
            return;
        started = true;
        RunSequence(PlayStartSequence());
    }

    void Update()
    {
        if (doorCollider == null || !doorCollider.enabled)
            return;

        if (!TryGetPrimaryPressAndScreenPoint(out Vector2 screenPosition))
            return;

        if (!IsDoorColliderUnderPointer(screenPosition))
            return;

        OnDoorClicked();
    }

    void OnDisable()
    {
        ReleaseBusyGate();
    }

    /// <summary>UI Bell 버튼 OnClick 진입점.</summary>
    public void OnBellClicked()
    {
        if (busy)
        {
            LogIgnored("Bell ignored: busy.");
            return;
        }

        if (!SceneInteractionController.TryInteract(InteractionIdBell))
            return;

        RunSequence(PlayBellSequence());
    }

    /// <summary>Door 월드 클릭 진입점.</summary>
    public void OnDoorClicked()
    {
        if (doorSequenceActive)
            return;

        PlayDoorSfx();

        if (busy)
        {
            LogIgnored("Door fade/load skipped: busy.");
            return;
        }

        if (!SceneInteractionController.TryInteract(InteractionIdDoor))
            return;

        doorSequenceActive = true;
        RunSequence(PlayDoorExitSequence());
    }

    void RunSequence(IEnumerator sequence)
    {
        if (SequenceRunnerForTests != null)
        {
            SequenceRunnerForTests(sequence);
            return;
        }

        StartCoroutine(sequence);
    }

    IEnumerator PlayStartSequence()
    {
        busy = true;
        InteractionInputGate.Block(SequenceGateReason);

        bool fadeDone = false;
        GameplayScreenFade.FadeThen(startFadeTargetAlpha, startFadeDurationSeconds, () => fadeDone = true);
        while (!fadeDone)
            yield return null;

        yield return SayLine(TrapLine);

        busy = false;
        ReleaseBusyGate();
    }

    IEnumerator PlayBellSequence()
    {
        busy = true;
        isCall = true;
        InteractionInputGate.Block(SequenceGateReason);

        if (bellSoundClip != null)
        {
            AudioSource.PlayClipAtPoint(bellSoundClip, transform.position, bellSoundVolume);
            yield return new WaitForSecondsRealtime(bellSoundClip.length);
        }

        if (lightObject != null)
            lightObject.SetActive(false);
        if (mansionLightsObject != null)
            mansionLightsObject.SetActive(false);

        yield return SayLine(BellNoResponseLine);
        yield return SayLine(BellEnterLine);

        busy = false;
        ReleaseBusyGate();
    }

    IEnumerator PlayDoorExitSequence()
    {
        busy = true;
        InteractionInputGate.Block(SequenceGateReason);

        bool fadeDone = false;
        GameplayScreenFade.FadeThen(exitFadeTargetAlpha, exitFadeDurationSeconds, () => fadeDone = true);
        while (!fadeDone)
            yield return null;

        RequestHallAnimate();
    }

    void PlayDoorSfx()
    {
        if (PlaySfxHandlerForTests != null)
        {
            PlaySfxHandlerForTests(doorSfxIndex);
            return;
        }

        if (SfxController.Instance != null)
            SfxController.Instance.PlaySFX(doorSfxIndex);
    }

    IEnumerator SayLine(string line)
    {
        bool done = false;
        SayDialog dialog = SayDialog.GetSayDialog();
        if (dialog == null || !dialog.isActiveAndEnabled)
        {
            GameLog.LogWarning("[OpeningMentionOpen] SayDialog not found or inactive.");
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

    void RequestHallAnimate()
    {
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        if (SceneLoadHandlerForTests != null)
        {
            SceneLoadHandlerForTests(SceneNames.HallAnimate);
            return;
        }

        SceneTransitionService.LoadSceneSafely(SceneNames.HallAnimate);
    }

    void ReleaseBusyGate()
    {
        InteractionInputGate.Unblock(SequenceGateReason);
        InteractionLock.ForceUnlock();
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        DeferredClickCleanup.Run(null, resetWindowClicked: false);
    }

    void LogIgnored(string message)
    {
        if (enableDebugLogging)
            GameLog.Log("[OpeningMentionOpen] " + message);
    }

    bool IsDoorColliderUnderPointer(Vector2 screenPosition)
    {
        Vector2 world = ScreenToWorldOnGameplayPlane(screenPosition);
        return doorCollider.OverlapPoint(world);
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

    internal static Func<string, bool> SceneLoadHandlerForTests;
    internal static Action<int> PlaySfxHandlerForTests;
    internal static Action<IEnumerator> SequenceRunnerForTests;

    internal static void ResetStateForTests()
    {
        SceneLoadHandlerForTests = null;
        PlaySfxHandlerForTests = null;
        SequenceRunnerForTests = null;
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        SceneTransitionService.ResetForTests();
    }

    internal bool IsBusyForTests => busy;
    internal bool IsCallForTests => isCall;

    internal void SimulateBusyForTests(bool value)
    {
        busy = value;
        if (value)
            InteractionInputGate.Block(SequenceGateReason);
        else
            ReleaseBusyGate();
    }

    internal void SimulateIsCallForTests(bool value) => isCall = value;

    internal void RequestHallAnimateForTests() => RequestHallAnimate();

    internal void AssignBellTargetsForTests(GameObject light, GameObject mansionLights, AudioClip clip)
    {
        lightObject = light;
        mansionLightsObject = mansionLights;
        bellSoundClip = clip;
    }
}
