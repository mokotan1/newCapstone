using UnityEngine;
using Godlotto.ModalInput;

namespace Godlotto.Interaction
{
    /// <summary>
    /// BedRoom 씬 전용 상호작용 조율. Phase R1: 클릭·패널 닫기 → Fungus Say/Menu.
    /// </summary>
    public class BedRoomInteractionController : RoomInteractionController
    {
        const string PanelBackspaceId = "panel_backspace";
        const string HavePrisonKeyVariable = "HavePrisonKey";
        const string HaveHolyGrailVariable = "HaveHolyGrail";

        [SerializeField] GameObject safePanel;
        [SerializeField] GameObject safeItemEffect;

        protected override string LogPrefix => "[BedRoom]";

        protected override void Awake()
        {
            base.Awake();
            EnsureSafePanelModalScope();
        }

        void EnsureSafePanelModalScope()
        {
            if (safePanel == null || safePanel.GetComponent<ModalInputScope>() != null)
                return;

            safePanel.AddComponent<ModalInputScope>();
        }

        protected override void HandlePanelClosed(string panelCloseId, GameObject closedPanel)
        {
            if (!string.Equals(panelCloseId, PanelBackspaceId, System.StringComparison.Ordinal))
                return;

            if (safePanel == null || closedPanel != safePanel)
                return;

            TryHideSafeItemEffectWhenClosingSafe();
        }

        void TryHideSafeItemEffectWhenClosingSafe()
        {
            if (safeItemEffect == null || Flowchart == null)
                return;

            if (!Flowchart.GetBooleanVariable(HavePrisonKeyVariable)
                || !Flowchart.GetBooleanVariable(HaveHolyGrailVariable))
                return;

            safeItemEffect.SetActive(false);
        }
    }
}
