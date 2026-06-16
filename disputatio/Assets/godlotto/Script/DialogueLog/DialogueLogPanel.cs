using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 대사 로그(백로그) 기능. Fungus 패키지를 수정하지 않고
/// <see cref="WriterSignals.OnWriterState"/> 이벤트를 구독해 진행된 대사를
/// 세션 메모리에 누적하고, 버튼/단축키로 스크롤 패널을 띄운다.
/// </summary>
public class DialogueLogPanel : SingletonMonoBehaviour<DialogueLogPanel>
{
    protected override bool PersistAcrossScenes => true;

    [Header("Visual Style")]
    [SerializeField] DialogueLogVisualStyle visualStyle = DialogueLogVisualStyle.ParchmentCodex;

    [Header("Style Layers")]
    [SerializeField] DialogueLogStyleLayer parchmentLayer;
    [SerializeField] DialogueLogStyleLayer darkConfessionLayer;
    [SerializeField] DialogueLogStyleLayer legacyLayer;

    [Header("UI (legacy fallback)")]
    [Tooltip("스타일 레이어가 비어 있을 때 사용하는 패널 루트.")]
    [SerializeField] private GameObject logPanel;
    [Tooltip("스타일 레이어가 비어 있을 때 사용하는 ScrollRect.")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("스타일 레이어가 비어 있을 때 사용하는 항목 프리팹.")]
    [SerializeField] private GameObject entryPrefab;

    [Header("입력")]
    [SerializeField] private KeyCode logHotkey = KeyCode.L;

    [Header("캔버스 정렬 (SayDialog 위로)")]
    [SerializeField] private string canvasSortingLayerName = "Setting";
    [SerializeField] private int canvasSortingOrder = 60;

    readonly List<DialogueLogEntry> entries = new List<DialogueLogEntry>();
    readonly List<DialogueLogEntry> cheshireEntries = new List<DialogueLogEntry>();
    readonly List<DialogInput> disabledInputs = new List<DialogInput>();
    DialogueLogSayDialogSnapshot sayDialogSnapshot;
    DialogueLogTabBar activeTabBar;
    DialogueLogContentTab activeContentTab = DialogueLogContentTab.Dialogue;
    bool isOpen;

    GameObject activePanelRoot;
    ScrollRect activeScrollRect;
    GameObject activeEntryPrefab;

    public bool IsOpen => isOpen;
    public DialogueLogVisualStyle VisualStyle => visualStyle;
    public DialogueLogContentTab ActiveContentTab => activeContentTab;

    /// <summary>
    /// EditMode 테스트에서 DontDestroyOnLoad 직후 static Instance가 비는 경우를 보정한다.
    /// </summary>
    internal static void EnsureInstanceForTests(DialogueLogPanel panel)
    {
        if (panel != null && Instance != panel)
            Instance = panel;
    }

    /// <summary>
    /// 로그가 Escape로 닫힌 같은 프레임에 설정 패널 Escape 토글이 이어지지 않도록 한다.
    /// </summary>
    internal bool SuppressOtherModalEscapeHandling { get; private set; }

    protected override void OnSingletonAwake()
    {
        ApplyVisualStyle();
        if (activePanelRoot != null)
            activePanelRoot.SetActive(false);
        isOpen = false;
    }

    private void OnEnable()
    {
        WriterSignals.OnWriterState += HandleWriterState;
    }

    private void OnDisable()
    {
        WriterSignals.OnWriterState -= HandleWriterState;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ApplyVisualStyle();
    }
#endif

    private void Update()
    {
        if (SceneManager.GetActiveScene().name == SceneNames.MainMenu)
        {
            if (isOpen) Close();
            return;
        }

        if (Input.GetKeyDown(logHotkey) && !ModalGamePause.IsSettingsOpen)
        {
            Toggle();
            return;
        }

        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            SuppressOtherModalEscapeHandling = true;
            Close();
        }
    }

    private void LateUpdate()
    {
        SuppressOtherModalEscapeHandling = false;
    }

    private void HandleWriterState(Writer writer, WriterState writerState)
    {
        if (writerState != WriterState.End || writer == null)
            return;

        var sayDialog = writer.GetComponent<SayDialog>();
        if (sayDialog == null)
            return;

        DialogueLogLogic.TryAppend(entries, sayDialog.NameText, sayDialog.StoryText);
    }

    /// <summary>체셔 탭에 플레이어 질문을 추가한다.</summary>
    public bool TryAppendCheshirePlayer(string text) =>
        TryAppendCheshire(DialogueLogLogic.CheshirePlayerSpeaker, text);

    /// <summary>체셔 탭에 체셔 최종 응답을 한 번만 추가한다.</summary>
    public bool TryAppendCheshireResponse(string text) =>
        TryAppendCheshire(DialogueLogLogic.CheshireBotSpeaker, text);

    bool TryAppendCheshire(string speaker, string text)
    {
        if (!DialogueLogLogic.TryAppend(cheshireEntries, speaker, text))
            return false;

        if (isOpen && activeContentTab == DialogueLogContentTab.Cheshire)
        {
            BuildContent();
            StartCoroutine(ScrollToBottomNextFrame());
        }

        return true;
    }

    public void SelectContentTab(DialogueLogContentTab tab)
    {
        if (activeContentTab == tab)
            return;

        activeContentTab = tab;
        activeTabBar?.SetSelected(activeContentTab);

        if (!isOpen)
            return;

        BuildContent();
        StartCoroutine(ScrollToBottomNextFrame());
    }

    public void Toggle()
    {
        if (!isOpen && ModalGamePause.IsSettingsOpen)
            return;

        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        ResolveActiveLayer();
        if (isOpen || activePanelRoot == null || ModalGamePause.IsSettingsOpen)
            return;

        ApplyVisualStyle();
        sayDialogSnapshot = DialogueLogSayDialogSnapshot.Capture();

        isOpen = true;
        activePanelRoot.SetActive(true);

        EnsureTabBar();
        activeTabBar?.SetSelected(activeContentTab);
        BuildContent();
        EnsureCanvasSortsAboveSayDialog();
        BlockDialogueAdvance();
        SettingPanelWorldInputBlocker.Begin(activePanelRoot);
        Time.timeScale = 0f;

        StartCoroutine(ScrollToBottomNextFrame());
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        if (activePanelRoot != null)
            activePanelRoot.SetActive(false);

        RestoreDialogueAdvance();
        sayDialogSnapshot.Restore();
        sayDialogSnapshot = default;

        if (ModalGamePause.ShouldEndWorldInputBlocker())
            SettingPanelWorldInputBlocker.End();
        Time.timeScale = ModalGamePause.ResolveTimeScaleOnClose();
    }

    /// <summary>
    /// 인스펙터 드롭다운 변경 시 활성 스타일 레이어·팔레트를 갱신한다.
    /// Play 중 변경해도 다음 Open() 또는 에디터 OnValidate에서 반영된다.
    /// </summary>
    public void ApplyVisualStyle()
    {
        ResolveActiveLayer();
        SetLayerActive(parchmentLayer, visualStyle == DialogueLogVisualStyle.ParchmentCodex);
        SetLayerActive(darkConfessionLayer, visualStyle == DialogueLogVisualStyle.DarkConfession);
        SetLayerActive(legacyLayer, visualStyle == DialogueLogVisualStyle.LegacyNotebook);

        if (activePanelRoot != null)
            DialogueLogPanelStyleApplicator.Apply(activePanelRoot, visualStyle);
    }

    static void SetLayerActive(DialogueLogStyleLayer layer, bool active)
    {
        if (layer?.panelRoot == null)
            return;

        layer.panelRoot.SetActive(active);
    }

    internal DialogueLogStyleLayer ResolveLayer(DialogueLogVisualStyle style) =>
        style switch
        {
            DialogueLogVisualStyle.ParchmentCodex => parchmentLayer,
            DialogueLogVisualStyle.DarkConfession => darkConfessionLayer,
            _ => legacyLayer,
        };

    internal GameObject ResolveEntryPrefab(DialogueLogVisualStyle style)
    {
        var layer = ResolveLayer(style);
        if (layer != null && layer.entryPrefab != null)
            return layer.entryPrefab;

        return entryPrefab;
    }

    void ResolveActiveLayer()
    {
        var layer = ResolveLayer(visualStyle);
        if (layer != null && layer.IsConfigured)
        {
            activePanelRoot = layer.panelRoot;
            activeScrollRect = layer.scrollRect;
            activeEntryPrefab = layer.entryPrefab;
            return;
        }

        activePanelRoot = logPanel;
        activeScrollRect = scrollRect;
        activeEntryPrefab = entryPrefab;
    }

    void BuildContent()
    {
        if (activeScrollRect == null || activeScrollRect.content == null || activeEntryPrefab == null)
            return;

        Transform content = activeScrollRect.content;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            GameObject child = content.GetChild(i).gameObject;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child);
            else
#endif
                Destroy(child);
        }

        var palette = DialogueLogStylePalette.ForStyle(visualStyle);

        IReadOnlyList<DialogueLogEntry> source = GetActiveEntries();
        if (source.Count == 0)
        {
            ShowEmptyState(content);
            return;
        }

        for (int i = 0; i < source.Count; i++)
            InstantiateEntryView(source[i], content, palette);
    }

    void ShowEmptyState(Transform content)
    {
        var emptyGo = new GameObject("EmptyState", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        emptyGo.transform.SetParent(content, false);

        var rect = emptyGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var label = emptyGo.GetComponent<TextMeshProUGUI>();
        label.text = activeContentTab == DialogueLogContentTab.Cheshire
            ? DialogueLogTabSpec.EmptyCheshireText
            : DialogueLogTabSpec.EmptyDialogueText;
        label.alignment = TextAlignmentOptions.Center;
        label.verticalAlignment = VerticalAlignmentOptions.Middle;
        label.color = DialogueLogTabSpec.EmptyTextColor;
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.fontSize = DialogueLogTabSpec.EmptyFontSize;
    }

    IReadOnlyList<DialogueLogEntry> GetActiveEntries() =>
        activeContentTab == DialogueLogContentTab.Cheshire ? cheshireEntries : entries;

    void InstantiateEntryView(DialogueLogEntry entry, Transform content, DialogueLogStylePalette palette)
    {
        GameObject go = Instantiate(activeEntryPrefab, content);
        go.SetActive(true);

        var entryView = go.GetComponent<DialogueLogEntryView>();
        if (entryView != null)
        {
            entryView.Bind(entry, palette);
            return;
        }

        var label = go.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = DialogueLogLogic.FormatEntry(entry);
            DialogueLogTypography.ApplyBody(label, visualStyle);
        }
    }

    void EnsureTabBar()
    {
        if (activePanelRoot == null)
            return;

        activeTabBar = DialogueLogTabBar.EnsureUnder(activePanelRoot.transform, this, visualStyle);
        if (activeTabBar != null)
            activeTabBar.gameObject.SetActive(true);

        if (visualStyle == DialogueLogVisualStyle.ParchmentCodex && activeScrollRect != null)
            DialogueLogParchmentTabLayout.Apply(activePanelRoot.transform, activeScrollRect);
    }

    internal int DialogueEntryCountForTests => entries.Count;

    internal int CheshireEntryCountForTests => cheshireEntries.Count;

    internal bool TryAppendDialogueForTests(string speaker, string text) =>
        DialogueLogLogic.TryAppend(entries, speaker, text);

    internal int ActiveScrollEntryCountForTests =>
        activeScrollRect != null && activeScrollRect.content != null
            ? activeScrollRect.content.childCount
            : 0;

    IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        if (activeScrollRect == null)
            yield break;
        Canvas.ForceUpdateCanvases();
        activeScrollRect.verticalNormalizedPosition = 0f;
    }

    void BlockDialogueAdvance()
    {
        disabledInputs.Clear();
        var inputs = FindObjectsByType<DialogInput>(FindObjectsSortMode.None);
        foreach (DialogInput input in inputs)
        {
            if (input != null && input.enabled)
            {
                input.enabled = false;
                disabledInputs.Add(input);
            }
        }
    }

    void RestoreDialogueAdvance()
    {
        foreach (DialogInput input in disabledInputs)
        {
            if (input != null)
                input.enabled = true;
        }
        disabledInputs.Clear();
    }

    void EnsureCanvasSortsAboveSayDialog()
    {
        if (activePanelRoot == null)
            return;
        Canvas canvas = activePanelRoot.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = activePanelRoot.GetComponent<Canvas>();
        if (canvas == null)
            return;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = canvasSortingLayerName;
        canvas.sortingOrder = canvasSortingOrder;
    }
}
