using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mokotan.StandingDialogue
{
    /// <summary>
    /// 인트로 오프닝 씬에서 Talk Standing 스프라이트 위치를 단일 offset으로 고정합니다.
    /// Fungus 커맨드·캐릭터 데이터의 per-character offset은 렌더링 단계에서 무시됩니다.
    /// </summary>
    public static class IntroStandingDialogueOffsetPolicy
    {
        public static readonly Vector2 FixedOffset = new Vector2(0f, -300f);

        /// <summary>인트로 씬 StandingTalk 캐릭터 뒤 검은 반투명 배경 알파 (40~60%).</summary>
        public const float BackdropAlpha = 0.8f;

        private static readonly HashSet<string> IntroScenes = new HashSet<string>(StringComparer.Ordinal)
        {
            SceneNames.IntroScene,
            SceneNames.OpeningOffice,
            SceneNames.OpeningMention,
            SceneNames.OpeningMentionOpen,
        };

        public static bool UsesFixedOffset(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && IntroScenes.Contains(sceneName);

        public static Vector2 ResolveForScene(Vector2 configuredOffset, string sceneName) =>
            UsesFixedOffset(sceneName) ? FixedOffset : configuredOffset;

        public static Vector2 ResolveForActiveScene(Vector2 configuredOffset) =>
            ResolveForScene(configuredOffset, SceneManager.GetActiveScene().name);
    }
}
