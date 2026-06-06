using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Fungus Flowchart 대사/연출 블록 실행 래퍼.
    /// 향후 C# 상호작용 계층에서 Fungus를 호출할 때 공통 진입점으로 사용합니다.
    /// </summary>
    public static class FungusDialogueBridge
    {
        /// <summary>에디터/개발 빌드에서 실행/차단 로그.</summary>
        public static bool EnableDebugLogging { get; set; }

        /// <summary>
        /// 블록 존재·실행 중 여부를 검사한 뒤 안전하게 <see cref="Flowchart.ExecuteBlock"/>을 호출합니다.
        /// </summary>
        public static bool ExecuteBlockSafely(Flowchart flowchart, string blockName)
        {
            if (flowchart == null)
            {
                GameLog.LogWarning("[FungusDialogueBridge] Flowchart가 null입니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(blockName))
            {
                GameLog.LogWarning($"[FungusDialogueBridge] 블록 이름이 비어 있습니다. ({flowchart.name})");
                return false;
            }

            if (!flowchart.HasBlock(blockName))
            {
                GameLog.LogWarning($"[FungusDialogueBridge] 블록 '{blockName}'이(가) 없습니다. ({flowchart.name})");
                return false;
            }

            Block block = flowchart.FindBlock(blockName);
            if (block != null && block.IsExecuting())
            {
                if (EnableDebugLogging)
                    GameLog.Log($"[FungusDialogueBridge] 이미 실행 중인 블록 '{blockName}' 재호출 차단. ({flowchart.name})");
                return false;
            }

            if (EnableDebugLogging)
                GameLog.Log($"[FungusDialogueBridge] ExecuteBlock '{blockName}' on {flowchart.name}");

            flowchart.ExecuteBlock(blockName);
            return true;
        }

        internal static void ResetForTests()
        {
            EnableDebugLogging = false;
        }
    }
}
