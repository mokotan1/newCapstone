using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fungus;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class DraggableSnap2D : MonoBehaviour
{
    public SnapKind kind = SnapKind.Any;
    public bool lockAfterSnap = true;

    [Header("Fungus 연동 (선택)")]
    public Flowchart targetFlowchart;
    public string fungusVarName = "";

    [HideInInspector] public bool isSnapped = false;
    [HideInInspector] public Rigidbody2D rb;
    [HideInInspector] public Vector3 originalPos;

    private SpriteRenderer sr;
    private const string SnapSaveKeyPrefix = "SnapState_";
    private string SaveKey => SnapSaveKeyPrefix + SceneManager.GetActiveScene().name + "_" + gameObject.name;

    [Header("스프라이트 앞·뒤 (목마 등 겹침)")]
    [Tooltip("같은 Sorting Layer끼리 월드 Y가 작을수록(화면 아래·가까운 쪽) 더 앞에 그립니다.")]
    [SerializeField] private bool syncSpriteDepthByWorldY = true;

    [Tooltip("Y가 작을수록 앞이 아니라 뒤로 취급하려면 켭니다.")]
    [SerializeField] private bool invertWorldYDepth = false;

    private const int DepthOrderStep = 5;

#if UNITY_EDITOR
    [Header("Editor only — 빌드에는 포함되지 않음")]
    [Tooltip("플레이 시작 1회, 씬에 있는 모든 DraggableSnap2D의 SnapState_* 키만 삭제합니다.")]
    [SerializeField] private bool editorClearSnapPlayerPrefsOnPlay;
    [Tooltip("플레이 시작 1회 PlayerPrefs.DeleteAll(). 설정·Fungus 세이브까지 전부 지워집니다.")]
    [SerializeField] private bool editorDangerDeleteAllPlayerPrefsOnPlay;

    private static bool sEditorPrefsHandledOncePerPlay;

    /// <summary>
    /// 에디터 전용: 플레이 모드 진입 시 게이트를 리셋해, 옵션 토글이 매 플레이마다 한 번 적용되게 합니다.
    /// (Enter Play Mode Without Domain Reload 대비)
    /// </summary>
    public static void EditorResetPlayModePrefGate()
    {
        sEditorPrefsHandledOncePerPlay = false;
    }
#endif

    void Awake()
    {
#if UNITY_EDITOR
        if (!sEditorPrefsHandledOncePerPlay)
        {
            sEditorPrefsHandledOncePerPlay = true;
            DraggableSnap2D[] allDrag = Object.FindObjectsByType<DraggableSnap2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            bool deleteAll = false;
            bool clearSnaps = false;
            foreach (var d in allDrag)
            {
                if (d == null)
                    continue;
                if (d.editorDangerDeleteAllPlayerPrefsOnPlay)
                    deleteAll = true;
                if (d.editorClearSnapPlayerPrefsOnPlay)
                    clearSnaps = true;
            }

            if (deleteAll)
            {
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.LogWarning("[DraggableSnap2D] Editor: PlayerPrefs.DeleteAll() 실행 (인스펙터 위험 옵션).");
            }
            else if (clearSnaps)
            {
                string sceneName = SceneManager.GetActiveScene().name;
                foreach (var d in allDrag)
                {
                    if (d != null)
                        PlayerPrefs.DeleteKey(SnapSaveKeyPrefix + sceneName + "_" + d.gameObject.name);
                }
                PlayerPrefs.Save();
                Debug.Log("[DraggableSnap2D] Editor: SnapState_* 키만 삭제했습니다.");
            }
        }
#endif
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        sr = GetComponent<SpriteRenderer>();

        if (PlayerPrefs.GetInt(SaveKey, 0) == 1)
        {
            var target = FindSnapTargetForKind();
            if (target != null)
            {
                transform.position = target.transform.position;
                isSnapped = true;
                target.occupied = true;

                if (sr != null)
                    sr.color = new Color(1f, 1f, 1f, 0.9f);

                if (lockAfterSnap)
                {
                    var col = GetComponent<Collider2D>();
                    if (col != null)
                        col.enabled = false;
                }

                Flowchart snapFc = FlowchartLocator.Resolve(targetFlowchart);
                if (snapFc != null && !string.IsNullOrEmpty(fungusVarName))
                    snapFc.SetBooleanVariable(fungusVarName, true);
            }
        }
    }

    /// <summary>
    /// 같은 Sorting Layer를 쓰는 <see cref="DraggableSnap2D"/>끼리 묶어,
    /// 월드 Y에 따라 sortingOrder를 재배치합니다. (낮은 Y = 앞, 기본값)
    /// </summary>
    public static void RefreshSpriteDepthByWorldY(DraggableSnap2D[] all = null)
    {
        if (all == null)
            all = Object.FindObjectsByType<DraggableSnap2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (all == null || all.Length == 0)
            return;

        var groups = new Dictionary<int, List<DraggableSnap2D>>();
        foreach (var d in all)
        {
            if (d == null || !d.syncSpriteDepthByWorldY || !d.isActiveAndEnabled)
                continue;
            var r = d.GetComponent<SpriteRenderer>();
            if (r == null)
                continue;
            int layerId = r.sortingLayerID;
            if (!groups.TryGetValue(layerId, out var list))
            {
                list = new List<DraggableSnap2D>();
                groups[layerId] = list;
            }
            list.Add(d);
        }

        foreach (var kv in groups)
        {
            var list = kv.Value;
            if (list == null || list.Count <= 1)
                continue;

            bool groupInvert = list[0].invertWorldYDepth;
            list.Sort((a, b) => CompareDepthPeers(a, b, groupInvert));

            int minOrder = int.MaxValue;
            foreach (var d in list)
            {
                var r = d.GetComponent<SpriteRenderer>();
                if (r != null && r.sortingOrder < minOrder)
                    minOrder = r.sortingOrder;
            }

            if (minOrder == int.MaxValue)
                minOrder = 0;

            int n = list.Count;
            for (int i = 0; i < n; i++)
            {
                var r = list[i].GetComponent<SpriteRenderer>();
                if (r == null)
                    continue;
                r.sortingOrder = minOrder + (n - 1 - i) * DepthOrderStep;
            }
        }
    }

    private static int CompareDepthPeers(DraggableSnap2D a, DraggableSnap2D b, bool invertY)
    {
        float fa = invertY ? -a.transform.position.y : a.transform.position.y;
        float fb = invertY ? -b.transform.position.y : b.transform.position.y;
        int c = fa.CompareTo(fb);
        if (c != 0)
            return c;
        return string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
    }

    public void OnSnappedTo(SnapTarget target)
    {
        isSnapped = true;
        target.occupied = true;
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(target.transform.position);

        PlayerPrefs.SetInt(SaveKey, 1);
        PlayerPrefs.Save();

        Flowchart snapFc = FlowchartLocator.Resolve(targetFlowchart);
        if (snapFc != null && !string.IsNullOrEmpty(fungusVarName))
            snapFc.SetBooleanVariable(fungusVarName, true);

        if (sr != null)
            sr.color = new Color(1f, 1f, 1f, 0.9f);

        if (lockAfterSnap)
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;
        }
    }

    public void ResetToOrigin()
    {
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(originalPos);
    }

    private SnapTarget FindSnapTargetForKind()
    {
        var all = Object.FindObjectsByType<SnapTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t.acceptKind == kind)
                return t;
        }

        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isSnapped ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
