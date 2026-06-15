using System;
using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen 씬 전용 상호작용 조율.
    /// Phase R6-A~C: 클릭·드롭 진입점. R6-D: 패널 열기/닫기는 KitchenPanelRegistry.
    /// R6-B Execute UI: 투명 Execute 박스 레이캐스트 보정 + UI/월드 클릭 우선순위 분리.
    /// Phase R6-E: 싱크/병/수도꼭지/드롭 bool — KitchenPuzzleState + Fungus 호출 게이트.
    /// </summary>
    public class KitchenInteractionController : RoomInteractionController
    {
        const string PanelBackspaceId = "panel_backspace";

        [SerializeField] KitchenPanelRegistry panelRegistry;
        [SerializeField] KitchenPuzzleState puzzleState;
        [SerializeField] KitchenSinkWaterDisplay sinkWaterDisplay;
        [SerializeField] bool applyExecuteUiRaycastPolicy = true;

        protected override string LogPrefix => "[Kitchen]";

        protected override void Awake()
        {
            base.Awake();

            if (puzzleState == null)
                puzzleState = GetComponent<KitchenPuzzleState>();
        }

        void Start()
        {
            puzzleState?.HydrateFromFungus();
            SyncSinkWaterDisplayFromPuzzleState();

            if (applyExecuteUiRaycastPolicy)
                KitchenFlowchartExecuteUiRaycastPolicy.Apply(this);
        }

        protected override bool ShouldUseSceneInteractionGate(string interactionId, string blockName)
        {
            return interactionId != KitchenSinkInteractionGate.BottleDragInteractionId
                && interactionId != "food_drag";
        }

        protected override bool ShouldExecuteInteraction(string interactionId, string blockName)
        {
            return KitchenSinkInteractionGate.ShouldExecuteFungusBlock(interactionId, puzzleState);
        }

        protected override void PrepareInteractionExecution(string interactionId, string blockName)
        {
            if (puzzleState == null)
                return;

            if (!IsSinkRouteInteraction(interactionId))
                return;

            puzzleState.MirrorSinkFlagsToFlowchart(Flowchart);

            // FaucetClicked는 Faucet 블록 완료(OnBlockEnd) 후에만 true가 됩니다.
            // 클릭 직후 물 연출을 켜면 faucetClosed(=Faucet 버튼 GO)가 비활성화되어
            // 그 위의 FaucetKeyReleaseController가 FaucetClicked 변화를 감지하지 못합니다.
        }

        protected override void OnInteractionBlockCompleted(string blockName)
        {
            puzzleState?.ApplyBlockCompletion(blockName);
            SyncSinkWaterDisplayAfterBlock(blockName);
        }

        void SyncSinkWaterDisplayAfterBlock(string blockName)
        {
            if (sinkWaterDisplay == null || puzzleState == null)
                return;

            if (blockName == KitchenSinkInteractionGate.FaucetBlockName
                || blockName == KitchenSinkInteractionGate.BottleDraggedBlockName
                || blockName == KitchenSinkInteractionGate.FilledBottleBlockName)
            {
                SyncSinkWaterDisplayFromPuzzleState();
            }
        }

        void SyncSinkWaterDisplayFromPuzzleState()
        {
            if (sinkWaterDisplay == null || puzzleState == null)
                return;

            SyncSinkWaterDisplayFromFaucetRunning(puzzleState.IsSinkWaterRunning);
        }

        void SyncSinkWaterDisplayFromFaucetRunning(bool faucetRunning)
        {
            if (sinkWaterDisplay == null)
                return;

            sinkWaterDisplay.SyncFromFaucetClicked(faucetRunning);
        }

        internal void SetSinkWaterDisplayForTests(KitchenSinkWaterDisplay display) => sinkWaterDisplay = display;

        static bool IsSinkRouteInteraction(string interactionId)
        {
            return interactionId == KitchenSinkInteractionGate.SinkInteractionId
                || interactionId == KitchenSinkInteractionGate.BottleInteractionId
                || interactionId == KitchenSinkInteractionGate.FaucetInteractionId
                || interactionId == KitchenSinkInteractionGate.FilledBottleInteractionId
                || interactionId == KitchenSinkInteractionGate.BottleDragInteractionId;
        }

        protected override bool ShouldProcessWorldClickAt(Vector2 screenPosition)
        {
            return !Clickable2D.IsInteractiveUiUnderPointer(screenPosition);
        }

        protected override bool ShouldProcessWorldClickBinding(WorldClickBinding binding, Vector2 screenPosition)
        {
            if (!IsFripanPanelOpen())
                return true;

            return binding.interactionId != "fripan" && binding.interactionId != "burner";
        }

        bool IsFripanPanelOpen()
        {
            if (panelRegistry == null)
                panelRegistry = GetComponent<KitchenPanelRegistry>();

            return panelRegistry != null && panelRegistry.IsFripanPanelOpen;
        }

        /// <summary>
        /// RoomInteractionController.OnClosePanel이 대상 패널을 비활성화한 뒤 호출됩니다.
        /// CloseAllPanels는 형제/자식 패널(burner, Bottle)과 Parret까지 함께 정리합니다.
        /// isClicked 리셋·DeferredClickCleanup은 기본 close 흐름이 이어서 처리합니다.
        /// </summary>
        protected override void HandlePanelClosed(string panelCloseId, GameObject closedPanel)
        {
            if (!string.Equals(panelCloseId, PanelBackspaceId, StringComparison.Ordinal))
                return;

            panelRegistry?.CloseAllPanels();
        }
    }
}
