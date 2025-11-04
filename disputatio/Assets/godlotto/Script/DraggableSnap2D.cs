using UnityEngine;
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

#if UNITY_EDITOR
    private static bool sClearedOnceThisPlay = false;   // ← 에디터 Play 세션 동안 단 1회만 초기화
#endif

    void Awake()
    {
#if UNITY_EDITOR
        if (!sClearedOnceThisPlay)
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            sClearedOnceThisPlay = true;
            Debug.Log("[Editor Play] PlayerPrefs cleared (once per play session).");
        }
#endif
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        sr = GetComponent<SpriteRenderer>();

        // 저장된 스냅 상태 복원
        string key = SnapSaveKeyPrefix + gameObject.name;
        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            var target = FindSnapTargetForKind();
            if (target != null)
            {
                transform.position = target.transform.position;
                isSnapped = true;
                target.occupied = true;

                if (lockAfterSnap)
                {
                    var col = GetComponent<Collider2D>();
                    if (col != null) col.enabled = false;
                }

                if (targetFlowchart && !string.IsNullOrEmpty(fungusVarName))
                    targetFlowchart.SetBooleanVariable(fungusVarName, true);
            }
        }
    }

    public void OnSnappedTo(SnapTarget target)
    {
        isSnapped = true;
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(target.transform.position);

        // 저장
        string key = SnapSaveKeyPrefix + gameObject.name;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        if (targetFlowchart && !string.IsNullOrEmpty(fungusVarName))
            targetFlowchart.SetBooleanVariable(fungusVarName, true);

        if (sr != null)
            sr.color = new Color(1f, 1f, 1f, 0.9f);

        if (lockAfterSnap)
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }

    public void ResetToOrigin()
    {
        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(originalPos);
    }

    private SnapTarget FindSnapTargetForKind()
    {
        var all = FindObjectsOfType<SnapTarget>();
        foreach (var t in all)
            if (t.acceptKind == kind) return t;
        return null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isSnapped ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}
