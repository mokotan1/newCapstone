using UnityEngine;
using Fungus;
using System.Collections.Generic;

// 인스펙터에서 리스트로 관리하기 위한 클래스
[System.Serializable]
public class BoolObjectMapping
{
    [Tooltip("Fungus 플로우차트에 있는 Boolean 변수의 이름을 정확히 적어주세요.")]
    public string fungusBoolName;
    
    [Tooltip("활성화/비활성화 할 게임 오브젝트를 연결해주세요.")]
    public GameObject targetObject;
}

public class MagicController : MonoBehaviour
{
    [Tooltip("Fungus 변수가 들어있는 플로우차트를 연결해주세요.")]
    public Flowchart flowchart;

    [Tooltip("제어할 변수 이름과 오브젝트 목록입니다.")]
    public List<BoolObjectMapping> objectMappings = new List<BoolObjectMapping>();

    void Update()
    {
        // 플로우차트가 연결되어 있지 않으면 실행하지 않습니다.
        if (flowchart == null)
        {
            return;
        }

        // 리스트에 있는 모든 항목을 매 프레임 검사합니다.
        for (int i = 0; i < objectMappings.Count; i++)
        {
            var mapping = objectMappings[i];

            // 변수 이름이 비어있거나 대상 오브젝트가 없으면 건너뜁니다.
            if (string.IsNullOrEmpty(mapping.fungusBoolName) || mapping.targetObject == null)
            {
                continue;
            }

            // Fungus 플로우차트에서 해당 이름의 bool 값을 가져옵니다.
            bool isTrue = flowchart.GetBooleanVariable(mapping.fungusBoolName);

            // 현재 오브젝트의 활성화 상태와 Fungus 변수의 상태가 다를 때만 상태를 변경합니다.
            if (mapping.targetObject.activeSelf != isTrue)
            {
                mapping.targetObject.SetActive(isTrue);
            }
        }
    }
}