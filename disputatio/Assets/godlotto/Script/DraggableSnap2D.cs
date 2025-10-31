using UnityEngine;
using Fungus;

/// <summary>
/// 2D 드래그 가능한 오브젝트 + 스냅 후 상태 저장/복원
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class DraggableSnap2D : MonoBehaviour
{
    [Header("이 오브젝트의 종류 (타겟 매칭용)")]
    public SnapKind kind = SnapKind.Any;

    [Header("스냅 후 드래그 금지")]
    public bool lockAfterSnap = true;

    [Header("Fungus 연동 (선택)")]
    public Flowchart targetFlowchart;
    [Tooltip("붙었을 때 true로 바꿀 변수 이름 (예: seal1, seal2 등)")]
    public string fungusVarName = "";

    [HideInInspector] public bool isSnapped = false;
    [HideInInspector] public Rigidbody2D rb;
    [HideInInspector] public Vector3 originalPos;

    private SpriteRenderer sr;

    // 🔹 PlayerPrefs 키 prefix (개별 오브젝트별 저장용)
    private const string SnapSaveKeyPrefix = "SnapState_";

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation2D.Interpolate;

        sr = GetComponent<SpriteRenderer>();

        // ✅ 저장된 스냅 상태 불러오기
        string key = SnapSaveKeyPrefix + gameObject.name;
        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            // 이미 스냅된 상태면 타겟 위치로 이동 복원
            var target = FindSnapTargetForKind();
            if (target != null)
            {
                transform.position = target.transform.position;
                isSnapped = true;
                target.occupied = true;

                if (lockAfterSnap)
                {
                    Collider2D col = GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                }

                // Fungus 변수도 true로 복원
                if (targetFlowchart != null && !string.IsNullOrEmpty(fungusVarName))
                    targetFlowchart.SetBooleanVariable(fungusVarName, true);
            }
        }
    }

    /// <summary>
    /// DragManager2D가 스냅 성공 시 호출
    /// </summary>
    public void OnSnappedTo(SnapTarget target)
    {
        isSnapped = true;

        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(target.transform.position);

        // ✅ PlayerPrefs 저장
        string key = SnapSaveKeyPrefix + gameObject.name;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        // ✅ Fungus 변수 true 설정
        if (targetFlowchart != null && !string.IsNullOrEmpty(fungusVarName))
        {
            targetFlowchart.SetBooleanVariable(fungusVarName, true);
            Debug.Log($"[Fungus] {fungusVarName} = true (저장됨)");
        }

        // 시각 효과 (선택)
        if (sr != null)
            sr.color = new Color(1f, 1f, 1f, 0.9f);

        // 드래그 잠금
        if (lockAfterSnap)
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }

    /// <summary>
    /// 드래그 실패 시 DragManager2D가 호출
    /// </summary>
    public void ResetToOrigin()
    {
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(originalPos);
    }

    /// <summary>
    /// kind와 일치하는 SnapTarget 찾기
    /// </summary>
    private SnapTarget FindSnapTargetForKind()
    {
        var all = FindObjectsOfType<SnapTarget>();
        foreach (var t in all)
        {
            if (t.acceptKind == kind)
                return t;
        }
        return null;
    }

    // 디버그용 시각화
    void OnDrawGizmosSelected()
    {
        Gizmos.color = isSnapped ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
