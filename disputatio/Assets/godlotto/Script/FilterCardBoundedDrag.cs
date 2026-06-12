using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// FilterCard 전용 자유 이동 드래그 핸들러.
/// 기존 Draggable과 달리 드롭 실패 시 위치를 원위치로 되돌리지 않고,
/// 지정된 경계(boundsRect) 안에서만 카드가 움직이도록 클램프한다.
/// 회전(FilterCardRotator)과는 독립적으로 동작한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FilterCardBoundedDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("드래그 이동을 가둘 영역. 보통 드롭 시 FilterCardBookDropZone이 코드로 주입한다. " +
             "비어 있으면 부모 RectTransform을 경계로 사용한다.")]
    [SerializeField] private RectTransform boundsRect;

    /// <summary>드래그 종료 및 경계 보정 후 호출.</summary>
    public event Action DragEnded;

    private RectTransform rectTransform;

    // 매 프레임 할당을 피하기 위해 재사용하는 꼭짓점 버퍼.
    private readonly Vector3[] cardCorners = new Vector3[4];
    private readonly Vector3[] boundCorners = new Vector3[4];

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>드롭 시 책 패널 RectTransform을 드래그 경계로 주입한다.</summary>
    public void SetBounds(RectTransform bounds)
    {
        boundsRect = bounds;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 자유 이동만 담당하므로 시작 시점에 별도로 저장할 상태가 없다.
    }

    public void OnDrag(PointerEventData eventData)
    {
        // transform.root(Canvas) 스케일 보정 — 기존 Draggable과 동일한 방식.
        float rootScale = transform.root.localScale.x;
        if (Mathf.Approximately(rootScale, 0f))
            rootScale = 1f;

        rectTransform.anchoredPosition += eventData.delta / rootScale;
        ClampInsideBounds();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 위치를 되돌리지 않는다. 경계만 한 번 더 보정한다.
        ClampInsideBounds();
        DragEnded?.Invoke();
    }

    /// <summary>
    /// 카드의 월드 AABB가 경계 AABB를 벗어나면 다시 안으로 밀어 넣는다.
    /// 월드 꼭짓점을 사용하므로 카드가 회전/스케일된 상태에서도 화면 밖으로 튀지 않는다.
    /// </summary>
    private void ClampInsideBounds()
    {
        RectTransform bounds = boundsRect != null ? boundsRect : rectTransform.parent as RectTransform;
        if (bounds == null)
            return;

        rectTransform.GetWorldCorners(cardCorners);
        bounds.GetWorldCorners(boundCorners);

        float cardMinX = Mathf.Min(cardCorners[0].x, cardCorners[1].x, cardCorners[2].x, cardCorners[3].x);
        float cardMaxX = Mathf.Max(cardCorners[0].x, cardCorners[1].x, cardCorners[2].x, cardCorners[3].x);
        float cardMinY = Mathf.Min(cardCorners[0].y, cardCorners[1].y, cardCorners[2].y, cardCorners[3].y);
        float cardMaxY = Mathf.Max(cardCorners[0].y, cardCorners[1].y, cardCorners[2].y, cardCorners[3].y);

        float boundMinX = Mathf.Min(boundCorners[0].x, boundCorners[1].x, boundCorners[2].x, boundCorners[3].x);
        float boundMaxX = Mathf.Max(boundCorners[0].x, boundCorners[1].x, boundCorners[2].x, boundCorners[3].x);
        float boundMinY = Mathf.Min(boundCorners[0].y, boundCorners[1].y, boundCorners[2].y, boundCorners[3].y);
        float boundMaxY = Mathf.Max(boundCorners[0].y, boundCorners[1].y, boundCorners[2].y, boundCorners[3].y);

        float offsetX = 0f;
        float offsetY = 0f;

        // 카드를 경계 안으로 밀어 넣는다. 한쪽이 넘쳤을 때만 보정한다.
        if (cardMinX < boundMinX)
            offsetX = boundMinX - cardMinX;
        else if (cardMaxX > boundMaxX)
            offsetX = boundMaxX - cardMaxX;

        if (cardMinY < boundMinY)
            offsetY = boundMinY - cardMinY;
        else if (cardMaxY > boundMaxY)
            offsetY = boundMaxY - cardMaxY;

        if (offsetX != 0f || offsetY != 0f)
            rectTransform.position += new Vector3(offsetX, offsetY, 0f);
    }
}
