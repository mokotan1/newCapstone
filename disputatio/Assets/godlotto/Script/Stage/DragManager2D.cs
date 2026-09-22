using System.Collections.Generic;
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
    public float snapSearchRadius = 1.5f;

    [Tooltip("2D 드래그용 월드 평면의 Z (씬에 맞게 조정). ChildRoom 등 픽셀 좌표·z=0 스프라이트 기준.")]
    [SerializeField] private float dragPlaneWorldZ = 0f;

    private Camera cam;
    private DraggableSnap2D current;
    private Vector3 grabOffset;
    private Vector3 originalPos;
    private int layerMaskWithoutSnapTargets;
    private DraggableSnap2D[] cachedDraggables;

    private readonly Dictionary<GameObject, DraggableSnap2D> draggableByGameObject =
        new Dictionary<GameObject, DraggableSnap2D>();
    private readonly Dictionary<GameObject, SpriteRenderer> spriteRendererByGameObject =
        new Dictionary<GameObject, SpriteRenderer>();
    private readonly Dictionary<GameObject, SnapTarget> snapTargetByGameObject =
        new Dictionary<GameObject, SnapTarget>();

    private const float SortingLayerWeight = 10000f;

    void Awake()
    {
        cam = Camera.main;
        int snapTargetMask = LayerMask.GetMask("SnapTarget");
        layerMaskWithoutSnapTargets = ~snapTargetMask;
    }

    void Start()
    {
        cachedDraggables = Object.FindObjectsByType<DraggableSnap2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        DraggableSnap2D.RefreshSpriteDepthByWorldY(cachedDraggables);
        WorldSpriteDepthSorter2D.SortActiveSceneSprites();
    }

    void OnDisable()
    {
        draggableByGameObject.Clear();
        spriteRendererByGameObject.Clear();
        snapTargetByGameObject.Clear();
        cachedDraggables = null;
    }

    void Update()
    {
        if (cam == null)
            cam = Camera.main;
        if (Input.GetMouseButtonDown(0))
            TryBeginDrag();
        if (Input.GetMouseButton(0))
            OnDrag();
        if (Input.GetMouseButtonUp(0))
            EndDrag();
    }

    private Vector3 MouseWorldOnDragPlane()
    {
        if (cam == null)
            return Vector3.zero;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3 planePoint = new Vector3(0f, 0f, dragPlaneWorldZ);
        Plane plane = new Plane(-cam.transform.forward, planePoint);

        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        Vector3 fallback = Input.mousePosition;
        fallback.z = Mathf.Abs(cam.transform.position.z - dragPlaneWorldZ);
        return cam.ScreenToWorldPoint(fallback);
    }

    private void TryBeginDrag()
    {
        if (current != null)
            return;

        Vector3 mw = MouseWorldOnDragPlane();
        Physics2D.SyncTransforms();
        Collider2D[] hits = Physics2D.OverlapPointAll(mw, layerMaskWithoutSnapTargets);
        if (hits == null || hits.Length == 0)
            return;

        Collider2D bestCol = null;
        float bestOrder = float.NegativeInfinity;

        foreach (var h in hits)
        {
            if (!TryGetDraggableSnap(h, out var drag) || (drag.isSnapped && drag.lockAfterSnap))
                continue;

            float order = ComputePickPriority(h);
            if (order > bestOrder)
            {
                bestOrder = order;
                bestCol = h;
            }
        }

        if (bestCol == null)
            return;

        if (!TryGetDraggableSnap(bestCol, out current))
            return;

        originalPos = current.transform.position;
        grabOffset = current.transform.position - mw;
        grabOffset.z = 0f;
    }

    bool TryGetDraggableSnap(Collider2D h, out DraggableSnap2D drag)
    {
        drag = null;
        if (h == null)
            return false;

        GameObject go = h.gameObject;
        if (draggableByGameObject.TryGetValue(go, out drag) && drag != null)
            return true;

        drag = h.GetComponent<DraggableSnap2D>();
        draggableByGameObject[go] = drag;
        return drag != null;
    }

    /// <summary>마우스 아래 여러 콜라이더가 겹칠 때, 화면 앞쪽(정렬 우선) 후보 점수.</summary>
    private float ComputePickPriority(Collider2D h)
    {
        if (TryGetSpriteRenderer(h, out var sr) && sr != null)
        {
            int layerOrder = SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
            return layerOrder * SortingLayerWeight + sr.sortingOrder;
        }

        return -h.transform.position.z;
    }

    bool TryGetSpriteRenderer(Collider2D h, out SpriteRenderer sr)
    {
        sr = null;
        if (h == null)
            return false;

        GameObject go = h.gameObject;
        if (spriteRendererByGameObject.TryGetValue(go, out sr) && sr != null)
            return true;

        sr = h.GetComponent<SpriteRenderer>();
        spriteRendererByGameObject[go] = sr;
        return sr != null;
    }

    bool TryGetSnapTarget(Collider2D h, out SnapTarget target)
    {
        target = null;
        if (h == null)
            return false;

        GameObject go = h.gameObject;
        if (snapTargetByGameObject.TryGetValue(go, out target) && target != null)
            return true;

        target = h.GetComponent<SnapTarget>();
        snapTargetByGameObject[go] = target;
        return target != null;
    }

    private void OnDrag()
    {
        if (current == null)
            return;

        Vector3 mouseWorld = MouseWorldOnDragPlane();
        Vector3 targetPos = mouseWorld + grabOffset;
        targetPos.z = current.transform.position.z;

        current.rb.MovePosition(targetPos);
        DraggableSnap2D.RefreshSpriteDepthByWorldY(cachedDraggables);
        WorldSpriteDepthSorter2D.SortActiveSceneSprites();
    }

    private void EndDrag()
    {
        if (current == null)
            return;

        SnapTarget bestTarget = FindBestSnapTarget(current.transform.position, current);

        if (bestTarget != null)
            current.OnSnappedTo(bestTarget);
        else
        {
            current.rb.linearVelocity = Vector2.zero;
            current.rb.MovePosition(originalPos);
        }

        DraggableSnap2D.RefreshSpriteDepthByWorldY(cachedDraggables);
        WorldSpriteDepthSorter2D.SortActiveSceneSprites();
        current = null;
    }

    private SnapTarget FindBestSnapTarget(Vector2 draggablePosition, DraggableSnap2D draggable)
    {
        int snapMask = LayerMask.GetMask("SnapTarget");
        Physics2D.SyncTransforms();
        var hits = snapMask != 0
            ? Physics2D.OverlapCircleAll(draggablePosition, snapSearchRadius, snapMask)
            : Physics2D.OverlapCircleAll(draggablePosition, snapSearchRadius);

        SnapTarget bestTarget = null;
        float bestDistSq = float.MaxValue;

        foreach (var h in hits)
        {
            if (!TryGetSnapTarget(h, out var t) || !t.CanAccept(draggable))
                continue;

            float d = ((Vector2)t.transform.position - draggablePosition).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                bestTarget = t;
            }
        }

        return bestTarget;
    }

    void OnDrawGizmosSelected()
    {
        if (current == null)
            return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(current.transform.position, snapSearchRadius);
    }
}
