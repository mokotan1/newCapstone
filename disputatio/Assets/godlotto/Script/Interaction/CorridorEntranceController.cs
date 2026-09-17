using Fungus;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 복도·입장 씬의 월드 클릭·확인 메뉴·씬 전환을 C#에서 조율합니다.
    /// Fungus는 Say/Menu 연출만 담당하고 LoadScene·isClicked 정리·복귀는 여기서 처리합니다.
    /// </summary>
    public class CorridorEntranceController : RoomInteractionController
    {
        const string HallAnimationEntryBlockName = "IsPlayedAnimation";

        protected override string LogPrefix => "[CorridorEntrance]";

        protected override void ApplyBlockOutcome(Block block, BlockOutcome outcome)
        {
            if (ShouldSkipHallHubEntranceCinematic(block, outcome))
                return;

            base.ApplyBlockOutcome(block, outcome);
        }

        /// <summary>
        /// `Hall_playerble` 허브에서 입장 연출 씬 로드를 막습니다.
        /// </summary>
        /// <remarks>
        /// 본편 입장 연출은 Opening(`Opening_Mention _open`) → `Hall_animate`입니다.
        /// `Hall_playerble`의 GameStarted/`IsPlayedAnimation`은 에디터 Play와 방 재입장까지
        /// `Hall_animate`로 보내므로 허브에서는 이 outcome을 적용하지 않습니다.
        /// </remarks>
        /// <param name="block">종료된 Fungus 블록. null이면 스킵하지 않습니다.</param>
        /// <param name="outcome">블록 outcome. `IsPlayedAnimation` → `Hall_animate`가 아니면 false.</param>
        /// <returns>허브에서 연출 씬 로드를 건너뛰면 true.</returns>
        static bool ShouldSkipHallHubEntranceCinematic(Block block, BlockOutcome outcome)
        {
            return block != null
                && outcome != null
                && string.Equals(block.BlockName, HallAnimationEntryBlockName, System.StringComparison.Ordinal)
                && outcome.loadScene
                && string.Equals(outcome.sceneName, SceneNames.HallAnimate, System.StringComparison.Ordinal)
                && IsHallPlayableContext();
        }

        static bool IsHallPlayableContext()
        {
            return string.Equals(SceneManagerHelper.GetActiveSceneName(), SceneNames.HallPlayable, System.StringComparison.Ordinal)
                || string.Equals(SceneTransitionService.LastLoadedSceneName, SceneNames.HallPlayable, System.StringComparison.Ordinal);
        }
    }
}
