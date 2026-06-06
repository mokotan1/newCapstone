using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// MaidRoom 씬 전용 상호작용 조율. Phase R3-A: 클릭 진입점 → Fungus Say/Menu.
    /// Phase R3-C: SelectYes/No·패널 백스페이스 outcome.
    /// </summary>
    public class MaidRoomPuzzleController : RoomInteractionController
    {
        [SerializeField] GameObject diaryPanelToHideOnBookOpen;

        protected override string LogPrefix => "[MaidRoom]";

        protected override void ApplyBlockOutcome(Block block, BlockOutcome outcome)
        {
            base.ApplyBlockOutcome(block, outcome);

            if (outcome.openPanel != null && diaryPanelToHideOnBookOpen != null)
                diaryPanelToHideOnBookOpen.SetActive(false);
        }
    }
}
