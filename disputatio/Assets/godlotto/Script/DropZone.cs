using UnityEngine;
using UnityEngine.EventSystems;

public class DropZone : MonoBehaviour, IDropHandler
{
    [Header("회전 버튼 오브젝트")]
    public GameObject rotateRightButtonObject;
    public GameObject rotateLeftButtonObject;

    void Start()
    {
        // 시작 시 버튼들을 화면에서 숨깁니다.
        if (rotateRightButtonObject != null)
        {
            rotateRightButtonObject.SetActive(false);
        }
        if (rotateLeftButtonObject != null)
        {
            rotateLeftButtonObject.SetActive(false);
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        Draggable draggable = eventData.pointerDrag.GetComponent<Draggable>();
        if (draggable != null)
        {
            // 위치 및 크기 조절
            draggable.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 0f);
            draggable.transform.localScale = Vector3.one;

            if (rotateRightButtonObject != null)
            {
                rotateRightButtonObject.SetActive(true);
            }
            if (rotateLeftButtonObject != null)
            {
                rotateLeftButtonObject.SetActive(true);
            }
        }
    }
}