using UnityEngine;
using UnityEngine.SceneManagement;

public class AutoAudioListenerFixer : MonoBehaviour
{
    // "나 자신"을 기억하는 정적 변수 (모든 인스턴스가 공유함)
    private static AutoAudioListenerFixer instance;

    void Awake()
    {
        // 1. 이미 살아있는 '원본'이 있는지 확인
        if (instance == null)
        {
            // 없다면, 내가 원본이다.
            instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            // 이미 원본이 있다면? 나는 메인 메뉴가 다시 로드되면서 생긴 '가짜(중복)'다.
            // 나를 파괴하고 함수를 종료한다.
            Destroy(this.gameObject);
            return; 
        }
    }

    void OnEnable()
    {
        // (중요) 중복된 객체라면 이벤트를 연결하면 안 됨
        if (instance == this)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    void OnDisable()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // (안전장치) 이 코드가 실행되는 시점에 내가 파괴될 운명이라면 실행 금지
        if (this == null) return; 

        AudioListener[] listeners = FindObjectsOfType<AudioListener>();

        if (listeners.Length > 1)
        {
            foreach (var listener in listeners)
            {
                // 이름이 "Camera"이고 부모가 있는(자식인) 경우만 삭제
                if (listener.gameObject.name == "Camera" && listener.transform.parent != null)
                {
                    Debug.Log($"[AudioFixer] 중복 리스너 자동 제거: {listener.gameObject.name} in {scene.name}");
                    Destroy(listener);
                }
            }
        }
    }
}