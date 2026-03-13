using UnityEngine;
using UnityEngine.EventSystems;

// IPointer 인터페이스들을 상속받으면, 투명한 이미지라도 무조건 클릭을 가로챕니다.
public class ClickBlocker : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    // 안에 코드가 없어도 됩니다. 이 함수들이 있다는 것 자체가 클릭을 여기서 소멸시킵니다.
    public void OnPointerClick(PointerEventData eventData) 
    {
        // 클릭 먹어치움
    }

    public void OnPointerDown(PointerEventData eventData) 
    {
        // 마우스 누를 때 먹어치움
    }

    public void OnPointerUp(PointerEventData eventData) 
    {
        // 마우스 뗄 때 먹어치움
    }
}