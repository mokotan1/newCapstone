using UnityEngine;
using UnityEngine.EventSystems;

public class Draggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 드래그 시작 시 원래 위치를 저장하고, 카드를 살짝 투명하게 만듭니다.
        originalPosition = rectTransform.anchoredPosition;
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false; // 드롭 존을 감지하기 위해 잠시 자신의 레이캐스트를 끕니다.

        // 만약 각도 텍스트가 보이고 있었다면 숨깁니다.
        GetComponent<FilterCardRotator>()?.HideAngleText();
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 마우스를 따라 카드가 움직입니다.
        rectTransform.anchoredPosition += eventData.delta / transform.root.localScale.x;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // 드래그 종료 시 원래 상태로 복구합니다.
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // 만약 드롭 존 위에 놓이지 않았다면, 원래 위치로 되돌립니다.
        if (eventData.pointerEnter == null || eventData.pointerEnter.GetComponent<DropZone>() == null)
        {
            rectTransform.anchoredPosition = originalPosition;
        }
    }
}