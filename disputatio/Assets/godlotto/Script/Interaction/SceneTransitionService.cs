using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godlotto.Interaction
{
    /// <summary>
    /// 씬 전환 공통 서비스. 중복 LoadScene 호출을 방지합니다.
    /// </summary>
    public static class SceneTransitionService
    {
        static bool transitionPending;
        static string pendingSceneName = string.Empty;

        /// <summary>에디터/개발 빌드에서 전환 시도/차단 로그.</summary>
        public static bool EnableDebugLogging { get; set; }

        public static bool IsTransitionPending => transitionPending;

        public static string PendingSceneName => pendingSceneName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResetState();
        }

        /// <summary>
        /// 씬 전환을 요청합니다. 이미 전환 중이면 false를 반환합니다.
        /// </summary>
        public static bool LoadSceneSafely(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                GameLog.LogWarning("[SceneTransitionService] sceneName이 비어 있습니다.");
                return false;
            }

            if (transitionPending)
            {
                if (EnableDebugLogging)
                {
                    GameLog.Log(
                        $"[SceneTransitionService] 전환 중 차단: requested='{sceneName}', pending='{pendingSceneName}'");
                }

                return false;
            }

            transitionPending = true;
            pendingSceneName = sceneName;

            if (EnableDebugLogging)
                GameLog.Log($"[SceneTransitionService] LoadScene '{sceneName}' ({mode})");

            SceneManager.LoadScene(sceneName, mode);
            return true;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetState();
        }

        static void ResetState()
        {
            transitionPending = false;
            pendingSceneName = string.Empty;
        }

        internal static void SetTransitionPendingForTests(bool pending, string sceneName = "")
        {
            transitionPending = pending;
            pendingSceneName = pending ? sceneName ?? string.Empty : string.Empty;
        }

        internal static void ResetForTests()
        {
            EnableDebugLogging = false;
            ResetState();
        }
    }
}
