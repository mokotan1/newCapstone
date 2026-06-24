using Fungus;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Godlotto.Interaction
{
    /// <summary>
    /// EventSystem 기반 UI 클릭을 RoomInteractionController.OnInteraction으로 전달합니다.
    /// </summary>
    public class RoomUiClickForwarder : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] RoomInteractionController controller;
        [SerializeField] string interactionId;
        [SerializeField] Clickable2D clickable;

        void Awake()
        {
            if (clickable != null)
                clickable.enabled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (controller == null || string.IsNullOrWhiteSpace(interactionId))
                return;

            controller.OnInteraction(interactionId, gameObject);
        }
    }
}
