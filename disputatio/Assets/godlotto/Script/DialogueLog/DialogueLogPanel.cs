using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Fungus;

/// <summary>
/// 대사 로그(백로그) 기능. Fungus 패키지를 수정하지 않고
/// <see cref="WriterSignals.OnWriterState"/> 이벤트를 구독해 진행된 대사를
/// 세션 메모리에 누적하고, 버튼/단축키로 스크롤 패널을 띄운다.
///
/// 설계는 <c>InGameSettingsPanel</c>의 모달 패널 패턴을 그대로 따른다
/// (싱글톤·씬 유지·입력 차단·캔버스 정렬·timeScale 정지).
/// </summary>
public class DialogueLogPanel : SingletonMonoBehaviour<DialogueLogPanel>
{
    protected override bool PersistAcrossScenes => true;

    [Header("UI")]
    [Tooltip("로그 전체를 담는 패널 루트. 닫혀 있을 때 비활성.")]
    [SerializeField] private GameObject logPanel;
    [Tooltip("대사 항목들이 쌓이는 ScrollRect. Content에 Vertical Layout Group 권장.")]
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("항목 1줄 프리팹. 루트 또는 자식에 TMP_Text가 있어야 한다.")]
    [SerializeField] private GameObject entryPrefab;

    [Header("입력")]
    [SerializeField] private KeyCode logHotkey = KeyCode.L;

    [Header("캔버스 정렬 (SayDialog 위로)")]
    [SerializeField] private string canvasSortingLayerName = "Setting";
    [SerializeField] private int canvasSortingOrder = 60;

    private readonly List<DialogueLogEntry> entries = new List<DialogueLogEntry>();
    private readonly List<DialogInput> disabledInputs = new List<DialogInput>();
    private bool isOpen;

    public bool IsOpen => isOpen;

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
        if (logPanel != null)
            logPanel.SetActive(false);
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

    private void Update()
    {
        // 메인 메뉴에서는 로그를 띄우지 않는다(설정 패널과 동일 가드).
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

    // ---------------------------------------------------------------------
    // 기록: Fungus 대사 1줄이 끝나는 시점(End)에 화자/본문을 캡처
    // ---------------------------------------------------------------------
    private void HandleWriterState(Writer writer, WriterState writerState)
    {
        if (writerState != WriterState.End || writer == null)
            return;

        // Writer와 SayDialog는 동일 GameObject에 위치한다(SayDialog.cs:121).
        var sayDialog = writer.GetComponent<SayDialog>();
        if (sayDialog == null)
            return;

        DialogueLogLogic.TryAppend(entries, sayDialog.NameText, sayDialog.StoryText);
    }

    // ---------------------------------------------------------------------
    // 패널 토글 (InGameSettingsPanel.ToggleSettingPanel 미러링)
    // ---------------------------------------------------------------------
    public void Toggle()
    {
        if (!isOpen && ModalGamePause.IsSettingsOpen)
            return;

        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen || logPanel == null || ModalGamePause.IsSettingsOpen)
            return;

        isOpen = true;
        logPanel.SetActive(true);

        BuildContent();
        EnsureCanvasSortsAboveSayDialog();
        BlockDialogueAdvance();
        SettingPanelWorldInputBlocker.Begin(logPanel);
        Time.timeScale = 0f;

        StartCoroutine(ScrollToBottomNextFrame());
    }

    public void Close()
    {
        if (!isOpen)
            return;

        isOpen = false;
        if (logPanel != null)
            logPanel.SetActive(false);

        RestoreDialogueAdvance();
        if (ModalGamePause.ShouldEndWorldInputBlocker())
            SettingPanelWorldInputBlocker.End();
        Time.timeScale = ModalGamePause.ResolveTimeScaleOnClose();
    }

    // ---------------------------------------------------------------------
    // 콘텐츠 렌더링
    // ---------------------------------------------------------------------
    private void BuildContent()
    {
        if (scrollRect == null || scrollRect.content == null || entryPrefab == null)
            return;

        Transform content = scrollRect.content;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        foreach (DialogueLogEntry entry in entries)
        {
            GameObject go = Instantiate(entryPrefab, content);
            go.SetActive(true);

            var entryView = go.GetComponent<DialogueLogEntryView>();
            if (entryView != null)
            {
                entryView.Bind(entry);
                continue;
            }

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = DialogueLogLogic.FormatEntry(entry);
        }
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        // timeScale=0 에서도 동작하도록 프레임 단위 대기.
        yield return null;
        if (scrollRect == null)
            yield break;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    // ---------------------------------------------------------------------
    // 대사 진행 차단 / 복원 (씬 유지 싱글톤이므로 런타임에 탐색)
    // ---------------------------------------------------------------------
    private void BlockDialogueAdvance()
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

    private void RestoreDialogueAdvance()
    {
        foreach (DialogInput input in disabledInputs)
        {
            if (input != null)
                input.enabled = true;
        }
        disabledInputs.Clear();
    }

    private void EnsureCanvasSortsAboveSayDialog()
    {
        if (logPanel == null)
            return;
        Canvas canvas = logPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = logPanel.GetComponent<Canvas>();
        if (canvas == null)
            return;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = canvasSortingLayerName;
        canvas.sortingOrder = canvasSortingOrder;
    }
}
