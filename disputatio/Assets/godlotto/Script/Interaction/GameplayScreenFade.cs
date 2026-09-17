using System;
using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Fungus CameraManager 페이드를 Interaction·Sequence 전환에 재사용합니다 (FadeScreen과 동일 API).
    /// </summary>
    public static class GameplayScreenFade
    {
        public const float BasementTransitionDurationSeconds = 1f;

        /// <summary>BasementHallway 문 블록과 동일 (FadeScreen targetAlpha).</summary>
        public const float BasementDoorFadeTargetAlpha = 0f;

        public static Action<float, float, Action> FadeThenHandlerForTests;

        public static void FadeThen(float targetAlpha, float durationSeconds, Action onComplete)
        {
            if (FadeThenHandlerForTests != null)
            {
                FadeThenHandlerForTests(targetAlpha, durationSeconds, onComplete);
                return;
            }

            if (!TryGetCameraManager(out CameraManager cameraManager))
            {
                onComplete?.Invoke();
                return;
            }

            EnsureBlackFadeTexture(cameraManager);
            cameraManager.Fade(targetAlpha, durationSeconds, onComplete);
        }

        /// <summary>입장 시 검은 화면에서 FadeScreen targetAlpha까지 (Hallway Start 블록 기본).</summary>
        public static void PlayEnterFade(float targetAlpha, float durationSeconds)
        {
            FadeThen(targetAlpha, durationSeconds, null);
        }

        public static void FadeOutThenLoadScene(string sceneName, float targetAlpha, float durationSeconds)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            FadeThen(
                targetAlpha,
                durationSeconds,
                () => SceneTransitionService.LoadSceneSafely(sceneName));
        }

        static bool TryGetCameraManager(out CameraManager cameraManager)
        {
            cameraManager = null;
            if (FungusManager.Instance == null)
                return false;

            cameraManager = FungusManager.Instance.CameraManager;
            return cameraManager != null;
        }

        static void EnsureBlackFadeTexture(CameraManager cameraManager)
        {
            if (cameraManager.ScreenFadeTexture != null)
                return;

            cameraManager.ScreenFadeTexture = CameraManager.CreateColorTexture(Color.black, 32, 32);
        }
    }
}
