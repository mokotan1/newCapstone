using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// ChildRoom 씬 전용 상호작용 조율.
    /// Phase R5-A: 월드/UI 클릭 진입점 → Fungus Say/Menu.
    /// </summary>
    public class ChildRoomPuzzleController : RoomInteractionController
    {
        protected override string LogPrefix => "[ChildRoom]";
    }
}
