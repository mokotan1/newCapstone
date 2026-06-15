using UnityEngine;
using UnityEngine.Rendering;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Sink_Pannel 수도꼭지/물/파티클 표시 순서와 FaucetClicked 상태를 동기화합니다.
    /// Screen Space Overlay Canvas 위 LineRenderer·ParticleSystem이 배경 Image 뒤로 깔리지 않도록 정렬합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class KitchenSinkWaterDisplay : MonoBehaviour
    {
        [SerializeField] GameObject sinkBackground;
        [SerializeField] Canvas waterOverlayCanvas;
        [SerializeField] GameObject faucetClosed;
        [SerializeField] GameObject faucetOpen;
        [SerializeField] GameObject waterRoot;
        [SerializeField] KitchenPuzzleState puzzleState;

        Canvas rootPanelCanvas;

        void Awake()
        {
            RefreshRootPanelCanvas();
        }

        void OnEnable()
        {
            RunEnableSync();
        }

        void RunEnableSync()
        {
            RefreshRootPanelCanvas();
            SyncFromResolvedPuzzleState();
        }

        void RefreshRootPanelCanvas()
        {
            rootPanelCanvas = GetComponentInParent<Canvas>();
        }

        void SyncFromResolvedPuzzleState()
        {
            KitchenPuzzleState state = ResolvePuzzleState();
            if (state == null)
            {
                EnsureLayoutAndSorting();
                return;
            }

            if (Application.isPlaying)
                state.HydrateFromFungus();

            SyncFromFaucetClicked(state.IsSinkWaterRunning);
        }

        KitchenPuzzleState ResolvePuzzleState()
        {
            if (puzzleState != null)
                return puzzleState;

            return FindFirstObjectByType<KitchenPuzzleState>();
        }

        public void SyncFromFaucetClicked(bool faucetRunning)
        {
            ApplyFaucetRunningVisuals(faucetRunning);
            EnsureLayoutAndSorting();
        }

        public void EnsureLayoutAndSorting()
        {
            if (sinkBackground != null)
                sinkBackground.transform.SetAsFirstSibling();

            int sortingLayerId = ResolveSortingLayerId();

            if (waterOverlayCanvas != null)
            {
                waterOverlayCanvas.overrideSorting = true;
                waterOverlayCanvas.sortingLayerID = sortingLayerId;
                waterOverlayCanvas.sortingOrder = KitchenSinkWaterDisplayPolicy.OverlaySortingOrder;
            }

            ApplyRendererSorting(waterRoot, sortingLayerId);
            MoveOverlayBeforeBackspace();
        }

        void ApplyFaucetRunningVisuals(bool faucetRunning)
        {
            if (faucetClosed != null)
                faucetClosed.SetActive(!faucetRunning);

            if (faucetOpen != null)
                faucetOpen.SetActive(faucetRunning);

            if (waterRoot != null)
                waterRoot.SetActive(faucetRunning);
        }

        void MoveOverlayBeforeBackspace()
        {
            if (waterOverlayCanvas == null)
                return;

            Transform overlay = waterOverlayCanvas.transform;
            Transform backspace = FindBackspaceCornerFold();
            if (backspace == null)
            {
                overlay.SetAsLastSibling();
                return;
            }

            int targetIndex = backspace.GetSiblingIndex();
            if (overlay.GetSiblingIndex() >= targetIndex)
                overlay.SetSiblingIndex(Mathf.Max(0, targetIndex));
        }

        Transform FindBackspaceCornerFold()
        {
            foreach (Transform child in transform)
            {
                if (child.name == "BackspaceCornerFold")
                    return child;
            }

            return null;
        }

        int ResolveSortingLayerId()
        {
            if (rootPanelCanvas != null)
                return rootPanelCanvas.sortingLayerID;

            return SortingLayer.NameToID("Default");
        }

        static void ApplyRendererSorting(GameObject root, int sortingLayerId)
        {
            if (root == null)
                return;

            foreach (LineRenderer lineRenderer in root.GetComponentsInChildren<LineRenderer>(true))
            {
                lineRenderer.sortingLayerID = sortingLayerId;
                lineRenderer.sortingOrder = KitchenSinkWaterDisplayPolicy.OverlaySortingOrder;
            }

            foreach (ParticleSystemRenderer particleRenderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                particleRenderer.sortingLayerID = sortingLayerId;
                particleRenderer.sortingOrder = KitchenSinkWaterDisplayPolicy.OverlaySortingOrder;
            }
        }

        internal void SetReferencesForTests(
            GameObject background,
            Canvas overlayCanvas,
            GameObject closedFaucet,
            GameObject openFaucet,
            GameObject water)
        {
            sinkBackground = background;
            waterOverlayCanvas = overlayCanvas;
            faucetClosed = closedFaucet;
            faucetOpen = openFaucet;
            waterRoot = water;
        }

        internal void SetPuzzleStateForTests(KitchenPuzzleState state)
        {
            puzzleState = state;
        }

        internal void RunEnableSyncForTests()
        {
            RunEnableSync();
        }
    }
}
