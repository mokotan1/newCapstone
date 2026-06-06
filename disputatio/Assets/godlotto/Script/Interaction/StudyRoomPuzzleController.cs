using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// StudyRoom 씬 전용 상호작용 조율. Phase R4-A: CardStack/Diary UI 클릭 → Fungus Say/Menu.
    /// </summary>
    public class StudyRoomPuzzleController : RoomInteractionController
    {
        protected override string LogPrefix => "[StudyRoom]";
    }
}
