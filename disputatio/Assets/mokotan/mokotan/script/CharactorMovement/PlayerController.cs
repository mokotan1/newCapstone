using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace UndergroundLab.MiniGame
{
    public class CameraEffectManager : MonoBehaviour
    {
        // 싱글톤 인스턴스
        public static CameraEffectManager Instance { get; private set; }

        [Header("UI References")]
        public Image redOverlay; // 화면 전체를 덮는 붉은색 Image 패널 (Alpha 0)

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void TriggerRedFlash()
        {
            if (redOverlay != null)
            {
                StopAllCoroutines();
                StartCoroutine(RedFlashRoutine());
            }
        }

        private IEnumerator RedFlashRoutine()
        {
            float duration = 0.2f;
            float elapsed = 0f;
            
            // 붉게 변함
            Color tempColor = redOverlay.color;
            tempColor.a = 0.5f; // 최대 투명도 설정
            redOverlay.color = tempColor;

            // 서서히 사라짐
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                tempColor.a = Mathf.Lerp(0.5f, 0f, elapsed / duration);
                redOverlay.color = tempColor;
                yield return null;
            }
        }
    }
}