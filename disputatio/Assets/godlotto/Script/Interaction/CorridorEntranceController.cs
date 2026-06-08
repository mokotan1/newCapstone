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
            if (ShouldSkipRepeatedHallAnimation(block, outcome))
                return;

            base.ApplyBlockOutcome(block, outcome);
        }

        static bool ShouldSkipRepeatedHallAnimation(Block block, BlockOutcome outcome)
        {
            return block != null
                && outcome != null
                && string.Equals(block.BlockName, HallAnimationEntryBlockName, System.StringComparison.Ordinal)
                && outcome.loadScene
                && string.Equals(outcome.sceneName, SceneNames.HallAnimate, System.StringComparison.Ordinal)
                && IsHallPlayableContext()
                && string.Equals(SceneTransitionService.PreviousSceneName, SceneNames.HallAnimate, System.StringComparison.Ordinal);
        }

        static bool IsHallPlayableContext()
        {
            return string.Equals(SceneManagerHelper.GetActiveSceneName(), SceneNames.HallPlayable, System.StringComparison.Ordinal)
                || string.Equals(SceneTransitionService.LastLoadedSceneName, SceneNames.HallPlayable, System.StringComparison.Ordinal);
        }
    }
}
