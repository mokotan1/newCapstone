using UnityEngine;
using Fungus;

public class DeveloperModeController : SingletonMonoBehaviour<DeveloperModeController>
{
    [Header("Toggle Keys")]
    [SerializeField] private KeyCode toggleDevModeKey = KeyCode.F2;
    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F3;
    [SerializeField] private KeyCode quickRestartKey = KeyCode.F5;
    [SerializeField] private KeyCode skipOpeningKey = KeyCode.F6;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private KeyCode toggleQaPanelKey = KeyCode.F7;
#endif

    [Header("Services")]
    [SerializeField] private QuickRestartService quickRestartService;
    [SerializeField] private OpeningSkipService openingSkipService;
    [SerializeField] private InGameDeveloperOverlay developerOverlay;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private Godlotto.QA.Gateway.QaDeveloperPanel qaDeveloperPanel;
#endif

    public static bool IsDeveloperModeEnabled { get; private set; }

#if UNITY_INCLUDE_TESTS
    internal static bool? RuntimeAvailabilityOverrideForTests { get; set; }
#endif

    /// <summary>
    /// 에디터·Development Build·<c>ENABLE_DEVELOPER_MODE</c>에서만 Dev Mode 런타임(F2/F3 등)을 허용합니다.
    /// 릴리즈 빌드에서는 false입니다.
    /// </summary>
    public static bool CanUseDeveloperModeRuntime
    {
        get
        {
#if UNITY_INCLUDE_TESTS
            if (RuntimeAvailabilityOverrideForTests.HasValue)
                return RuntimeAvailabilityOverrideForTests.Value;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ENABLE_DEVELOPER_MODE
            return true;
#else
            return false;
#endif
        }
    }

    protected override bool PersistAcrossScenes => true;

    protected override void OnSingletonAwake()
    {
        IsDeveloperModeEnabled = CanUseDeveloperModeRuntime && ReadDevModeFromVariableManager();
    }

    private void OnEnable()
    {
        JumpscareManager.OnPlayerDied += HandlePlayerDied;
        SpecialJumpscareManager.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        JumpscareManager.OnPlayerDied -= HandlePlayerDied;
        SpecialJumpscareManager.OnPlayerDied -= HandlePlayerDied;
    }

    private void Start()
    {
        EnsureDeveloperOverlay();
        EnsureServices();

        if (developerOverlay != null)
            developerOverlay.SetVisible(IsDeveloperModeEnabled);
    }

    private void Update()
    {
        if (!CanUseDeveloperModeRuntime)
            return;

        if (Input.GetKeyDown(toggleDevModeKey))
            ToggleDeveloperMode();

        if (!IsDeveloperModeEnabled)
            return;

        if (Input.GetKeyDown(toggleOverlayKey) && developerOverlay != null)
        {
            if (developerOverlay.IsVisible && developerOverlay.IsOverlayMinimized)
                developerOverlay.SetOverlayMinimized(false);
            else
                developerOverlay.ToggleVisible();
        }
        if (Input.GetKeyDown(quickRestartKey))
            quickRestartService.TriggerRestart();
        if (Input.GetKeyDown(skipOpeningKey))
            openingSkipService.SkipOpening();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Input.GetKeyDown(toggleQaPanelKey))
            ToggleQaDeveloperPanel();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// QA 개발자 패널(Task 10)을 켜고 끕니다. 패널은 <c>QaCommandGatewayHost</c>가 소유하는
    /// 공유 게이트웨이만 호출하므로, 이 컨트롤러는 QA 코어에 대해 아무 것도 소유하거나
    /// dispose하지 않습니다 — 여기서 하는 일은 오직 GameObject 생명주기와 표시 여부뿐입니다.
    /// </summary>
    public void ToggleQaDeveloperPanel()
    {
        if (!IsDeveloperModeEnabled)
            return;

        EnsureQaDeveloperPanel();
        qaDeveloperPanel?.ToggleVisible();
    }

    private void EnsureQaDeveloperPanel()
    {
        if (qaDeveloperPanel != null)
            return;

        EnsurePlayerCommandGatewayFactoryInstalled();

        qaDeveloperPanel = FindFirstObjectByType<Godlotto.QA.Gateway.QaDeveloperPanel>(FindObjectsInactive.Include);
        if (qaDeveloperPanel == null)
        {
            var panelObject = new GameObject("QaDeveloperPanel");
            qaDeveloperPanel = panelObject.AddComponent<Godlotto.QA.Gateway.QaDeveloperPanel>();
            if (Application.isPlaying)
                DontDestroyOnLoad(panelObject);
        }

        // Godlotto.QA.UI cannot reference this default-assembly type (that would be a circular
        // assembly reference), so readiness flags are pushed in here instead (DIP).
        qaDeveloperPanel.ConfigureReadinessProviders(
            () => CanUseDeveloperModeRuntime,
            () => IsDeveloperModeEnabled);
    }

    /// <summary>
    /// 순수 standalone development player 빌드(Editor 어셈블리가 존재하지 않는 빌드)에서는
    /// <c>QaEditorCommandGatewayInstaller</c>의 <c>[InitializeOnLoad]</c> 팩토리가 절대 실행되지
    /// 않으므로, <see cref="Godlotto.QA.Gateway.QaCommandGatewayHost"/>는 자체 기본값
    /// (<c>QaCommandGateway.CreateFallbackProfileService</c> — mutation을 모두 거부하는 안전한
    /// no-op)으로 대체합니다. 이 컨트롤러(Assembly-CSharp; <c>QaProfileService</c>/
    /// <c>PlayDataPrefsCleaner</c>에 접근 가능한 몇 안 되는 어셈블리 중 하나)가 대신 진짜
    /// 팩토리를 설치하여, standalone 빌드의 QA 패널도 Editor CLI와 동등하게 실제 PlayerPrefs
    /// 격리를 받도록 합니다. Editor(Play Mode 포함)에서는 아무 것도 하지 않으므로
    /// <c>QaEditorCommandGatewayInstaller</c>가 설치한 팩토리를 덮어쓸 위험이 없습니다.
    /// </summary>
    private static void EnsurePlayerCommandGatewayFactoryInstalled()
    {
#if !UNITY_EDITOR && DEVELOPMENT_BUILD
        Godlotto.QA.Gateway.QaCommandGatewayHost.InstallFactory(CreatePlayerCommandGateway);
#endif
    }

#if !UNITY_EDITOR && DEVELOPMENT_BUILD
    private static Godlotto.QA.Gateway.QaCommandGateway CreatePlayerCommandGateway()
    {
        var recorder = Godlotto.QA.Evidence.DevelopmentQaEvidenceRecorder.CreateDefault();
        var profileService = new Godlotto.QA.Profile.QaProfileService(
            Godlotto.QA.Profile.QaFileProfileMarkerStore.CreateDefault());

        return new Godlotto.QA.Gateway.QaCommandGateway(
            recorder,
            () => recorder.RunDirectoryPath,
            profileService: profileService);
    }
#endif
#endif

    public void ToggleDeveloperMode()
    {
        if (!CanUseDeveloperModeRuntime)
            return;

        SetDeveloperModeEnabled(!IsDeveloperModeEnabled);
    }

    private void HandlePlayerDied()
    {
        if (!IsDeveloperModeEnabled)
            return;

        quickRestartService.TriggerRestart();
    }

    public void RequestSkipOpening()
    {
        if (IsDeveloperModeEnabled)
            openingSkipService.SkipOpening();
    }

    public void RequestQuickRestart()
    {
        if (IsDeveloperModeEnabled)
            quickRestartService.TriggerRestart();
    }

    public void RequestGrantAllItems()
    {
        if (IsDeveloperModeEnabled)
            DeveloperModeItemGrantService.GrantAllItems();
    }

    public DeveloperModeItemSelectionGrantResult RequestGrantSelectedItem(Item item, int quantity)
    {
        if (!IsDeveloperModeEnabled)
        {
            return new DeveloperModeItemSelectionGrantResult
            {
                WasBlockedByDevMode = true,
                FailureReason = "개발자 모드가 꺼져 있습니다.",
            };
        }

        return DeveloperModeItemGrantService.GrantSelectedItem(item, quantity);
    }

    /// <summary>서재 거울 퍼즐 QA — BookmarkMirror 즉시 지급.</summary>
    public DeveloperModeItemSelectionGrantResult RequestGrantBookmarkMirror()
    {
        if (!IsDeveloperModeEnabled)
        {
            return new DeveloperModeItemSelectionGrantResult
            {
                WasBlockedByDevMode = true,
                FailureReason = "개발자 모드가 꺼져 있습니다.",
            };
        }

        return StudyRoomPuzzleDevTool.GrantBookmarkMirror();
    }

    /// <summary>서재 거울 퍼즐 QA — DiarySolved/HaveTutorKey 초기화.</summary>
    public bool RequestResetStudyRoomPuzzle()
    {
        return IsDeveloperModeEnabled && StudyRoomPuzzleDevTool.ResetPuzzle();
    }

    /// <summary>서재 거울 퍼즐 QA — 강제 성공(기존 SuccessRouter 흐름 재사용).</summary>
    public bool RequestForceSolveStudyRoomPuzzle()
    {
        return IsDeveloperModeEnabled && StudyRoomPuzzleDevTool.ForceSolve();
    }

    private void SetDeveloperModeEnabled(bool enabled)
    {
        IsDeveloperModeEnabled = enabled;
        WriteDevModeToVariableManager(enabled);
        EnsureDeveloperOverlay();

        if (developerOverlay != null)
            developerOverlay.SetVisible(enabled);
    }

    private void EnsureDeveloperOverlay()
    {
        if (developerOverlay != null)
            return;

        developerOverlay = FindFirstObjectByType<InGameDeveloperOverlay>(FindObjectsInactive.Include);
        if (developerOverlay != null)
            return;

        var overlayObject = new GameObject("InGameDeveloperOverlay");
        developerOverlay = overlayObject.AddComponent<InGameDeveloperOverlay>();
        if (Application.isPlaying)
            DontDestroyOnLoad(overlayObject);
    }

    private void EnsureServices()
    {
        if (quickRestartService == null)
            quickRestartService = GetComponent<QuickRestartService>() ?? gameObject.AddComponent<QuickRestartService>();
        if (openingSkipService == null)
            openingSkipService = GetComponent<OpeningSkipService>() ?? gameObject.AddComponent<OpeningSkipService>();
    }

#if UNITY_INCLUDE_TESTS
    internal static void ResetTestOverrides()
    {
        RuntimeAvailabilityOverrideForTests = null;
        IsDeveloperModeEnabled = false;
    }

    internal static void SetIsDeveloperModeEnabledForTests(bool enabled)
    {
        IsDeveloperModeEnabled = enabled;
    }
#endif

    private static bool ReadDevModeFromVariableManager()
    {
        Flowchart flowchart = FlowchartLocator.Find();
        if (flowchart == null)
            return false;

        EnsureDevModeVariableExists(flowchart);
        return flowchart.GetBooleanVariable(FungusVariableKeys.DevModeEnabled);
    }

    private static void WriteDevModeToVariableManager(bool enabled)
    {
        Flowchart flowchart = FlowchartLocator.Find();
        if (flowchart == null)
            return;

        EnsureDevModeVariableExists(flowchart);
        flowchart.SetBooleanVariable(FungusVariableKeys.DevModeEnabled, enabled);
    }

    private static void EnsureDevModeVariableExists(Flowchart flowchart)
    {
        if (flowchart == null || flowchart.HasVariable(FungusVariableKeys.DevModeEnabled))
            return;

        var variable = flowchart.gameObject.AddComponent<BooleanVariable>();
        variable.Key = FungusVariableKeys.DevModeEnabled;
        variable.Scope = VariableScope.Public;
        variable.Value = false;
        flowchart.Variables.Add(variable);
        GameLog.Log($"[DeveloperModeController] Variablemanager에 bool 변수 '{FungusVariableKeys.DevModeEnabled}'를 추가했습니다.");
    }
}
