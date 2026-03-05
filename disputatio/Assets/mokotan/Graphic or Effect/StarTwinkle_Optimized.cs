using UnityEngine;

public class StarTwinkle_Optimized : MonoBehaviour
{
    // ▼▼▼ "frequency"를 "minFrequency"와 "maxFrequency"로 변경 ▼▼▼
    [Tooltip("최소 깜빡임 속도 (주파수)")]
    public float minFrequency = 0.5f;
    [Tooltip("최대 깜빡임 속도 (주파수)")]
    public float maxFrequency = 2.0f;
    // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

    [Tooltip("최소 밝기")]
    public float minBrightness = 0.5f;
    [Tooltip("최대 밝기")]
    public float maxBrightness = 1.5f;

    private Renderer starRenderer;
    private MaterialPropertyBlock propBlock;
    private float timeOffset;
    private int glowIntensityID;

    // ▼▼▼ 이 별의 고유한 깜빡임 속도를 저장할 변수 ▼▼▼
    private float thisStarFrequency;

    void Start()
    {
        starRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        
        // 셰이더 프로퍼티 ID 캐싱
        glowIntensityID = Shader.PropertyToID("_GlowIntensity"); // (셰이더에 맞게 이름 확인)

        // 1. 이 별만의 고유한 "시간 차"를 뽑음
        timeOffset = Random.Range(0f, 6.28f);

        // 2. ▼▼▼ 이 별만의 고유한 "주파수(속도)"를 뽑음 ▼▼▼
        thisStarFrequency = Random.Range(minFrequency, maxFrequency);
    }

    void Update()
    {
        // 3. ▼▼▼ 계산 시, 이 별의 고유 주파수(thisStarFrequency)를 사용 ▼▼▼
        float sinWave = Mathf.Sin((Time.time * thisStarFrequency * 2 * Mathf.PI) + timeOffset);
        float normalizedSin = (sinWave + 1) / 2.0f;
        float currentBrightness = Mathf.Lerp(minBrightness, maxBrightness, normalizedSin);

        // 4. 머티리얼 속성 덮어쓰기
        starRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(glowIntensityID, currentBrightness);
        starRenderer.SetPropertyBlock(propBlock);
    }
}