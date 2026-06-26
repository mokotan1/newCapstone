namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen Parret(체셔) 월드 클릭: ComeParret 이후 활성화된 뒤 한 번만 Fungus parret 블록을 실행합니다.
    /// isClicked 리셋(DeferredClickCleanup)과 무관하게 C#에서 소비합니다.
    /// </summary>
    public static class KitchenParretInteractionGate
    {
        public const string ParretInteractionId = "parret";
        public const string ParretBlockName = "parret";

        public static bool ShouldExecuteFungusBlock(string interactionId, KitchenPuzzleState state)
        {
            if (interactionId != ParretInteractionId)
                return true;

            if (state == null)
                return false;

            state.RefreshComeParretFromFungus();
            return state.ComeParret && !state.ParretClicked;
        }
    }
}
