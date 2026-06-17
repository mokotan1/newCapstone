using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Godlotto.ModalInput
{
    /// <summary>
    /// HUD/월드 레벨 <see cref="Selectable"/> 에 <see cref="ModalGuardedButton"/> 을 자동 부착해
    /// 모달이 열려 있는 동안 패널 밖 버튼 클릭이 막히도록 "공통 보장"하는 설치기.
    /// 영속 싱글톤으로 부트스트랩되어 씬 로드마다 설치 패스를 수행하므로 수동 배치가 필요 없습니다.
    /// 모달 패널 내부(<see cref="ModalInputScope"/> 하위) 버튼은 게이트가 자동 허용하므로 부착 대상에서 제외합니다.
    /// </summary>
    public sealed class ModalHudGuardInstaller : MonoBehaviour
    {
        private static ModalHudGuardInstaller instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
                return;

            var go = new GameObject(nameof(ModalHudGuardInstaller));
            instance = go.AddComponent<ModalHudGuardInstaller>();
            DontDestroyOnLoad(go);
            instance.InstallForActiveScene();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            InstallForActiveScene();
        }

        /// <summary>활성 씬의 모든 HUD 레벨 Selectable 에 가드를 보장합니다.</summary>
        public void InstallForActiveScene()
        {
            Selectable[] selectables = FindObjectsByType<Selectable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < selectables.Length; i++)
                EnsureGuard(selectables[i]);
        }

        /// <summary>해당 Selectable 이 자동 가드 부착 대상인지 판정합니다(순수 함수).</summary>
        public static bool ShouldGuard(Selectable selectable)
        {
            if (selectable == null)
                return false;

            // 이미 가드가 있으면 중복 부착하지 않습니다.
            if (selectable.GetComponent<ModalGuardedButton>() != null)
                return false;

            // 모달 패널 내부 버튼은 게이트(IsAllowed)가 자동 허용하므로 가드가 불필요합니다.
            if (selectable.GetComponentInParent<ModalInputScope>() != null)
                return false;

            return true;
        }

        /// <summary>가드가 필요하면 부착하고, 이미 있으면 기존 인스턴스를 반환합니다(멱등).</summary>
        public static ModalGuardedButton EnsureGuard(Selectable selectable)
        {
            if (selectable == null)
                return null;

            ModalGuardedButton existing = selectable.GetComponent<ModalGuardedButton>();
            if (existing != null)
                return existing;

            if (!ShouldGuard(selectable))
                return null;

            return selectable.gameObject.AddComponent<ModalGuardedButton>();
        }
    }
}
