using UnityEngine;

[DefaultExecutionOrder(-10000)] // 아주 이르게 실행 (다른 Awake보다 먼저)
public class VariablemanagerSingleton : MonoBehaviour
{
    private static VariablemanagerSingleton instance;

    void Awake()
    {
        // 이미 살아 있는 인스턴스가 있으면 이번에 생긴 건 즉시 제거
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 최초 1개만 살아남아 모든 씬을 통과
        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
