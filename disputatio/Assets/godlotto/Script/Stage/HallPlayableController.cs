using System;
using System.Collections;
using Fungus;
using Godlotto.Interaction;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hall_playerble hub without Fungus Flowchart.
/// Keeps CorridorEntrance hub rule: never reload Hall_animate from this scene.
/// </summary>
public sealed class HallPlayableController : MonoBehaviour
{
    public const string InteractionRight = "right";
    public const string InteractionLeft = "left";
    public const string InteractionStair = "stair";
    public const string InteractionBasement = "basement";
    public const string InteractionMap = "map";
    public const string InteractionUnlock = "unlock";
    const float GameplayPlaneZ = 0f;

    const string StairPrompt = "2층으로 올라갈까?";
    const string StairYes = "올라간다.\n";
    const string StairNo = "올라가지 않는다.";
    const string StairDecline = "1층을 더 둘러보자.";
    const string BasementLocked = "열쇠가 있어야 열 수 있을 것 같다.";
    const string BasementOpenPrompt = "지하실 문이 열려있다. 내려갈까?";
    const string BasementYes = "내려간다.";
    const string BasementNo = "내려가지 않는다.";
    const string UnlockPrompt = "문이 열렸다. 들어갈까?";
    const string UnlockYes = "들어간다";
    const string UnlockNo = "들어가지 않는다";
    const string NeedPrepare = "조금 더 준비가 필요하다.";

    [SerializeField] WorldClickBinding[] worldClicks = Array.Empty<WorldClickBinding>();
    [SerializeField] GameObject fieldMapObject;
    [SerializeField] float exitFadeDurationSeconds = 0.5f;
    [SerializeField] float exitFadeTargetAlpha = 1f;
    [SerializeField] bool enableDebugLogging;

    bool sequenceBusy;

    void Awake()
    {
        HallGlobalStateHost.EnsureInstance();
        for (int i = 0; i < worldClicks.Length; i++)
        {
            if (worldClicks[i].clickable != null)
                worldClicks[i].clickable.enabled = false;
        }
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

        if (!SceneInteractionController.TryInteract("hall_playable_" + interactionId))
            return;

        switch (interactionId)
        {
            case InteractionRight:
                StartSequence(FadeAndLoad(SceneNames.HallRight));
                break;
            case InteractionLeft:
                StartSequence(FadeAndLoad(SceneNames.HallLeft));
                break;
            case InteractionStair:
                StartSequence(PlayStairSequence());
                break;
            case InteractionBasement:
                StartSequence(PlayBasementDoorSequence());
                break;
            case InteractionUnlock:
                StartSequence(PlayUnlockBasementSequence());
                break;
            case InteractionMap:
                OpenMap();
                break;
            default:
                Log("unknown interaction: " + interactionId);
                break;
        }
    }

    public bool ShouldLoadHallAnimateFromHub() => false;

    public void ConfirmStairYes() => RequestLoad(SceneNames.SecondFloorMainHall);

    public void ConfirmBasementYes() => RequestLoad(SceneNames.BetaEnd);

    public void ConfirmUnlockYes() => RequestLoad(SceneNames.BetaEnd);

    IEnumerator PlayStairSequence()
    {
        yield return SayLine(StairPrompt);
        int choice = -1;
        yield return ShowMenu(new[] { StairYes, StairNo }, i => choice = i);
        if (choice == 0)
        {
            yield return FadeOut();
            RequestLoad(SceneNames.SecondFloorMainHall);
            yield break;
        }

        yield return SayLine(StairDecline);
        ReleaseBusy();
    }

    IEnumerator PlayBasementDoorSequence()
    {
        if (!FlowchartLocator.GetBoolean(FungusVariableKeys.UsedBasementKey))
        {
            yield return SayLine(BasementLocked);
            ReleaseBusy();
            yield break;
        }

        yield return SayLine(BasementOpenPrompt);
        int choice = -1;
        yield return ShowMenu(new[] { BasementYes, BasementNo }, i => choice = i);
        if (choice == 0)
        {
            yield return FadeOut();
            RequestLoad(SceneNames.BetaEnd);
            yield break;
        }

        yield return SayLine(NeedPrepare);
        ReleaseBusy();
    }

    IEnumerator PlayUnlockBasementSequence()
    {
        yield return SayLine(UnlockPrompt);
        int choice = -1;
        yield return ShowMenu(new[] { UnlockYes, UnlockNo }, i => choice = i);
        if (choice == 0)
        {
            yield return FadeOut();
            RequestLoad(SceneNames.BetaEnd);
            yield break;
        }

        yield return SayLine(NeedPrepare);
        ReleaseBusy();
    }

    IEnumerator FadeAndLoad(string sceneName)
    {
        yield return FadeOut();
        RequestLoad(sceneName);
    }

    IEnumerator FadeOut()
    {
        bool done = false;
        if (FadeThenHandlerForTests != null)
        {
            FadeThenHandlerForTests(exitFadeTargetAlpha, exitFadeDurationSeconds, () => done = true);
        }
        else
        {
            GameplayScreenFade.FadeThen(exitFadeTargetAlpha, exitFadeDurationSeconds, () => done = true);
        }

        while (!done)
            yield return null;
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
            GameLog.LogWarning("[HallPlayable] SayDialog missing.");
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
            GameLog.LogWarning("[HallPlayable] MenuDialog missing; declining.");
            onChoice?.Invoke(1);
            yield break;
        }

        // MenuDialog.AddOption(UnityAction) is private; public overload takes Block only.
        // Wire CachedButtons the same way the private path does.
        bool chosen = false;
        int selected = 1;
        menu.Clear();
        menu.SetActive(true);
        Button[] buttons = menu.CachedButtons;
        if (buttons == null || buttons.Length == 0)
        {
            GameLog.LogWarning("[HallPlayable] MenuDialog has no buttons; declining.");
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

    void OpenMap()
    {
        if (WhenClikcedButton.Instance != null)
        {
            WhenClikcedButton.Instance.OnOpenMapClick();
            return;
        }

        if (fieldMapObject != null)
            fieldMapObject.SetActive(true);
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
        FlowchartLocator.SetBoolean(FungusVariableKeys.IsClicked, false);
        InteractionLock.ForceUnlock();
        ClickInteractionCleanup.ResetAfterUiBoundary(null, resetWindowClicked: false);
        DeferredClickCleanup.Run(null, resetWindowClicked: false);
    }

    void RequestLoad(string sceneName)
    {
        sequenceBusy = false;
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
            Vector3 p = ray.GetPoint(t);
            return new Vector2(p.x, p.y);
        }

        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
    }

    internal static Func<string, bool> SceneLoadHandlerForTests;
    internal static Action<IEnumerator> SequenceRunnerForTests;
    internal static Action<float, float, Action> FadeThenHandlerForTests;
    internal static Action<string, Action> SayHandlerForTests;
    internal static Action<string[], Action<int>> MenuHandlerForTests;

    internal static void ResetStateForTests()
    {
        SceneLoadHandlerForTests = null;
        SequenceRunnerForTests = null;
        FadeThenHandlerForTests = null;
        SayHandlerForTests = null;
        MenuHandlerForTests = null;
        InteractionInputGate.ResetForTests();
        SceneInteractionController.ResetForTests();
        SceneTransitionService.ResetForTests();
        HallGlobalStateHost.ResetForTests();
    }

    internal void AssignWorldClicksForTests(WorldClickBinding[] bindings) => worldClicks = bindings ?? Array.Empty<WorldClickBinding>();

    internal bool IsBusyForTests => sequenceBusy;
}
