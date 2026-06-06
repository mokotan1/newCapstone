using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen 씬 전용 상호작용 조율.
    /// Phase R6-A~C: 클릭·드롭 진입점. R6-D: 패널 열기/닫기는 KitchenPanelRegistry.
    /// </summary>
    public class KitchenInteractionController : RoomInteractionController
    {
        const string PanelBackspaceBlockName = "PannelBackspace";

        [SerializeField] KitchenPanelRegistry panelRegistry;

        protected override string LogPrefix => "[Kitchen]";

        protected override void HandlePanelClosed(string panelCloseId, GameObject closedPanel)
        {
            panelRegistry?.CloseAllPanels();

            if (Flowchart != null)
                FungusDialogueBridge.ExecuteBlockSafely(Flowchart, PanelBackspaceBlockName);
        }
    }
}
