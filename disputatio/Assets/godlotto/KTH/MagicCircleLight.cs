using UnityEngine;
using UnityEngine.Rendering.Universal; // 2D URP 조명을 제어하기 위한 네임스페이스
using Fungus; // Fungus Flowchart를 제어하기 위한 네임스페이스

public class MagicCircleLight : MonoBehaviour
{
    [Header("컴포넌트 연결")]
    public Light2D magicLight;
    public Flowchart flowchart;

    [Header("빛 깜빡임 설정")]
    public float minIntensity = 0.5f; // 빛의 최소 밝기
    public float maxIntensity = 1.5f; // 빛의 최대 밝기
    public float flickerSpeed = 2.0f; // 깜빡이는 속도

    // 상태를 확인하기 위한 변수명 지정
    private string targetVariableName = "allSealsComplete";

    private void Update()
    {
        // 1. Flowchart에 연결되어 있고, 해당 변수가 true인지 확인합니다.
        if (flowchart != null && flowchart.GetBooleanVariable(targetVariableName))
        {
            // 봉인이 모두 완료되면 빛의 세기를 0으로 만들고 깜빡임 로직을 종료합니다.
            magicLight.intensity = 0f;
            return; 
        }

        // 2. 깜빡임(Pulse) 효과 계산
        // Mathf.Sin은 -1에서 1 사이를 왕복하므로, 이를 0에서 1 사이의 값으로 변환합니다.
        float pingPong = (Mathf.Sin(Time.time * flickerSpeed) + 1f) / 2f;

        // 3. 최소 밝기와 최대 밝기 사이를 부드럽게 오가도록 설정합니다.
        magicLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, pingPong);
    }
}