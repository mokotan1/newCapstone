using System.Collections.Generic;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 사유(reason) 기반 전역 입력 차단 게이트.
    /// 월드 클릭·UI 상호작용·씬 전환 등이 동일한 차단 계층을 공유할 수 있도록 합니다.
    /// 기존 <see cref="InteractionLock"/>과 독립이며, 이 단계에서는 기존 코드를 대체하지 않습니다.
    /// </summary>
    public static class InteractionInputGate
    {
        static readonly HashSet<string> ActiveBlockReasons = new HashSet<string>();

        /// <summary>에디터/개발 빌드에서 Block/Unblock/ForceClear 시 GameLog 출력.</summary>
        public static bool EnableDebugLogging { get; set; }

        public static bool IsBlocked => ActiveBlockReasons.Count > 0;

        public static int ActiveBlockCount => ActiveBlockReasons.Count;

        public static void Block(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return;

            if (ActiveBlockReasons.Add(reason) && EnableDebugLogging)
                GameLog.Log($"[InteractionInputGate] Blocked: {reason} (count={ActiveBlockReasons.Count})");
        }

        public static void Unblock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return;

            if (ActiveBlockReasons.Remove(reason) && EnableDebugLogging)
                GameLog.Log($"[InteractionInputGate] Unblocked: {reason} (count={ActiveBlockReasons.Count})");
        }

        public static void ForceClear()
        {
            if (ActiveBlockReasons.Count == 0)
                return;

            ActiveBlockReasons.Clear();

            if (EnableDebugLogging)
                GameLog.Log("[InteractionInputGate] ForceClear");
        }

        internal static void ResetForTests()
        {
            EnableDebugLogging = false;
            ActiveBlockReasons.Clear();
        }
    }
}
