using System;
using System.Collections;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;

/// <summary>
/// 1층 오른쪽 복도 5씬의 클릭·입장 연출. 규칙 소유자는 이 클래스이고 Sequence JSON은 쓰지 않는다.
/// </summary>
public sealed class RightHallCorridorController : MonoBehaviour
{
    public const string InteractionFront = "front";
    public const string InteractionShowcase = "showcase";
    public const string InteractionMedal = "medal";
    public const string InteractionRight = "right";
    public const string InteractionLeft = "left";

    public const float EnterFadeDurationSeconds = 1f;
    public const float EnterFadeTargetAlpha = 0f;
    public const float ExitFadeDurationSeconds = 1f;
    public const float ExitFadeTargetAlpha = 1f;
    public const float PostFadeWaitSeconds = 2f;
    public const float InspectionWaitSeconds = 0.1f;
    public const float ElectricOffFlash = -0.6f;

    public const string ShowcaseLine = "각종 단체에서 보낸 감사패이다.";
    public const string MedalLine = "봉사 메달과 헌혈 포장증이다.";

    [SerializeField] RightHallRoute route;
    [SerializeField] WorldClickBinding[] worldClicks = Array.Empty<WorldClickBinding>();
    [SerializeField] GameObject exposureObject;
    [SerializeField] GameObject audioObject;
    [SerializeField] GameObject menuDialog;

    const float GameplayPlaneZ = 0f;

    bool sequenceBusy;

    public RightHallRoute Route => route;

    void Awake()
    {
        HallGlobalStateHost.EnsureInstance();
        for (int i = 0; i < worldClicks.Length; i++)
        {
            if (worldClicks[i].clickable != null)
                worldClicks[i].clickable.enabled = false;
        }
    }

    void Start()
    {
        RunEnter();
    }

    void Update()
    {
        if (sequenceBusy)
            return;

        if (!TryGetPrimaryPress(out Vector2 screen))
            return;

        for (int i = 0; i < worldClicks.Length; i++)
        {
            WorldClickBinding binding = worldClicks[i];
            if (binding.collider == null || !binding.collider.enabled)
                continue;
            if (!binding.collider.OverlapPoint(ScreenToWorld(screen)))
                continue;
            OnInteraction(binding.interactionId);
            return;
        }
    }

    public void OnInteraction(string interactionId)
    {
        if (string.IsNullOrEmpty(interactionId) || sequenceBusy)
            return;

        if (FlowchartLocator.GetBoolean(FungusVariableKeys.IsClicked))
            return;

        if (!SceneInteractionController.TryInteract("right_hall_" + route + "_" + interactionId))
            return;

        switch (interactionId)
        {
            case InteractionFront:
                TravelTo(FrontDestination());
                break;
            case InteractionRight:
                if (route == RightHallRoute.HallRightCross)
                    TravelTo(SceneNames.StudyEntrance);
                break;
            case InteractionLeft:
                if (route == RightHallRoute.HallRightCross)
                    TravelTo(SceneNames.MaidEntrance);
                break;
            case InteractionShowcase:
                if (HasShowcase())
                    RunInspect(ShowcaseLine);
                break;
            case InteractionMedal:
                if (route != RightHallRoute.HallRightCross)
                    RunInspect(MedalLine);
                break;
        }
    }

    public void RunEnter()
    {
        if (sequenceBusy)
            return;

        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        bool electricOn = FlowchartLocator.GetBoolean(FungusVariableKeys.ElectricOn);
        if (electricOn)
        {
            ApplyFlash(0f, 0f);
            ApplyVignette(0f);
        }
        else if (route == RightHallRoute.HallRight)
        {
            ApplyFlash(ElectricOffFlash, ElectricOffFlash);
        }

        StartSequence(FadeRoutine(EnterFadeTargetAlpha, EnterFadeDurationSeconds, releaseWhenDone: true));
    }

    public void RunInspect(string line)
    {
        if (sequenceBusy || string.IsNullOrEmpty(line))
            return;

        StartSequence(InspectRoutine(line));
    }

    public void RunTravel(string sceneName)
    {
        TravelTo(sceneName);
    }

    public void ConfirmYes()
    {
        if (sequenceBusy || route == RightHallRoute.HallRightCross)
            return;

        if (route == RightHallRoute.HallRight2)
        {
            TravelTo(SceneNames.HallRight2);
            return;
        }

        StartSequence(ExitRoutine(goBack: true, sceneName: null));
    }

    public void ConfirmNo()
    {
        if (menuDialog != null)
            menuDialog.SetActive(false);

        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        ReleaseBusy();
    }

    public void OnPreviousChoice()
    {
        if (sequenceBusy || route != RightHallRoute.HallRightCross)
            return;

        TravelTo(SceneNames.HallwayRight);
    }

    void TravelTo(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        StartSequence(ExitRoutine(goBack: false, sceneName: sceneName));
    }

    string FrontDestination()
    {
        switch (route)
        {
            case RightHallRoute.HallRight:
                return SceneNames.HallRight2;
            case RightHallRoute.HallRight2:
                return SceneNames.HallRightCross;
            case RightHallRoute.HallwayRight:
                return SceneNames.HallwayRight2;
            case RightHallRoute.HallwayRight2:
                return SceneNames.HallPlayable;
            default:
                return null;
        }
    }

    bool HasShowcase()
    {
        return route == RightHallRoute.HallRight
            || route == RightHallRoute.HallwayRight
            || route == RightHallRoute.HallwayRight2;
    }

    IEnumerator InspectRoutine(string line)
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, true);
        yield return SayLine(line);
        yield return WaitSeconds(InspectionWaitSeconds);
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        ReleaseBusy();
    }

    IEnumerator ExitRoutine(bool goBack, string sceneName)
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, true);
        yield return FadeRoutine(ExitFadeTargetAlpha, ExitFadeDurationSeconds, releaseWhenDone: false);
        PlayFootstep();
        yield return WaitSeconds(PostFadeWaitSeconds);
        if (goBack)
            RequestBack();
        else
            RequestLoad(sceneName);
    }

    IEnumerator FadeRoutine(float targetAlpha, float durationSeconds, bool releaseWhenDone)
    {
        bool done = false;
        if (FadeThenHandlerForTests != null)
            FadeThenHandlerForTests(targetAlpha, durationSeconds, () => done = true);
        else
            GameplayScreenFade.FadeThen(targetAlpha, durationSeconds, () => done = true);

        while (!done)
            yield return null;

        if (releaseWhenDone)
            ReleaseBusy();
    }

    IEnumerator WaitSeconds(float seconds)
    {
        WaitHandlerForTests?.Invoke(seconds);
        if (WaitHandlerForTests != null)
            yield break;

        yield return new WaitForSeconds(seconds);
    }

    IEnumerator SayLine(string line)
    {
        if (SayHandlerForTests != null)
        {
            bool done = false;
            SayHandlerForTests(line, () => done = true);
            while (!done)
                yield return null;
            yield break;
        }

        bool finished = false;
        SayDialog dialog = SayDialog.GetSayDialog();
        if (dialog == null || !dialog.isActiveAndEnabled)
        {
            GameLog.LogWarning("[RightHall] SayDialog missing.");
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
            onComplete: () => finished = true);
        while (!finished)
            yield return null;
    }

    void ApplyFlash(float peak, float target)
    {
        if (ExposureHandlerForTests != null)
        {
            ExposureHandlerForTests("TriggerFlashEffect", peak, target);
            return;
        }

        PostExposureController exposure = exposureObject != null
            ? exposureObject.GetComponent<PostExposureController>()
            : null;
        if (exposure != null)
            exposure.TriggerFlashEffect(peak, target);
    }

    void ApplyVignette(float value)
    {
        if (ExposureHandlerForTests != null)
        {
            ExposureHandlerForTests("SetVignetteToZero", value, 0f);
            return;
        }

        PostExposureController exposure = exposureObject != null
            ? exposureObject.GetComponent<PostExposureController>()
            : null;
        if (exposure != null)
            exposure.SetVignetteToZero(value);
    }

    void PlayFootstep()
    {
        if (AudioHandlerForTests != null)
        {
            AudioHandlerForTests();
            return;
        }

        AudioBridge audio = audioObject != null ? audioObject.GetComponent<AudioBridge>() : null;
        if (audio != null)
            audio.CallPlayFootstepDefault();
    }

    void StartSequence(IEnumerator sequence)
    {
        sequenceBusy = true;
        if (SequenceRunnerForTests != null)
        {
            SequenceRunnerForTests(sequence);
            return;
        }

        StartCoroutine(Wrap(sequence));
    }

    IEnumerator Wrap(IEnumerator sequence)
    {
        yield return sequence;
        if (sequenceBusy)
            ReleaseBusy();
    }

    void ReleaseBusy()
    {
        sequenceBusy = false;
        InteractionLock.ForceUnlock();
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        DeferredClickCleanup.Run(null, resetWindowClicked: false);
    }

    void RequestLoad(string sceneName)
    {
        sequenceBusy = false;
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        if (sceneName == SceneNames.HallAnimate)
            return;

        if (SceneLoadHandlerForTests != null)
        {
            SceneLoadHandlerForTests(sceneName);
            return;
        }

        SceneTransitionService.LoadSceneSafely(sceneName);
    }

    void RequestBack()
    {
        sequenceBusy = false;
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        if (BackHandlerForTests != null)
        {
            BackHandlerForTests();
            return;
        }

        BackNavigator navigator = FindFirstObjectByType<BackNavigator>();
        if (navigator != null)
            navigator.GoBack();
    }

    static bool TryGetPrimaryPress(out Vector2 screen)
    {
        screen = default;
        if (Input.GetMouseButtonDown(0))
        {
            screen = Input.mousePosition;
            return true;
        }

        return false;
    }

    static Vector2 ScreenToWorld(Vector2 screen)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return Vector2.zero;

        Ray ray = cam.ScreenPointToRay(screen);
        if (Mathf.Abs(ray.direction.z) > 1e-5f)
        {
            float t = (GameplayPlaneZ - ray.origin.z) / ray.direction.z;
            Vector3 point = ray.GetPoint(t);
            return new Vector2(point.x, point.y);
        }

        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
    }

    internal static Func<string, bool> SceneLoadHandlerForTests;
    internal static Action BackHandlerForTests;
    internal static Action<IEnumerator> SequenceRunnerForTests;
    internal static Action<float, float, Action> FadeThenHandlerForTests;
    internal static Action<string, Action> SayHandlerForTests;
    internal static Action<float> WaitHandlerForTests;
    internal static Action AudioHandlerForTests;
    internal static Action<string, float, float> ExposureHandlerForTests;

    internal static void ResetStateForTests()
    {
        SceneLoadHandlerForTests = null;
        BackHandlerForTests = null;
        SequenceRunnerForTests = null;
        FadeThenHandlerForTests = null;
        SayHandlerForTests = null;
        WaitHandlerForTests = null;
        AudioHandlerForTests = null;
        ExposureHandlerForTests = null;
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        SceneTransitionService.ResetForTests();
        HallGlobalStateHost.ResetForTests();
    }

    internal void AssignRouteForTests(RightHallRoute value) => route = value;

    internal void AssignMenuForTests(GameObject menu) => menuDialog = menu;

    internal bool IsBusyForTests => sequenceBusy;
}

public enum RightHallRoute
{
    HallRight = 0,
    HallRight2 = 1,
    HallRightCross = 2,
    HallwayRight = 3,
    HallwayRight2 = 4,
}
