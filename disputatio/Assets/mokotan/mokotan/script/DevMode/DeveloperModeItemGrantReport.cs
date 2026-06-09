/// <summary>개발자 모드 "모든 아이템 지급" 실행 결과 요약.</summary>
public sealed class DeveloperModeItemGrantReport
{
    public int CandidateCount;
    public int GrantedCount;
    public int SkippedDuplicateCount;
    public int SkippedInvalidCount;
    public int FailedCount;
    public bool WasBlockedByDevMode;

    public bool HasFailures => FailedCount > 0;

    public override string ToString()
    {
        if (WasBlockedByDevMode)
            return "개발자 모드가 꺼져 있어 지급하지 않았습니다.";

        return $"지급 {GrantedCount} / 후보 {CandidateCount} (중복 스킵 {SkippedDuplicateCount}, 무효 스킵 {SkippedInvalidCount}, 실패 {FailedCount})";
    }
}
