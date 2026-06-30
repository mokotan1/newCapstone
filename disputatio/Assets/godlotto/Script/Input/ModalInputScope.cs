using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.ModalInput
{
    /// <summary>
    /// 모달 패널 루트에 붙이면, 패널이 활성화되어 있는 동안 <see cref="ModalInputGate"/> 에
    /// 잠금을 등록해 뒤쪽 월드 클릭과 HUD 버튼 클릭을 막습니다.
    /// 방마다 콜라이더를 끄는 땜질 대신, 패널마다 이 컴포넌트 하나만 붙이면 됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModalInputScope : MonoBehaviour
    {
        [Tooltip("뒤쪽 월드 2D 오브젝트 클릭을 막습니다.")]
        [SerializeField] private bool blocksWorldInput = true;

        [Tooltip("이동/지도/뒤로가기 같은 HUD 버튼 클릭을 막습니다.")]
        [SerializeField] private bool blocksHudInput = true;

        [Tooltip("패널 뒤에 투명 raycast 차단 Image를 생성해 UI 클릭도 소비합니다. 모달의 기본 의도(뒤 UI 차단)에 맞춰 기본 켜짐입니다.")]
        [SerializeField] private bool createRaycastBlocker = true;

        [Tooltip("입력을 허용할 루트. 비워두면 이 게임오브젝트가 사용됩니다.")]
        [SerializeField] private GameObject allowedRootOverride;

        private Image raycastBlocker;

        /// <summary>입력이 허용되는 루트(모달 내부 버튼 등).</summary>
        public GameObject AllowedRoot => allowedRootOverride != null ? allowedRootOverride : gameObject;

        private void OnEnable()
        {
            ModalInputGate.Begin(this, AllowedRoot, blocksHudInput, blocksWorldInput);
            EnsureBlocker();
        }

        private void OnDisable()
        {
            ModalInputGate.End(this);
            RemoveBlocker();
        }

        private void OnDestroy()
        {
            ModalInputGate.End(this);
            RemoveBlocker();
        }

        /// <summary>
        /// 투명 raycast 차단막 생성 여부를 런타임에 설정합니다(패널 바인딩 시 명시적 구성용).
        /// 활성 상태라면 즉시 차단막을 만들거나 제거합니다.
        /// </summary>
        public void SetCreateRaycastBlocker(bool enabled)
        {
            createRaycastBlocker = enabled;

            if (!isActiveAndEnabled)
                return;

            if (enabled)
                EnsureBlocker();
            else
                RemoveBlocker();
        }

        private void EnsureBlocker()
        {
            if (!createRaycastBlocker || raycastBlocker != null)
                return;

            // 패널 뒤 sibling 에 투명 차단막을 두는 공통 로직은 ModalRaycastBlocker 가 담당합니다.
            raycastBlocker = ModalRaycastBlocker.Create(transform);
        }

        private void RemoveBlocker()
        {
            ModalRaycastBlocker.Remove(raycastBlocker);
            raycastBlocker = null;
        }

        // ----------------------------------------------------------------
        //  Test hooks (production-safe public API)
        // ----------------------------------------------------------------

        /// <summary>차단 Image 인스턴스(없으면 null). 테스트/검증용.</summary>
        public Image RaycastBlockerForTests => raycastBlocker;

        /// <summary>인스펙터 값 대신 코드로 옵션을 설정합니다(테스트·런타임 구성용).</summary>
        public void ConfigureForTests(bool blocksWorld, bool blocksHud, bool createRaycastBlocker)
        {
            blocksWorldInput = blocksWorld;
            blocksHudInput = blocksHud;
            this.createRaycastBlocker = createRaycastBlocker;
            ModalInputGate.Begin(this, AllowedRoot, blocksHudInput, blocksWorldInput);
        }

        /// <summary>차단 Image를 현재 설정에 맞게 다시 만듭니다.</summary>
        public void RebuildBlockerForTests()
        {
            RemoveBlocker();
            EnsureBlocker();
        }
    }
}
