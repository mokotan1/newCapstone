using UnityEngine;
using UnityEngine.SceneManagement;
using Fungus;

public class WhenClikcedButton : SingletonMonoBehaviour<WhenClikcedButton>
{
    protected override bool PersistAcrossScenes => true;

    [Header("UI 연결")]
    [SerializeField] private GameObject panelToActivate;
    [Tooltip("비우면 씬에서 이름으로 GlobalInventoryCanvas_Prefab을 찾습니다.")]
    [SerializeField] private GameObject globalInventoryCanvas;

    [Header("Room Objects (Panel의 자식으로 설정 필수)")]
    [SerializeField] private GameObject unLocked;
    [SerializeField] private GameObject kitchen;
    [SerializeField] private GameObject locked;
    [SerializeField] private GameObject lib;
    [SerializeField] private GameObject lock2;
    [SerializeField] private GameObject made;
    [SerializeField] private GameObject bed;
    [SerializeField] private GameObject wife;
    [SerializeField] private GameObject Child;
    [SerializeField] private GameObject Tutor;
    [SerializeField] private GameObject Lock_2_1;
    [SerializeField] private GameObject Lock_2_2;
    [SerializeField] private GameObject Lock_2_3;
    [SerializeField] private GameObject Lock_2_4;

    [Header("현재 위치 클릭 피드백")]
    [Tooltip("같은 방을 눌렀을 때 메시지를 띄울 SayDialogNotebook 프리펩 또는 씬 인스턴스입니다.")]
    [SerializeField] private SayDialog currentLocationSayDialogNotebook;
    [SerializeField] private string currentLocationMessage = "현재 위치입니다";

    private Transform targetCanvas;
    private Flowchart globalFlowchart; // Variablemanager의 변수를 가져올 용도

    protected override void OnSingletonAwake()
    {
        if (panelToActivate != null)
        {
            panelToActivate.transform.SetParent(this.transform);
            panelToActivate.SetActive(false);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        FindGlobalManager();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshReferences();
    }

    private void Update()
    {
        // 일부 경로(단축키/다른 스크립트)로 지도가 켜질 때 OnOpenMapClick을 거치지 않을 수 있어
        // 패널이 열린 동안에는 방 상태를 지속 동기화합니다.
        if (panelToActivate == null || !panelToActivate.activeInHierarchy)
            return;

        if (IsJumpscareBlockingMap())
        {
            OnCloseMapClick();
            return;
        }

        if (globalFlowchart == null)
            FindGlobalManager();

        UpdateAllRoomStates();
    }

    private void FindGlobalManager()
    {
        // 캐시가 씬 전환 후에도 유효하지만, Variablemanager 재탐색이 안전합니다.
        globalFlowchart = FlowchartLocator.Find();
    }

    private void RefreshReferences()
    {
        // 전역 인벤토리 캔버스를 우선적으로 찾습니다.
        GameObject invCanvas = globalInventoryCanvas;
        if (invCanvas == null)
            invCanvas = GameObject.Find("GlobalInventoryCanvas_Prefab");
        if (invCanvas != null)
        {
            targetCanvas = invCanvas.transform;
        }
        else
        {
            Canvas c = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Exclude);
            if (c != null) targetCanvas = c.transform;
        }

        // 매 씬 로드마다 Variablemanager Flowchart를 다시 묶어 잘못된 차트·stale 참조를 방지합니다.
        FindGlobalManager();

        // Variablemanager를 못 찾는 씬에서도 전역(Fungus Global) fallback으로 상태를 갱신한다.
        if (globalFlowchart != null)
            globalFlowchart.SetBooleanVariable(FungusVariableKeys.IsClicked, false);

        UpdateAllRoomStates(); // 씬 로드 시 상태 갱신
    }

    public void OnOpenMapClick()
    {
        if (IsJumpscareBlockingMap())
            return;

        if (targetCanvas == null) RefreshReferences();

        if (panelToActivate != null && targetCanvas != null)
        {
            // 열기 전 최신 변수 상태 반영
            UpdateAllRoomStates();

            panelToActivate.SetActive(true);
            panelToActivate.transform.SetParent(targetCanvas, false); //

            RectTransform rect = panelToActivate.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.SetAsLastSibling();
            }

            if (globalFlowchart != null) globalFlowchart.SetBooleanVariable(FungusVariableKeys.IsClicked, true);
        }
    }

    public void OnCloseMapClick()
    {
        if (panelToActivate != null)
        {
            panelToActivate.SetActive(false);
            // 씬 이동 시 파괴되지 않게 다시 싱글톤 자식으로 회수
            panelToActivate.transform.SetParent(this.transform);
        }

        if (globalFlowchart != null) globalFlowchart.SetBooleanVariable(FungusVariableKeys.IsClicked, false);
        ClickInteractionCleanup.ResetAfterUiBoundary(globalFlowchart);
    }

    public void MoveScene(string sceneName)
    {
        if (MapSceneNavigationGuard.ShouldBlockSceneLoad(SceneManager.GetActiveScene().name, sceneName))
        {
            currentLocationSayDialogNotebook =
                MapCurrentLocationFeedback.Show(currentLocationSayDialogNotebook, currentLocationMessage);
            return;
        }

        OnCloseMapClick();
        SceneManager.LoadScene(sceneName); //
    }

    // 씬 이동용 함수들
    public void GoKitchen() => MoveScene(SceneNames.Kitchen);
    public void GoMaid() => MoveScene(SceneNames.MaidRoom);
    public void GoStudy() => MoveScene(SceneNames.StudyRoom);
    public void GoTutor() => MoveScene(SceneNames.TutorRoom);
    public void GoChild() => MoveScene(SceneNames.ChildRoom);
    public void GoWife() => MoveScene(SceneNames.WifeRoom);
    public void GoBed() => MoveScene(SceneNames.BedRoom);

    public void UpdateAllRoomStates()
    {
        // 각 방의 상태를 변수에 맞춰 강제로 고정합니다.
        UpdateRoomVisibility(unLocked, kitchen, FungusVariableKeys.ElectricOn);
        UpdateRoomVisibility(locked, lib, FungusVariableKeys.UsedStudyKey);
        UpdateRoomVisibility(lock2, made, FungusVariableKeys.UsedMaidKey);
        UpdateRoomVisibility(Lock_2_4, bed, FungusVariableKeys.UsedBedKey);
        UpdateRoomVisibility(Lock_2_3, wife, FungusVariableKeys.UsedWifeKey);
        UpdateRoomVisibility(Lock_2_2, Tutor, FungusVariableKeys.UsedTutorKey);
        UpdateRoomVisibility(Lock_2_1, Child, FungusVariableKeys.UsedChildKey);
    }

    // ★ 핵심 수정 부분: 변수가 false일 때도 상태를 제어합니다.
    private void UpdateRoomVisibility(GameObject lockObj, GameObject openObj, string key)
    {
        bool isOpen = CheckVar(key);

        // 방이 열렸다면 자물쇠를 끄고 방을 켬, 반대면 자물쇠를 켜고 방을 끔
        if (lockObj != null) lockObj.SetActive(!isOpen);
        if (openObj != null) openObj.SetActive(isOpen);

        // 일부 맵 노드는 open 오브젝트 내부에 잠금 배지(자식 UI)가 같이 들어있다.
        // 전역 bool이 true인 경우 배지를 숨겨 "열림" 상태가 시각적으로 보이도록 강제한다.
        if (openObj != null && key == FungusVariableKeys.UsedMaidKey)
            ToggleLockBadgeChildren(openObj, !isOpen);
    }

    private static void ToggleLockBadgeChildren(GameObject root, bool showLockBadge)
    {
        if (root == null) return;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == root.transform) continue;

            string n = t.name;
            if (string.IsNullOrEmpty(n)) continue;

            string lower = n.ToLowerInvariant();
            if (lower.Contains("lock") || lower.Contains("자물쇠"))
                t.gameObject.SetActive(showLockBadge);
        }
    }

    private bool CheckVar(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        // 1) 씬의 Variablemanager(Flowchart) 값을 우선
        if (globalFlowchart != null && globalFlowchart.GetBooleanVariable(key))
            return true;

        // 2) 변수 항목 누락/씬 교체 시에도 Fungus 전역 저장소를 fallback으로 조회
        return FlowchartLocator.GetFungusGlobalBoolean(key);
    }

    private static bool IsJumpscareBlockingMap()
    {
        JumpscareManager j = JumpscareManager.Instance;
        return j != null && j.IsGhostEncounterBlockingMapUi();
    }
}

public static class MapSceneNavigationGuard
{
    public static bool ShouldBlockSceneLoad(string currentSceneName, string targetSceneName)
    {
        if (string.IsNullOrEmpty(targetSceneName))
            return false;

        return string.Equals(currentSceneName, targetSceneName, System.StringComparison.Ordinal);
    }
}

public static class MapCurrentLocationFeedback
{
    public static SayDialog Show(SayDialog mappedDialog, string message)
    {
        SayDialog sayDialog = ResolveRuntimeDialog(mappedDialog);

        if (sayDialog == null)
            return mappedDialog;

        EnsureActiveInHierarchy(sayDialog);
        SayDialog.ActiveSayDialog = sayDialog;
        sayDialog.SetCharacterImage(null);
        sayDialog.SetCharacterName(string.Empty, Color.white);
        sayDialog.Say(string.IsNullOrEmpty(message) ? "현재 위치입니다" : message, true, true, true, true, false, null, null);

        return sayDialog;
    }

    private static SayDialog ResolveRuntimeDialog(SayDialog mappedDialog)
    {
        if (mappedDialog == null)
            return null;

        if (mappedDialog.gameObject.scene.IsValid())
            return mappedDialog;

        SayDialog instance = Object.Instantiate(mappedDialog);
        instance.name = mappedDialog.name;
        Object.DontDestroyOnLoad(instance.gameObject);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private static void EnsureActiveInHierarchy(SayDialog sayDialog)
    {
        for (Transform t = sayDialog.transform; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
        }
    }
}
