/// <summary>
/// 튜토리얼 퀘스트 진행 판정에 쓰는 월드 스냅샷. EditMode 테스트·런타임 리더가 공유한다.
/// </summary>
public readonly struct TutorialQuestWorldFlags
{
    public TutorialQuestWorldFlags(
        string activeSceneName,
        bool electricOn,
        bool getBottle,
        bool faucetClicked,
        bool bottleDragged)
    {
        ActiveSceneName = activeSceneName ?? string.Empty;
        ElectricOn = electricOn;
        GetBottle = getBottle;
        FaucetClicked = faucetClicked;
        BottleDragged = bottleDragged;
    }

    public string ActiveSceneName { get; }

    public bool ElectricOn { get; }

    public bool GetBottle { get; }

    public bool FaucetClicked { get; }

    public bool BottleDragged { get; }
}
