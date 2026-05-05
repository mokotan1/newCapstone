using System.Collections;
using Fungus;
using Mokotan.StandingDialogue;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 개발 로그 No.40 — 조건부 1회 독백(저택 첫 입성 / 망령 첫 사망 / 피의 길 확인).
/// PlayerPrefs로 중복 재생을 막습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class No40ConditionalDialogueRunner : MonoBehaviour
{
    public static class PrefsKeys
    {
        public const string MansionHubVisited = "No40_MansionHubVisited";
        public const string FirstEntryPlayed = "No40_FirstEntryPlayed";
        public const string FirstDeathLinePlayed = "No40_FirstDeathLinePlayed";
        public const string BloodPathLinePlayed = "No40_BloodPathLinePlayed";
    }

    private const float DeathLineSceneDelaySeconds = 0.45f;

    public static No40ConditionalDialogueRunner Instance { get; private set; }

    [Header("오디오")]
    [SerializeField] private AudioClip doorSlamClip;

    [Header("대사 (비우면 아래 기본 문구)")]
    [SerializeField] private string firstEntryLine =
        "젠장, 문이 멋대로 닫혀서 열리지 않잖아?";

    [SerializeField] private string firstDeathAfterGhostLine =
        "방금 무슨일이 있었던거지?";

    [SerializeField] private string bloodPathLine =
        "맙소사 이 저택에서 대체 무슨일이 있던거야?";

    [Tooltip("첫 망령 사망 후 독백에 사용할 SayDialog(예: SayDialogNotebook 프리팹). 비우면 씬 내 SayDialogNotebook 검색 후 없으면 스탠딩 UI로 폴백.")]
    [SerializeField] private SayDialog deathLineSayDialogPrefab;

    private SayDialog _deathNotebookRuntimeInstance;
    private bool _deathNotebookInstantiated;

    private Coroutine _firstEntryRoutine;
    private bool _pendingDeathMonologue;
    private bool _bloodRoutineActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_deathNotebookInstantiated && _deathNotebookRuntimeInstance != null)
            Destroy(_deathNotebookRuntimeInstance.gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        JumpscareManager.OnPlayerDied += HandlePlayerDied;
        SpecialJumpscareManager.OnPlayerDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        JumpscareManager.OnPlayerDied -= HandlePlayerDied;
        SpecialJumpscareManager.OnPlayerDied -= HandlePlayerDied;
    }

    public static No40ConditionalDialogueRunner EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject(nameof(No40ConditionalDialogueRunner));
        return go.AddComponent<No40ConditionalDialogueRunner>();
    }

    /// <summary>Hall_playerble 훅에서 인스펙터 값을 넘겨 덮어씁니다.</summary>
    public void ApplyDesignerOverrides(AudioClip slam,
        string entryLine, string deathLine, string bloodLine,
        SayDialog deathSayDialogPrefab = null)
    {
        if (slam != null)
            doorSlamClip = slam;
        if (!string.IsNullOrWhiteSpace(entryLine))
            firstEntryLine = entryLine;
        if (!string.IsNullOrWhiteSpace(deathLine))
            firstDeathAfterGhostLine = deathLine;
        if (!string.IsNullOrWhiteSpace(bloodLine))
            bloodPathLine = bloodLine;
        if (deathSayDialogPrefab != null)
            deathLineSayDialogPrefab = deathSayDialogPrefab;
    }

    /// <summary>저택 허브 최초 방문 플래그 + 첫 입장 독백 스케줄.</summary>
    public void RegisterMansionHubVisitAndTryFirstEntry()
    {
        PlayerPrefs.SetInt(PrefsKeys.MansionHubVisited, 1);
        PlayerPrefs.Save();

        if (PlayerPrefs.GetInt(PrefsKeys.FirstEntryPlayed, 0) != 0)
            return;

        if (_firstEntryRoutine != null)
            return;

        _firstEntryRoutine = StartCoroutine(FirstEntryRoutine());
    }

    /// <summary>Hallway_Right2 피의 길(전등 켜진 복도 조명 스프라이트) 클릭 시.</summary>
    public void TryPlayBloodPathLine()
    {
        if (_bloodRoutineActive)
            return;

        if (PlayerPrefs.GetInt(PrefsKeys.BloodPathLinePlayed, 0) != 0)
            return;

        if (!FlowchartLocator.GetFungusGlobalBoolean(FungusVariableKeys.ElectricOn))
            return;

        _bloodRoutineActive = true;
        StartCoroutine(PlayBloodPathRoutine());
    }

    private IEnumerator FirstEntryRoutine()
    {
        yield return null;

        if (PlayerPrefs.GetInt(PrefsKeys.FirstEntryPlayed, 0) != 0)
        {
            _firstEntryRoutine = null;
            yield break;
        }

        if (doorSlamClip != null)
        {
            Vector3 p = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(doorSlamClip, p);
        }

        yield return null;

        yield return PlayLineWithNotebookSayDialog(firstEntryLine, () =>
        {
            PlayerPrefs.SetInt(PrefsKeys.FirstEntryPlayed, 1);
            PlayerPrefs.Save();
        });

        _firstEntryRoutine = null;
    }

    private IEnumerator PlayBloodPathRoutine()
    {
        try
        {
            yield return PlayLineWithNotebookSayDialog(bloodPathLine, () =>
            {
                PlayerPrefs.SetInt(PrefsKeys.BloodPathLinePlayed, 1);
                PlayerPrefs.Save();
            });
        }
        finally
        {
            _bloodRoutineActive = false;
        }
    }

    private void HandlePlayerDied()
    {
        if (PlayerPrefs.GetInt(PrefsKeys.MansionHubVisited, 0) == 0)
            return;
        if (PlayerPrefs.GetInt(PrefsKeys.FirstDeathLinePlayed, 0) != 0)
            return;

        _pendingDeathMonologue = true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_pendingDeathMonologue)
            return;
        if (PlayerPrefs.GetInt(PrefsKeys.FirstDeathLinePlayed, 0) != 0)
        {
            _pendingDeathMonologue = false;
            return;
        }

        _pendingDeathMonologue = false;
        StartCoroutine(DeathLineAfterLoadRoutine());
    }

    private IEnumerator DeathLineAfterLoadRoutine()
    {
        yield return new WaitForSecondsRealtime(DeathLineSceneDelaySeconds);

        if (PlayerPrefs.GetInt(PrefsKeys.FirstDeathLinePlayed, 0) != 0)
            yield break;

        yield return PlayLineWithNotebookSayDialog(firstDeathAfterGhostLine, () =>
        {
            PlayerPrefs.SetInt(PrefsKeys.FirstDeathLinePlayed, 1);
            PlayerPrefs.Save();
        });
    }

    private IEnumerator PlayLineWithNotebookSayDialog(string line, System.Action onRecordedDone)
    {
        SayDialog dlg = GetOrCreateDeathNotebookSayDialog();
        if (dlg == null)
        {
            yield return PlayStandingLine(line, onRecordedDone);
            yield break;
        }

        // 잔존 standing 캔버스가 떠 있으면 숨겨 두 UI 겹침 방지
        StandingDialogueManager standing = StandingDialogueManager.Instance;
        if (standing != null) standing.HideAll();

        bool finished = false;
        SayDialog.ActiveSayDialog = dlg;
        dlg.gameObject.SetActive(true);
        dlg.SetCharacterName(string.Empty, Color.white);
        dlg.Say(line, true, true, true, false, false, null, () => finished = true);

        while (!finished)
            yield return null;

        onRecordedDone?.Invoke();
    }

    private SayDialog GetOrCreateDeathNotebookSayDialog()
    {
        if (_deathNotebookRuntimeInstance != null)
            return _deathNotebookRuntimeInstance;

        if (deathLineSayDialogPrefab != null)
        {
            if (deathLineSayDialogPrefab.gameObject.scene.IsValid())
            {
                _deathNotebookRuntimeInstance = deathLineSayDialogPrefab;
                _deathNotebookInstantiated = false;
                return _deathNotebookRuntimeInstance;
            }

            GameObject go = Instantiate(deathLineSayDialogPrefab.gameObject);
            go.name = deathLineSayDialogPrefab.gameObject.name;
            go.SetActive(false);
            DontDestroyOnLoad(go);
            _deathNotebookRuntimeInstance = go.GetComponent<SayDialog>();
            _deathNotebookInstantiated = _deathNotebookRuntimeInstance != null;
            return _deathNotebookRuntimeInstance;
        }

        GameObject found = GameObject.Find("SayDialogNotebook");
        if (found != null)
        {
            _deathNotebookRuntimeInstance = found.GetComponent<SayDialog>();
            _deathNotebookInstantiated = false;
        }

        return _deathNotebookRuntimeInstance;
    }

    private static IEnumerator PlayStandingLine(string line, System.Action onRecordedDone)
    {
        StandingDialogueManager mgr = StandingDialogueManager.GetStandingDialogue();
        if (mgr == null)
            yield break;

        bool finished = false;
        mgr.ClearStandingCharacterSlots();
        mgr.Speak(Side.Left, string.Empty, line, TypographySettings.Default, () => finished = true);

        while (!finished)
            yield return null;

        mgr.HideAll();
        onRecordedDone?.Invoke();
    }
}
