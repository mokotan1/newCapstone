using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// ChildRoom 씬 전용 상호작용 조율.
    /// Phase R5-A: 월드/UI 클릭 진입점 → Fungus Say/Menu.
    /// Phase R5-B: 인장 인벤 드롭(onUnlock) → OnInteraction(seal5/6/7) → Drag_seal* Fungus 블록.
    /// Phase R5-C: SealManager.onAllSealsComplete → OnInteraction(all_seals_complete) → allSealsComplete.
    /// </summary>
    public class ChildRoomPuzzleController : RoomInteractionController
    {
        protected override string LogPrefix => "[ChildRoom]";
    }
}
