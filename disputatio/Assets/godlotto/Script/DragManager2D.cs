using UnityEngine;

/// <summary>
/// 전역에서 마우스 입력을 수집해
/// - SnapTarget 레이어를 완전히 무시하고
/// - 마우스 아래 '드래그 가능한 것'만 집어서
/// - 드래그/스냅을 수행하는 매니저.
/// 씬에 1개만 두세요.
/// </summary>
public class DragManager2D : MonoBehaviour
{
    [Header("스냅 탐색 반경(드롭 시 근처 타겟 보정)")]
    public float snapSearchRadius = 0.2f;

    private Camera cam;
    private DraggableSnap2D current;
    private Vector3 grabOffset;
    private Vector3 originalPos;

    // SnapTarget 레이어 마스크 (에디터에서 레이어 이름만 맞춰두면 됨)
    private int layerMaskWithoutSnapTargets;

    void Awake()
    {
        cam = Camera.main;

        // SnapTarget 레이어를 완전히 제외한 마스크 구성
        int snapTargetMask = LayerMask.GetMask("SnapTarget"); // ← 레이어 이름만 "SnapTarget"으로 지정하세요.
        layerMaskWithoutSnapTargets = ~snapTargetMask;
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;
        if (Input.GetMouseButtonDown(0)) TryBeginDrag();
        if (Input.GetMouseButton(0)   ) OnDrag();
        if (Input.GetMouseButtonUp(0) ) EndDrag();
    }

    void TryBeginDrag()
    {
        if (current != null) return; // 이미 드래그 중

        // 마우스 월드 좌표
        Vector3 mw = cam.ScreenToWorldPoint(Input.mousePosition);
        mw.z = 0f;

        // SnapTarget 레이어를 '완전히 제외'하고 포인트 히트
        Collider2D[] hits = Physics2D.OverlapPointAll(mw, layerMaskWithoutSnapTargets);

        if (hits == null || hits.Length == 0) return;

        // '드래그 가능한 것'만 후보로 필터링 + 화면상 가장 앞(정렬 우선) 순서로 선택
        Collider2D bestCol = null;
        float bestOrder = float.NegativeInfinity;

        foreach (var h in hits)
        {
            var drag = h.GetComponent<DraggableSnap2D>();
            if (drag == null || drag.isSnapped && drag.lockAfterSnap) continue;

            float order = -h.transform.position.z; // 기본값: 카메라에 가까울수록 앞

            var sr = h.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                int layerOrder = SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
                order = layerOrder * 10000f + sr.sortingOrder;
            }

            if (order > bestOrder)
            {
                bestOrder = order;
                bestCol = h;
            }
        }

        if (bestCol == null) return;

        current = bestCol.GetComponent<DraggableSnap2D>();
        if (current == null) return;

        originalPos = current.transform.position;

        Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = current.transform.position.z;
        grabOffset = current.transform.position - mouse;
    }

    void OnDrag()
    {
        if (current == null) return;

        Vector3 mouse = cam.ScreenToWorldPoint(Input.mousePosition);
        mouse.z = current.transform.position.z;
        Vector3 targetPos = mouse + grabOffset;

        current.rb.MovePosition(targetPos);
    }

    void EndDrag()
{
    if (current == null) return;

    // 드롭 시점에서 근처의 스냅 타겟 탐색
    var hits = Physics2D.OverlapCircleAll(current.transform.position, snapSearchRadius);
    SnapTarget bestTarget = null;

    foreach (var h in hits)
    {
        var t = h.GetComponent<SnapTarget>();
        if (t != null && t.CanAccept(current))
        {
            bestTarget = t;
            break;
        }
    }

    if (bestTarget != null)
    {
        // 🔴 이전처럼 여기서 위치/변수/잠금 직접 처리하지 말고…
        // ✅ 한 줄로 위임: DraggableSnap2D가 모든 후속 처리(Flowchart true 포함) 수행
        current.OnSnappedTo(bestTarget);
    }
    else
    {
        // 실패 시 원위치 복귀
        current.rb.linearVelocity = Vector2.zero;
        current.rb.MovePosition(originalPos);
    }

    current = null;
}

    void OnDrawGizmosSelected()
    {
        if (current != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(current.transform.position, snapSearchRadius);
        }
    }
}
