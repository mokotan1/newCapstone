using System.Collections.Generic;
using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 씬 단위 상호작용 진입 게이트. 실제 클릭/버튼 처리 전에
    /// <see cref="TryInteract"/>로 중복·대사·전환·모달 차단을 일관되게 검사합니다.
    /// </summary>
    public static class SceneInteractionController
    {
        static readonly Dictionary<string, float> LastInteractUnscaledTimeById = new Dictionary<string, float>();

        const float DefaultDuplicateClickCooldownSeconds = 0.35f;

        /// <summary>에디터/개발 빌드에서 차단 사유 로그.</summary>
        public static bool EnableDebugLogging { get; set; }

        /// <summary>동일 interactionId 연타 방지 쿨다운(초, unscaled).</summary>
        public static float DuplicateClickCooldownSeconds { get; set; } = DefaultDuplicateClickCooldownSeconds;

        /// <summary>기존 Fungus <see cref="InteractionLock"/> 상태를 함께 존중합니다.</summary>
        public static bool RespectLegacyInteractionLock { get; set; } = true;

        /// <summary>Say/Menu 대사 중 월드 상호작용 차단.</summary>
        public static bool BlockDuringFungusDialogue { get; set; } = true;

        /// <summary>씬 전환 진행 중 상호작용 차단.</summary>
        public static bool BlockDuringSceneTransition { get; set; } = true;

        /// <summary>
        /// 상호작용을 진행해도 되는지 검사합니다. true면 호출 측이 실제 동작(Fungus/C#)을 수행합니다.
        /// </summary>
        public static bool TryInteract(string interactionId)
        {
            if (string.IsNullOrWhiteSpace(interactionId))
            {
                LogBlocked(interactionId, "empty interaction id");
                return false;
            }

            if (InteractionInputGate.IsBlocked)
            {
                LogBlocked(interactionId, "InteractionInputGate");
                return false;
            }

            if (RespectLegacyInteractionLock && InteractionLock.IsLocked)
            {
                LogBlocked(interactionId, "legacy InteractionLock");
                return false;
            }

            if (BlockDuringSceneTransition && SceneTransitionService.IsTransitionPending)
            {
                LogBlocked(interactionId, "scene transition pending");
                return false;
            }

            if (BlockDuringFungusDialogue && IsFungusConversationBlockingInteraction())
            {
                LogBlocked(interactionId, "fungus dialogue/menu");
                return false;
            }

            float cooldown = DuplicateClickCooldownSeconds;
            if (cooldown > 0f
                && LastInteractUnscaledTimeById.TryGetValue(interactionId, out float lastTime)
                && Time.unscaledTime - lastTime < cooldown)
            {
                LogBlocked(interactionId, "duplicate click cooldown");
                return false;
            }

            LastInteractUnscaledTimeById[interactionId] = Time.unscaledTime;

            if (EnableDebugLogging)
                GameLog.Log($"[SceneInteractionController] Allowed: {interactionId}");

            return true;
        }

        public static void ClearDuplicateClickHistory()
        {
            LastInteractUnscaledTimeById.Clear();
        }

        static bool IsFungusConversationBlockingInteraction()
        {
            MenuDialog menu = MenuDialog.ActiveMenuDialog;
            if (menu != null && menu.gameObject.activeInHierarchy)
                return true;

            SayDialog say = SayDialog.ActiveSayDialog;
            if (say == null || !say.gameObject.activeInHierarchy)
                return false;

            Writer writer = say.GetComponentInChildren<Writer>(true);
            return writer != null && (writer.IsWriting || writer.IsWaitingForInput);
        }

        static void LogBlocked(string interactionId, string reason)
        {
            if (!EnableDebugLogging)
                return;

            string id = string.IsNullOrWhiteSpace(interactionId) ? "(empty)" : interactionId;
            GameLog.Log($"[SceneInteractionController] Blocked '{id}': {reason}");
        }

        internal static void ResetForTests()
        {
            EnableDebugLogging = false;
            DuplicateClickCooldownSeconds = DefaultDuplicateClickCooldownSeconds;
            RespectLegacyInteractionLock = true;
            BlockDuringFungusDialogue = true;
            BlockDuringSceneTransition = true;
            LastInteractUnscaledTimeById.Clear();
        }
    }
}
