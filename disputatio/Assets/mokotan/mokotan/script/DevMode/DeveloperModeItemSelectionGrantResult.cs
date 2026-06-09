/// <summary>개발자 모드에서 선택한 단일 아이템 지급 결과.</summary>
public sealed class DeveloperModeItemSelectionGrantResult
{
    public bool WasBlockedByDevMode;
    public bool Succeeded;
    public int RequestedQuantity = 1;
    public int GrantedQuantity;
    public int SkippedDuplicateQuantity;
    public int FailedQuantity;
    public string ItemName;
    public int ItemId;
    public string FailureReason;

    public override string ToString()
    {
        if (WasBlockedByDevMode)
            return "개발자 모드가 꺼져 있어 지급하지 않았습니다.";

        if (!Succeeded)
            return string.IsNullOrEmpty(FailureReason)
                ? $"지급 실패: {ItemName} (id={ItemId})"
                : $"지급 실패: {ItemName} (id={ItemId}) — {FailureReason}";

        if (RequestedQuantity > 1 && GrantedQuantity < RequestedQuantity)
        {
            return $"지급 {GrantedQuantity}/{RequestedQuantity}개: {ItemName} (id={ItemId}) — 인벤토리는 스택 불가, 동일 아이템은 1개만 보유 가능";
        }

        return $"지급 완료: {ItemName} x{GrantedQuantity} (id={ItemId})";
    }
}
