using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>Opens the research room desk panel without a Flowchart click block.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class BasementResearchDeskOpener : MonoBehaviour
    {
        [SerializeField] GameObject panel;

        void OnMouseDown()
        {
            TryOpenPanel(Input.mousePosition);
        }

        public bool TryOpenPanel(Vector2 screenPosition)
        {
            if (panel == null || panel.activeSelf
                || InteractionInputGate.IsBlocked
                || InteractionLock.IsLocked
                || Clickable2D.ShouldBlockWorldClick(gameObject)
                || Clickable2D.IsModalSayDialogOpen()
                || Clickable2D.IsInteractiveUiUnderPointer(screenPosition))
                return false;

            panel.SetActive(true);
            return true;
        }
    }
}
