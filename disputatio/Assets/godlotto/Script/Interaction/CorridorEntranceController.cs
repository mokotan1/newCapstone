namespace Godlotto.Interaction
{
    /// <summary>
    /// 복도·입장 씬의 월드 클릭·확인 메뉴·씬 전환을 C#에서 조율합니다.
    /// Fungus는 Say/Menu 연출만 담당하고 LoadScene·isClicked 정리·복귀는 여기서 처리합니다.
    /// </summary>
    public class CorridorEntranceController : RoomInteractionController
    {
        protected override string LogPrefix => "[CorridorEntrance]";
    }
}
