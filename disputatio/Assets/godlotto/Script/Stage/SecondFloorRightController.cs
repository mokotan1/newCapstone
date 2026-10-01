using System;
using System.Collections;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2층 오른쪽 4씬의 클릭·입장·확인. 규칙 소유자는 이 클래스이고 Sequence JSON은 쓰지 않는다.
/// </summary>
public sealed class SecondFloorRightController : MonoBehaviour
{
    public const string InteractionLeft = "left";
    public const string InteractionRight = "right";
    public const string InteractionFront = "front";
    public const string InteractionJesus = "jesus";
    public const string InteractionPhoto1 = "photo1";
    public const string InteractionPhoto2 = "photo2";

    public const float EnterFadeDurationSeconds = 1f;
    public const float EnterFadeTargetAlpha = 0f;
    public const float ExitFadeDurationSeconds = 1f;
    public const float ExitFadeTargetAlpha = 1f;
    public const float PostFadeWaitSeconds = 2f;
    public const float InspectionWaitSeconds = 0.1f;
    public const int MainHallBgmIndex = 2;

    public const string JesusLine = "예수상이다.";
    public const string WeddingPhotoLine = "행복해보이는 부부의 웨딩 사진이다.";
    public const string LastSupperLine = "최후의 만찬 그림이다.";
    public const string DownstairsPrompt = "1층으로 내려갈까?";
    public const string DownstairsYes = "1층으로 내려간다.";
    public const string DownstairsNo = "내려가지 않는다.";
    public const string ReturnPrompt = "이전 위치로 돌아갈까?";
    public const string ReturnYes = "돌아간다.";
    public const string ReturnNo = "돌아가지 않는다.";

    [SerializeField] SecondFloorRightRoute route;
    [SerializeField] WorldClickBinding[] worldClicks = Array.Empty<WorldClickBinding>();
    [SerializeField] GameObject exposureObject;
    [SerializeField] GameObject audioObject;
    [SerializeField] GameObject menuDialog;

    const float GameplayPlaneZ = 0f;

    bool sequenceBusy;

    public SecondFloorRightRoute Route => route;

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
            if (!PassesOriginalClickGuards(binding.collider.gameObject, screen))
                return;
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

        if (!SceneInteractionController.TryInteract("second_floor_right_" + route + "_" + interactionId))
            return;

        switch (interactionId)
        {
            case InteractionLeft:
                if (route == SecondFloorRightRoute.MainHall)
                    TravelTo(SceneNames.SecondFloorLeft);
                else if (route == SecondFloorRightRoute.RightCross)
                    TravelTo(SceneNames.BedEntrance);
                break;
            case InteractionRight:
                if (route == SecondFloorRightRoute.MainHall)
                    TravelTo(SceneNames.SecondFloorRight);
                else if (route == SecondFloorRightRoute.RightCross)
                    TravelTo(SceneNames.WifeEntrance);
                break;
            case InteractionFront:
                TravelTo(FrontDestination());
                break;
            case InteractionJesus:
                if (route == SecondFloorRightRoute.MainHall)
                    RunInspect(JesusLine);
                break;
            case InteractionPhoto1:
                if (HasPhotos())
                    RunInspect(WeddingPhotoLine);
                break;
            case InteractionPhoto2:
                if (HasPhotos())
                    RunInspect(LastSupperLine);
                break;
        }
    }

    public void RunEnter()
    {
        if (sequenceBusy)
            return;

        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        if (route == SecondFloorRightRoute.MainHall)
            PlayBgm(MainHallBgmIndex);

        bool electricOn = FlowchartLocator.GetBoolean(FungusVariableKeys.ElectricOn);
        if (electricOn)
        {
            ApplyFlash(0f, 0f);
            ApplyVignette(0f);
        }

        StartSequence(FadeRoutine(EnterFadeTargetAlpha, EnterFadeDurationSeconds, releaseWhenDone: true));
    }

    public void RunInspect(string line)
    {
        if (sequenceBusy || string.IsNullOrEmpty(line))
            return;

        StartSequence(InspectRoutine(line));
    }

    public void OnBack()
    {
        if (sequenceBusy || !HasBackConfirmation())
            return;

        if (FlowchartLocator.GetBoolean(FungusVariableKeys.IsClicked))
            return;

        if (!SceneInteractionController.TryInteract("second_floor_right_" + route + "_back"))
            return;

        StartSequence(BackRoutine());
    }

    public void ConfirmYes()
    {
        if (sequenceBusy || !HasBackConfirmation())
            return;

        StartSequence(ExitRoutine(goBack: route != SecondFloorRightRoute.MainHall, sceneName: YesDestination()));
    }

    public void ConfirmNo()
    {
        if (menuDialog != null)
            menuDialog.SetActive(false);

        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        ReleaseBusy();
    }

    /// <summary>
    /// SelectPre. The Fungus block has no click handler; it fades and calls GoBack.
    /// </summary>
    public void OnPreviousChoice()
    {
        if (sequenceBusy || route != SecondFloorRightRoute.RightCross)
            return;

        StartSequence(ExitRoutine(goBack: true, sceneName: null));
    }

    /// <summary>
    /// Block 2floorHallway_Right. No ObjectClicked points at it, so this is not a world click.
    /// </summary>
    public void OnHallwayChoice()
    {
        if (sequenceBusy || route != SecondFloorRightRoute.RightCross)
            return;

        TravelTo(SceneNames.SecondFloorHallwayRight);
    }

    bool HasBackConfirmation()
    {
        return route == SecondFloorRightRoute.MainHall
            || route == SecondFloorRightRoute.Right
            || route == SecondFloorRightRoute.HallwayRight;
    }

    bool HasPhotos()
    {
        return route == SecondFloorRightRoute.Right
            || route == SecondFloorRightRoute.HallwayRight;
    }

    string FrontDestination()
    {
        switch (route)
        {
            case SecondFloorRightRoute.Right:
                return SceneNames.SecondFloorRightCross;
            case SecondFloorRightRoute.HallwayRight:
                return SceneNames.SecondFloorMainHall;
            default:
                return null;
        }
    }

    string YesDestination()
    {
        return route == SecondFloorRightRoute.MainHall ? SceneNames.HallPlayable : null;
    }

    IEnumerator BackRoutine()
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, true);
        bool downstairs = route == SecondFloorRightRoute.MainHall;
        yield return SayLine(downstairs ? DownstairsPrompt : ReturnPrompt);
        int choice = 1;
        string[] options = downstairs
            ? new[] { DownstairsYes, DownstairsNo }
            : new[] { ReturnYes, ReturnNo };
        yield return ShowMenu(options, index => choice = index);
        if (choice == 0)
            yield return ExitRoutine(goBack: !downstairs, sceneName: downstairs ? SceneNames.HallPlayable : null);
        else
            ConfirmNo();
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
            GameLog.LogWarning("[SecondFloorRight] SayDialog missing.");
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

    IEnumerator ShowMenu(string[] options, Action<int> onChoice)
    {
        if (MenuHandlerForTests != null)
        {
            bool done = false;
            MenuHandlerForTests(options, index =>
            {
                onChoice?.Invoke(index);
                done = true;
            });
            while (!done)
                yield return null;
            yield break;
        }

        MenuDialog menu = MenuDialog.GetMenuDialog();
        if (menu == null)
        {
            GameLog.LogWarning("[SecondFloorRight] MenuDialog missing; declining.");
            onChoice?.Invoke(1);
            yield break;
        }

        bool chosen = false;
        int selected = 1;
        menu.Clear();
        menu.SetActive(true);
        Button[] buttons = menu.CachedButtons;
        if (buttons == null || buttons.Length == 0)
        {
            GameLog.LogWarning("[SecondFloorRight] MenuDialog has no buttons; declining.");
            onChoice?.Invoke(1);
            yield break;
        }

        int count = Math.Min(options.Length, buttons.Length);
        for (int i = 0; i < count; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            button.gameObject.SetActive(true);
            button.interactable = true;
            TextAdapter textAdapter = new TextAdapter();
            textAdapter.InitFromGameObject(button.gameObject, true);
            if (textAdapter.HasTextObject())
                textAdapter.Text = options[i];

            int captured = i;
            button.onClick.AddListener(() =>
            {
                selected = captured;
                chosen = true;
                menu.Clear();
                menu.SetActive(false);
                menu.HideSayDialog();
            });
        }

        while (!chosen)
            yield return null;

        onChoice?.Invoke(selected);
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

    void PlayBgm(int index)
    {
        if (BgmHandlerForTests != null)
        {
            BgmHandlerForTests(index);
            return;
        }

        AudioBridge audio = audioObject != null ? audioObject.GetComponent<AudioBridge>() : null;
        if (audio != null)
            audio.CallPlayBGM(index);
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

    void TravelTo(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        StartSequence(ExitRoutine(goBack: false, sceneName: sceneName));
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

    static bool PassesOriginalClickGuards(GameObject target, Vector2 screen)
    {
        if (Clickable2D.IsInteractiveUiUnderPointer(screen))
            return false;
        if (target != null && Clickable2D.ShouldBlockWorldClick(target))
            return false;
        if (Clickable2D.IsModalSayDialogOpen())
            return false;
        if (InteractionLock.IsLocked)
            return false;
        return true;
    }

    internal bool TryDispatchForTests(GameObject target, Vector2 screen, string interactionId)
    {
        if (!PassesOriginalClickGuards(target, screen))
            return false;
        OnInteraction(interactionId);
        return true;
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
    internal static Action<int> BgmHandlerForTests;
    internal static Action<string, float, float> ExposureHandlerForTests;
    internal static Action<string[], Action<int>> MenuHandlerForTests;

    internal static void ResetStateForTests()
    {
        SceneLoadHandlerForTests = null;
        BackHandlerForTests = null;
        SequenceRunnerForTests = null;
        FadeThenHandlerForTests = null;
        SayHandlerForTests = null;
        WaitHandlerForTests = null;
        AudioHandlerForTests = null;
        BgmHandlerForTests = null;
        ExposureHandlerForTests = null;
        MenuHandlerForTests = null;
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        SceneTransitionService.ResetForTests();
        HallGlobalStateHost.ResetForTests();
    }

    internal void AssignRouteForTests(SecondFloorRightRoute value) => route = value;

    internal void AssignMenuForTests(GameObject menu) => menuDialog = menu;

    internal bool IsBusyForTests => sequenceBusy;
}

public enum SecondFloorRightRoute
{
    MainHall = 0,
    Right = 1,
    RightCross = 2,
    HallwayRight = 3,
}
