using System;
using System.Collections;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WifeEntrance와 BedEntrance의 문·열쇠·뒤로가기. Sequence JSON은 쓰지 않는다.
/// </summary>
public sealed class SecondFloorRoomEntranceController : MonoBehaviour
{
    public const string InteractionDoor = "door";
    public const string InteractionUnlock = "unlock";
    public const string InteractionBack = "back";

    public const float EnterFadeDurationSeconds = 1f;
    public const float EnterFadeTargetAlpha = 0f;
    public const float ExitFadeDurationSeconds = 1f;
    public const float ExitFadeTargetAlpha = 1f;
    public const float PostFadeWaitSeconds = 2f;
    public const float InspectionWaitSeconds = 0.1f;

    public const string LockedLine = "잠겨있다";
    public const string WifeLockedHint = "열쇠가 있어야 문을 열 수 있을 것 같다.";
    public const string BedLockedHint = "열쇠가 있어야 열 수 있을 것 같다.";
    public const string EnterPrompt = "들어갈까?\n";
    public const string EnterYes = "들어간다";
    public const string EnterNo = "들어가지 않는다";
    public const string DoorDeclineLine = "주변을 더 둘러봐야겠다.";
    public const string UnlockPrompt = "문이 열렸다. 들어갈까?";
    public const string UnlockDeclineLine = "조금 더 준비가 필요하다.";
    public const string ReturnPrompt = "이전 위치로 돌아갈까?";
    public const string ReturnYes = "돌아간다.";
    public const string ReturnNo = "돌아가지 않는다.";

    [SerializeField] SecondFloorRoomEntrance route;
    [SerializeField] WorldClickBinding[] worldClicks = Array.Empty<WorldClickBinding>();
    [SerializeField] GameObject exposureObject;
    [SerializeField] GameObject audioObject;

    const float GameplayPlaneZ = 0f;
    bool sequenceBusy;

    public SecondFloorRoomEntrance Route => route;

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
        switch (interactionId)
        {
            case InteractionDoor:
                OnDoor();
                break;
            case InteractionUnlock:
                OnUnlock();
                break;
            case InteractionBack:
                OnBack();
                break;
        }
    }

    public void OnDoor()
    {
        if (!BeginPlayerAction(InteractionDoor))
            return;

        if (!KeyUsed())
            StartSequence(LockedRoutine());
        else
            StartSequence(ChoiceRoutine(EnterPrompt, DoorDeclineLine, loadRoom: true));
    }

    public void OnUnlock()
    {
        if (!BeginPlayerAction(InteractionUnlock))
            return;

        FlowchartLocator.SetBoolean(KeyName(), true);
        StartSequence(ChoiceRoutine(UnlockPrompt, UnlockDeclineLine, loadRoom: true));
    }

    public void OnBack()
    {
        if (!BeginPlayerAction(InteractionBack))
            return;

        StartSequence(BackRoutine());
    }

    public void ConfirmEnter()
    {
        if (sequenceBusy)
            return;
        StartSequence(ExitRoutine(goBack: false));
    }

    public void DeclineDoor()
    {
        if (sequenceBusy)
            return;
        StartSequence(SayAndRelease(DoorDeclineLine));
    }

    public void DeclineUnlock()
    {
        if (sequenceBusy)
            return;
        StartSequence(SayAndRelease(UnlockDeclineLine));
    }

    public void ConfirmBack()
    {
        if (sequenceBusy)
            return;
        StartSequence(ExitRoutine(goBack: true));
    }

    public void DeclineBack()
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        ReleaseBusy();
    }

    public void RunEnter()
    {
        if (sequenceBusy)
            return;

        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        if (FlowchartLocator.GetBoolean(FungusVariableKeys.ElectricOn))
        {
            ApplyFlash(0f, 0f);
            ApplyVignette(0f);
        }

        StartSequence(FadeRoutine(EnterFadeTargetAlpha, EnterFadeDurationSeconds, releaseWhenDone: true));
    }

    bool BeginPlayerAction(string interactionId)
    {
        if (sequenceBusy)
            return false;
        if (FlowchartLocator.GetBoolean(FungusVariableKeys.IsClicked))
            return false;
        return SceneInteractionController.TryInteract("room_entrance_" + route + "_" + interactionId);
    }

    bool KeyUsed()
    {
        return FlowchartLocator.GetBoolean(KeyName());
    }

    string KeyName()
    {
        return route == SecondFloorRoomEntrance.Bed
            ? FungusVariableKeys.UsedBedKey
            : FungusVariableKeys.UsedWifeKey;
    }

    string Destination()
    {
        return route == SecondFloorRoomEntrance.Bed ? SceneNames.BedRoom : SceneNames.WifeRoom;
    }

    string LockedHint()
    {
        return route == SecondFloorRoomEntrance.Bed ? BedLockedHint : WifeLockedHint;
    }

    IEnumerator LockedRoutine()
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, true);
        yield return SayLine(LockedLine);
        yield return WaitSeconds(InspectionWaitSeconds);
        yield return SayLine(LockedHint());
        yield return WaitSeconds(InspectionWaitSeconds);
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        ReleaseBusy();
    }

    IEnumerator ChoiceRoutine(string prompt, string declineLine, bool loadRoom)
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, true);
        yield return SayLine(prompt);
        yield return WaitSeconds(InspectionWaitSeconds);
        int choice = 1;
        yield return ShowMenu(new[] { EnterYes, EnterNo }, index => choice = index);
        if (choice == 0 && loadRoom)
            yield return ExitRoutine(goBack: false);
        else
            yield return SayAndRelease(declineLine);
    }

    IEnumerator BackRoutine()
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, true);
        yield return SayLine(ReturnPrompt);
        int choice = 1;
        yield return ShowMenu(new[] { ReturnYes, ReturnNo }, index => choice = index);
        if (choice == 0)
            yield return ExitRoutine(goBack: true);
        else
            DeclineBack();
    }

    IEnumerator SayAndRelease(string line)
    {
        yield return SayLine(line);
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        ReleaseBusy();
    }

    IEnumerator ExitRoutine(bool goBack)
    {
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, true);
        yield return FadeRoutine(ExitFadeTargetAlpha, ExitFadeDurationSeconds, releaseWhenDone: false);
        PlayFootstep();
        yield return WaitSeconds(PostFadeWaitSeconds);
        if (goBack)
            RequestBack();
        else
            RequestLoad(Destination());
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
            GameLog.LogWarning("[RoomEntrance] SayDialog missing.");
            yield break;
        }

        SayDialog.ActiveSayDialog = dialog;
        dialog.SetCharacterName(string.Empty, Color.white);
        dialog.Say(line, true, true, true, true, false, null, () => finished = true);
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
        if (menu == null || menu.CachedButtons == null || menu.CachedButtons.Length == 0)
        {
            onChoice?.Invoke(1);
            yield break;
        }

        bool chosen = false;
        int selected = 1;
        menu.Clear();
        menu.SetActive(true);
        Button[] buttons = menu.CachedButtons;
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

    static bool TryGetPrimaryPress(out Vector2 screen)
    {
        screen = default;
        if (HasPressForTests)
        {
            screen = PressForTests;
            HasPressForTests = false;
            return true;
        }

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
            return ray.GetPoint(t);
        }

        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
    }

    internal static bool HasPressForTests;
    internal static Vector2 PressForTests;
    internal static Func<string, bool> SceneLoadHandlerForTests;
    internal static Action BackHandlerForTests;
    internal static Action<IEnumerator> SequenceRunnerForTests;
    internal static Action<float, float, Action> FadeThenHandlerForTests;
    internal static Action<string, Action> SayHandlerForTests;
    internal static Action<float> WaitHandlerForTests;
    internal static Action AudioHandlerForTests;
    internal static Action<string, float, float> ExposureHandlerForTests;
    internal static Action<string[], Action<int>> MenuHandlerForTests;

    internal static void ResetStateForTests()
    {
        HasPressForTests = false;
        PressForTests = default;
        SceneLoadHandlerForTests = null;
        BackHandlerForTests = null;
        SequenceRunnerForTests = null;
        FadeThenHandlerForTests = null;
        SayHandlerForTests = null;
        WaitHandlerForTests = null;
        AudioHandlerForTests = null;
        ExposureHandlerForTests = null;
        MenuHandlerForTests = null;
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        SceneTransitionService.ResetForTests();
        HallGlobalStateHost.ResetForTests();
    }

    internal void AssignRouteForTests(SecondFloorRoomEntrance value) => route = value;

    internal bool IsBusyForTests => sequenceBusy;

    internal bool TryDispatchForTests(GameObject target, Vector2 screen, string interactionId)
    {
        if (!PassesOriginalClickGuards(target, screen))
            return false;
        OnInteraction(interactionId);
        return true;
    }
}

public enum SecondFloorRoomEntrance
{
    Wife = 0,
    Bed = 1,
}
