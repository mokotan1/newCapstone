namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen 싱크/병/수도꼭지/드롭 라우트의 Fungus 블록 실행 여부를 C# 상태로 판단합니다.
    /// Phase R6-E 1차: 중복·무효 드롭만 차단하고, 분기 대사가 있는 클릭은 Fungus에 위임합니다.
    /// </summary>
    public static class KitchenSinkInteractionGate
    {
        public const string SinkInteractionId = "sink";
        public const string BottleInteractionId = "bottle";
        public const string FaucetInteractionId = "faucet";
        public const string FilledBottleInteractionId = "filled_bottle";
        public const string BottleDragInteractionId = "bottle_drag";

        public const string SinkBlockName = "Sink";
        public const string BottleClickedBlockName = "Bottle_Clicked";
        public const string FaucetBlockName = "Faucet";
        public const string FilledBottleBlockName = "FilledBottle";
        public const string BottleDraggedBlockName = "Bottle_Dragged";

        public static bool ShouldExecuteFungusBlock(string interactionId, KitchenPuzzleState state)
        {
            if (string.IsNullOrWhiteSpace(interactionId) || state == null)
                return true;

            if (interactionId == BottleDragInteractionId)
                return PlayerHasBottle(state) && !state.BottleDragged;

            return true;
        }

        internal static bool PlayerHasBottle(KitchenPuzzleState state)
        {
            if (state != null && state.HasBottle)
                return true;

            if (InventorySlot.draggedItem != null
                && InventorySlot.draggedItem.itemName == "Bottle")
                return true;

            InventoryManager inventory = InventoryManager.Instance
                ?? UnityEngine.Object.FindFirstObjectByType<InventoryManager>();
            if (inventory == null || inventory.Items == null)
                return false;

            for (int i = 0; i < inventory.Items.Count; i++)
            {
                Item item = inventory.Items[i];
                if (item != null && item.itemName == "Bottle")
                    return true;
            }

            return false;
        }
    }
}
