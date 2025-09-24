using UnityEngine;
using UnityEngine.Events;
using Fungus; // Fungus를 사용하기 위해 이 줄을 추가해야 합니다.

public class InteractableObject : MonoBehaviour
{
    // 이 오브젝트를 작동시키는 데 필요한 아이템 '설계도'
    public Item requiredItem;

    // 상호작용 성공 시 Inspector에서 지정할 이벤트
    public UnityEvent onSuccess;
    
    // 이 상호작용이 변경할 Fungus 변수의 이름 (Inspector에서 설정)
    [Tooltip("이 상호작용이 성공했을 때 true로 설정할 Fungus Flowchart의 Boolean 변수 이름")]
    public string fungusBooleanVariableName = "hasUsedStudyKey"; // Fungus에 만들 변수 이름과 일치해야 합니다.

    // 이 오브젝트를 마우스로 클릭했을 때 호출됩니다.
    void OnMouseDown()
    {
        // 1. 현재 '손에 든' 아이템이 있는지 확인
        if (InventoryManager.instance.selectedItem != null)
        {
            // 2. '손에 든' 아이템이 이 오브젝트가 필요로 하는 아이템과 같은지 확인
            if (InventoryManager.instance.selectedItem == requiredItem)
            {
                Debug.Log(requiredItem.itemName + " 사용 성공!");

                // 3. 성공 이벤트 실행 (예: 문 열기 애니메이션 재생)
                onSuccess.Invoke();
                
                // 4. Fungus 변수 업데이트
                // 씬에서 활성화된 Flowchart를 찾습니다.
                // 만약 여러 개의 Flowchart가 있다면, 특정 Flowchart를 참조하도록 변경해야 할 수 있습니다.
                Flowchart flowchart = FindFirstObjectByType<Flowchart>();
                if (flowchart != null && !string.IsNullOrEmpty(fungusBooleanVariableName))
                {
                    flowchart.SetBooleanVariable(fungusBooleanVariableName, true);
                    Debug.Log($"Fungus 변수 '{fungusBooleanVariableName}'을(를) True로 설정했습니다.");
                }
                else if (flowchart == null)
                {
                    Debug.LogWarning("씬에서 Fungus Flowchart를 찾을 수 없습니다. Fungus 변수를 업데이트할 수 없습니다.");
                }
                else if (string.IsNullOrEmpty(fungusBooleanVariableName))
                {
                    Debug.LogWarning("Fungus Boolean Variable Name이 InteractableObject에 설정되지 않았습니다. Fungus 변수를 업데이트할 수 없습니다.");
                }

                // 5. 인벤토리에서 사용된 아이템을 제거
                InventoryManager.instance.RemoveItem(requiredItem);

                // 6. '손에 든' 아이템 상태를 해제
                InventoryManager.instance.DeselectItem();
            }
            else
            {
                Debug.Log("잘못된 아이템입니다.");
            }
        }
        else
        {
            // 아무 아이템도 들지 않고 그냥 클릭했을 때의 반응
            Debug.Log(gameObject.name + "을(를) 조사했다.");
        }
    }
}