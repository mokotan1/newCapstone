using UnityEngine;

public class InGameDeveloperOverlay : MonoBehaviour
{
    static readonly Vector2 DefaultWindowSize = new Vector2(520f, 640f);

    [SerializeField] private bool visible = true;
    [SerializeField] private Rect windowRect = new Rect(24f, 24f, DefaultWindowSize.x, DefaultWindowSize.y);

    private HeuristicDebugSnapshot latestSnapshot;
    private Vector2 scrollPosition;
    private DeveloperModeController cachedDeveloperModeController;
    private readonly DeveloperModeItemPickerGui itemPickerGui = new DeveloperModeItemPickerGui();
    private readonly DeveloperModeGuiStyles guiStyles = new DeveloperModeGuiStyles();
    private float pendingFontSize;
    private bool fontSizeApplyPending;

    private void OnEnable()
    {
        PromptInfoBudgetComposer.OnSnapshotUpdated += HandleSnapshotUpdated;
        cachedDeveloperModeController =
            FindFirstObjectByType<DeveloperModeController>(FindObjectsInactive.Include);

        DeveloperModeGuiTypography.Load();
        pendingFontSize = DeveloperModeGuiTypography.FontSize;
        ApplyFontSizeIfNeeded(force: true);
    }

    private void OnDisable()
    {
        PromptInfoBudgetComposer.OnSnapshotUpdated -= HandleSnapshotUpdated;
    }

    public bool IsVisible => visible;

    public void ToggleVisible()
    {
        visible = !visible;
    }

    public void SetVisible(bool isVisible)
    {
        visible = isVisible;
        if (isVisible)
            itemPickerGui.InvalidateCatalog();
    }

    private void HandleSnapshotUpdated(HeuristicDebugSnapshot snapshot)
    {
        latestSnapshot = snapshot;
    }

    private void OnGUI()
    {
        if (!visible || !DeveloperModeController.IsDeveloperModeEnabled)
            return;

        if (Event.current == null || GUI.skin == null)
            return;

        guiStyles.EnsureBuilt(DeveloperModeGuiTypography.FontSize);

        windowRect = GUILayout.Window(
            GetInstanceID(),
            windowRect,
            DrawWindow,
            "Developer Mode Console",
            guiStyles.Window);

        if (fontSizeApplyPending)
        {
            ApplyFontSizeIfNeeded(force: true);
            fontSizeApplyPending = false;
        }
    }

    private void DrawWindow(int id)
    {
        DrawTypographySection();
        GUILayout.Space(guiStyles.ScaledHeight(4f));

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        DrawHeuristicSection();
        DrawQuickActionsSection();
        itemPickerGui.Draw(guiStyles);
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, guiStyles.ScaledHeight(24f)));
    }

    private void DrawTypographySection()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("글자 크기", guiStyles.Label, GUILayout.Width(guiStyles.ScaledWidth(72f)));

        float newSize = GUILayout.HorizontalSlider(
            pendingFontSize,
            DeveloperModeGuiTypography.MinFontSize,
            DeveloperModeGuiTypography.MaxFontSize,
            GUILayout.ExpandWidth(true));

        GUILayout.Label($"{Mathf.RoundToInt(newSize)}pt", guiStyles.Label, GUILayout.Width(guiStyles.ScaledWidth(44f)));
        GUILayout.EndHorizontal();

        if (Mathf.Approximately(newSize, pendingFontSize))
            return;

        pendingFontSize = newSize;
        fontSizeApplyPending = true;
    }

    private void DrawHeuristicSection()
    {
        if (latestSnapshot == null)
        {
            GUILayout.Label("Heuristic snapshot: 아직 없음", guiStyles.Label);
            return;
        }

        GUILayout.Label($"Room: {latestSnapshot.roomName}", guiStyles.Label);
        GUILayout.Label($"Level: {latestSnapshot.level}", guiStyles.Label);
        GUILayout.Label($"Skill: {latestSnapshot.skillScore:0.000}", guiStyles.Label);
        GUILayout.Label($"Progress: {latestSnapshot.progressScore:0.000}", guiStyles.Label);
        GUILayout.Label($"Accuracy: {latestSnapshot.accuracyScore:0.000}", guiStyles.Label);
        GUILayout.Label($"Stuck: {latestSnapshot.stuckScore:0.000}", guiStyles.Label);
        GUILayout.Label($"RevisitCount: {latestSnapshot.unsolvedRevisitCount}", guiStyles.Label);
        GUILayout.Label($"RevisitIntervalSec: {latestSnapshot.revisitIntervalSeconds:0.0}", guiStyles.Label);
        GUILayout.Label($"NoProgressAfterRevisit: {latestSnapshot.noProgressAfterRevisitCount}", guiStyles.Label);
        GUILayout.Label($"Reason: {latestSnapshot.reason}", guiStyles.Label);
        GUILayout.Label($"GeneratedAt: {latestSnapshot.generatedAtUtc}", guiStyles.Label);
        GUILayout.Space(guiStyles.ScaledHeight(6f));
    }

    private void DrawQuickActionsSection()
    {
        GUILayout.Label("F5: Quick Restart   F6: Opening Skip", guiStyles.Label);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Quick Restart", guiStyles.Button))
            cachedDeveloperModeController?.RequestQuickRestart();

        if (GUILayout.Button("Skip Opening", guiStyles.Button))
            cachedDeveloperModeController?.RequestSkipOpening();
        GUILayout.EndHorizontal();
        GUILayout.Space(guiStyles.ScaledHeight(6f));
    }

    private void ApplyFontSizeIfNeeded(bool force)
    {
        if (!force && Mathf.Approximately(pendingFontSize, DeveloperModeGuiTypography.FontSize))
            return;

        DeveloperModeGuiTypography.SetFontSize(pendingFontSize);
        guiStyles.Rebuild(pendingFontSize);
        ApplyWindowSizeForFont(pendingFontSize);
    }

    private void ApplyWindowSizeForFont(float fontSize)
    {
        float scale = fontSize / DeveloperModeGuiTypography.ReferenceFontSize;
        windowRect.width = Mathf.Max(DefaultWindowSize.x, DefaultWindowSize.x * scale);
        windowRect.height = Mathf.Max(DefaultWindowSize.y, DefaultWindowSize.y * scale);
    }
}
