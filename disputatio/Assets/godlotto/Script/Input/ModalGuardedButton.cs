using UnityEngine;
using UnityEngine.UI;

namespace Godlotto.ModalInput
{
    /// <summary>
    /// 이동/지도/뒤로가기처럼 UI <see cref="Button"/>(또는 <see cref="Selectable"/>)으로 동작하는
    /// HUD 컨트롤에 붙입니다. 모달이 열려 있고 이 버튼이 모달 허용 루트 밖이면
    /// <see cref="ModalInputGate"/> 판정에 따라 일시적으로 interactable 을 꺼서 클릭을 막습니다.
    /// 모달이 닫히면 원래 상태로 복구합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModalGuardedButton : MonoBehaviour
    {
        private Selectable selectable;
        private bool hasCachedInteractable;
        private bool cachedInteractable;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
        }

        private void OnDisable()
        {
            RestoreInteractable();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>현재 게이트 상태에 맞춰 버튼의 interactable 을 갱신합니다.</summary>
        public void Refresh()
        {
            if (selectable == null)
                selectable = GetComponent<Selectable>();

            if (selectable == null)
                return;

            if (ShouldBlock())
            {
                if (!hasCachedInteractable)
                {
                    cachedInteractable = selectable.interactable;
                    hasCachedInteractable = true;
                }

                selectable.interactable = false;
            }
            else
            {
                RestoreInteractable();
            }
        }

        /// <summary>모달 때문에 이 버튼을 막아야 하는지. 순수 판정 함수(테스트 가능).</summary>
        public bool ShouldBlock()
        {
            return ModalInputGate.IsBlockingHudInput && !ModalInputGate.IsAllowed(gameObject);
        }

        private void RestoreInteractable()
        {
            if (!hasCachedInteractable || selectable == null)
            {
                hasCachedInteractable = false;
                return;
            }

            selectable.interactable = cachedInteractable;
            hasCachedInteractable = false;
        }
    }
}
