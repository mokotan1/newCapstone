using System.Collections.Generic;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen 씬 패널 Show/Hide. Fungus SetActive 대신 Call Method 또는 Controller에서 호출합니다.
    /// </summary>
    public class KitchenPanelRegistry : MonoBehaviour
    {
        [SerializeField] GameObject burnerPanel;
        [SerializeField] GameObject fripanPanel;
        [SerializeField] GameObject parrotPanel;
        [SerializeField] GameObject sinkPanel;
        [SerializeField] GameObject bottlePanel;

        public void OpenBurnerPanel() => SetActiveSafe(burnerPanel, true);

        public void CloseBurnerPanel() => SetActiveSafe(burnerPanel, false);

        public void OpenFripanPanel() => SetActiveSafe(fripanPanel, true);

        public void CloseFripanPanel() => SetActiveSafe(fripanPanel, false);

        public void OpenParrotPanel() => SetActiveSafe(parrotPanel, true);

        public void CloseParrotPanel() => SetActiveSafe(parrotPanel, false);

        public void OpenSinkPanel()
        {
            SetActiveSafe(sinkPanel, true);
            NormalizePanelCanvas(sinkPanel);
        }

        public void CloseSinkPanel() => SetActiveSafe(sinkPanel, false);

        public void OpenBottlePanel() => SetActiveSafe(bottlePanel, true);

        public void CloseBottlePanel() => SetActiveSafe(bottlePanel, false);

        public void CloseAllPanels()
        {
            CloseBurnerPanel();
            CloseFripanPanel();
            CloseParrotPanel();
            CloseSinkPanel();
            CloseBottlePanel();
        }

        public bool IsFripanPanelOpen => fripanPanel != null && fripanPanel.activeInHierarchy;

        internal IReadOnlyList<GameObject> GetAllPanels() => new[]
        {
            burnerPanel,
            fripanPanel,
            parrotPanel,
            sinkPanel,
            bottlePanel,
        };

        static void SetActiveSafe(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        static void NormalizePanelCanvas(GameObject panel)
        {
            if (panel == null)
                return;

            InventoryManager.NormalizeInventoryCanvasTransform(panel.transform);
        }
    }
}
