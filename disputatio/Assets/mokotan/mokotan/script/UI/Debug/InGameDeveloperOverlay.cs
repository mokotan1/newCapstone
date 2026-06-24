using Godlotto.Interaction;
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
    private bool studyRoomSectionExpanded;
    private bool overlayPanelMinimized;

    public bool IsOverlayMinimized => overlayPanelMinimized;

    public void SetOverlayMinimized(bool minimized)
    {
        overlayPanelMinimized = minimized;
    }

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
        {
            itemPickerGui.InvalidateCatalog();
            return;
        }

        overlayPanelMinimized = false;
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

        if (overlayPanelMinimized)
        {
            DrawMinimizedChrome();
            return;
        }

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
        DrawOverlayChromeControls();
        DrawTypographySection();
        GUILayout.Space(guiStyles.ScaledHeight(4f));

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        DrawHeuristicSection();
        DrawQuickActionsSection();
        DrawStudyRoomPuzzleSection();
        itemPickerGui.Draw(guiStyles);
        GUILayout.EndScrollView();

        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, guiStyles.ScaledHeight(24f)));
    }

    private void DrawOverlayChromeControls()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("패널 숨기기 (F3)", guiStyles.Button))
            overlayPanelMinimized = true;
        GUILayout.EndHorizontal();
        GUILayout.Space(guiStyles.ScaledHeight(4f));
    }

    private void DrawMinimizedChrome()
    {
        const float minimizedWidth = 148f;
        const float minimizedHeight = 34f;
        windowRect.width = minimizedWidth;
        windowRect.height = minimizedHeight;

        windowRect = GUILayout.Window(
            GetInstanceID() + 1,
            windowRect,
            DrawMinimizedWindow,
            "Dev QA",
            guiStyles.Window);
    }

    private void DrawMinimizedWindow(int id)
    {
        if (GUILayout.Button("패널 열기 (F3)", guiStyles.Button))
            overlayPanelMinimized = false;

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

    private void DrawStudyRoomPuzzleSection()
    {
        string header = (studyRoomSectionExpanded ? "▼ " : "▶ ") + "StudyRoom 거울 퍼즐 (QA)";
        if (GUILayout.Button(header, guiStyles.Button))
            studyRoomSectionExpanded = !studyRoomSectionExpanded;

        if (!studyRoomSectionExpanded)
            return;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("책갈피 거울 지급", guiStyles.Button))
            cachedDeveloperModeController?.RequestGrantBookmarkMirror();
        if (GUILayout.Button("퍼즐 초기화", guiStyles.Button))
            cachedDeveloperModeController?.RequestResetStudyRoomPuzzle();
        if (GUILayout.Button("강제 성공", guiStyles.Button))
            cachedDeveloperModeController?.RequestForceSolveStudyRoomPuzzle();
        GUILayout.EndHorizontal();

        StudyRoomPuzzleDebugInfo info = StudyRoomPuzzleDevTool.CaptureDebugInfo();

        GUILayout.Label($"BookmarkMirror 보유: {Mark(info.HasBookmarkMirror)}", guiStyles.Label);

        if (!info.IsStudyRoomScene)
        {
            GUILayout.Label("현재 StudyRoom 씬이 아닙니다. (변수 상태만 표시)", guiStyles.Label);
        }

        GUILayout.Label($"DiarySolved: {Mark(info.DiarySolved)}", guiStyles.Label);
        GUILayout.Label($"HaveTutorKey: {Mark(info.HaveTutorKey)}", guiStyles.Label);

        if (info.HasPlacement)
        {
            MirrorPlacementDebug p = info.Placement;
            GUILayout.Label(
                $"판정 — 위치 {Mark(p.PositionPass)}  각도 {Mark(p.AnglePass)}  반사 {Mark(p.ReflectionPass)}",
                guiStyles.Label);
            GUILayout.Label($"밝기 강도: {p.Intensity01 * 100f:0}%  (전체성공 {Mark(p.IsFullSolution)})", guiStyles.Label);
        }
        else
        {
            GUILayout.Label("거울 카드 미배치: 판정 상태 없음", guiStyles.Label);
        }

        GUILayout.Space(guiStyles.ScaledHeight(6f));
    }

    private static string Mark(bool value) => value ? "O" : "X";

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
