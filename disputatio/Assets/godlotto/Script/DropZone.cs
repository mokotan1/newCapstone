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

            // 1. 버튼들을 화면에 다시 표시합니다 (SetActive 사용).
            if (rotateRightButtonObject != null)
            {
                rotateRightButtonObject.SetActive(true);
            }
            if (rotateLeftButtonObject != null)
            {
                rotateLeftButtonObject.SetActive(true);
            }

            // 2. 각도 텍스트를 화면에 표시하도록 요청합니다.
            FilterCardRotator rotator = draggable.GetComponent<FilterCardRotator>();
            if (rotator != null)
            {
                rotator.ShowAngleText();
            }
        }
    }
}