using UnityEngine;
using Fungus;

public class DeveloperModeController : SingletonMonoBehaviour<DeveloperModeController>
{
    [Header("Toggle Keys")]
    [SerializeField] private KeyCode toggleDevModeKey = KeyCode.F2;
    [SerializeField] private KeyCode toggleOverlayKey = KeyCode.F3;
    [SerializeField] private KeyCode quickRestartKey = KeyCode.F5;
    [SerializeField] private KeyCode skipOpeningKey = KeyCode.F6;

    [Header("Services")]
    [SerializeField] private QuickRestartService quickRestartService;
    [SerializeField] private OpeningSkipService openingSkipService;
    [SerializeField] private InGameDeveloperOverlay developerOverlay;

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
            developerOverlay.ToggleVisible();
        if (Input.GetKeyDown(quickRestartKey))
            quickRestartService.TriggerRestart();
        if (Input.GetKeyDown(skipOpeningKey))
            openingSkipService.SkipOpening();
    }

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
