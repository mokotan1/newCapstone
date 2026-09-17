namespace Godlotto.Interaction
{
    /// <summary>
    /// 지하 복도: 문 클릭은 Sequence(load_scene)로 처리. Flowchart Start(페이드 인)만 유지합니다.
    /// </summary>
    public sealed class BasementHallwayInteractionController : RoomInteractionController
    {
        protected override string LogPrefix => "[BasementHallway]";

        protected override bool ShouldUseSceneInteractionGate(string interactionId, string blockName) => false;

        protected override bool RequestSceneTransition(string sceneName)
        {
            if (SceneLoadHandlerForTests != null)
                return base.RequestSceneTransition(sceneName);

            GameplayScreenFade.FadeOutThenLoadScene(
                sceneName,
                GameplayScreenFade.BasementDoorFadeTargetAlpha,
                GameplayScreenFade.BasementTransitionDurationSeconds);
            return true;
        }
    }
}
