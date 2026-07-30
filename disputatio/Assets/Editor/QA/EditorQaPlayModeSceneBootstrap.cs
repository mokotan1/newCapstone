#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Godlotto.QA.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Godlotto.QA.EditorCli
{
    /// <summary>
    /// Editor Play Mode + scene bootstrap for <c>qa_run</c>. Temporarily enables
    /// <see cref="EnterPlayModeOptions.DisableDomainReload"/> so the in-flight CLI Task
    /// survives Enter Play Mode, opens/loads <c>scenario.scene</c>, and exits Play Mode in
    /// <see cref="RestoreIfOwned"/> when this instance entered it.
    /// </summary>
    public sealed class EditorQaPlayModeSceneBootstrap : IQaPlayModeSceneBootstrap
    {
        private bool ownsPlayMode;
        private bool mutatedEnterPlayModeOptions;
        private bool previousEnterPlayModeOptionsEnabled;
        private EnterPlayModeOptions previousEnterPlayModeOptions;

        public async Task<QaPlayModeBootstrapResult> EnsureReadyAsync(
            string sceneName,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return QaPlayModeBootstrapResult.Blocked(
                    "BLOCKED: scenario.scene is blank; cannot bootstrap Play Mode.");
            }

            if (!TryResolveScenePath(sceneName.Trim(), out string scenePath))
            {
                return QaPlayModeBootstrapResult.Blocked(
                    "BLOCKED: scene '" + sceneName + "' was not found in EditorBuildSettings or Assets.");
            }

            TimeSpan effectiveTimeout = timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(60);

            try
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    ApplyDisableDomainReload();
                    ownsPlayMode = true;
                    EditorApplication.isPlaying = true;

                    bool entered = await WaitUntilAsync(
                        () => IsPlayModeReady(
                            EditorApplication.isPlaying,
                            EditorApplication.isPlayingOrWillChangePlaymode),
                        effectiveTimeout,
                        cancellationToken).ConfigureAwait(true);
                    if (!entered)
                    {
                        return QaPlayModeBootstrapResult.Blocked(
                            "BLOCKED: timed out entering Play Mode for scene '" + sceneName + "'.",
                            enteredPlayMode: ownsPlayMode);
                    }
                }

                if (!string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal))
                {
                    var parameters = new LoadSceneParameters(LoadSceneMode.Single);
                    EditorSceneManager.LoadSceneInPlayMode(scenePath, parameters);
                    bool loaded = await WaitUntilAsync(
                        () => SceneManager.GetActiveScene().isLoaded
                            && string.Equals(
                                SceneManager.GetActiveScene().name,
                                sceneName,
                                StringComparison.Ordinal),
                        effectiveTimeout,
                        cancellationToken).ConfigureAwait(true);
                    if (!loaded)
                    {
                        return QaPlayModeBootstrapResult.Blocked(
                            "BLOCKED: timed out loading scene '" + sceneName + "' into Play Mode.",
                            enteredPlayMode: ownsPlayMode);
                    }
                }

                // Allow one Editor update tick for Awake/Start on newly loaded scene objects.
                await WaitUntilAsync(() => true, TimeSpan.FromMilliseconds(1), cancellationToken)
                    .ConfigureAwait(true);

                return QaPlayModeBootstrapResult.Success(
                    "Play Mode ready with scene '" + sceneName + "'.",
                    enteredPlayMode: ownsPlayMode);
            }
            catch (OperationCanceledException)
            {
                return QaPlayModeBootstrapResult.Blocked(
                    "BLOCKED: Play Mode scene bootstrap cancelled.",
                    enteredPlayMode: ownsPlayMode);
            }
            catch (Exception ex)
            {
                return QaPlayModeBootstrapResult.Blocked(
                    "BLOCKED: Play Mode scene bootstrap threw " + ex.GetType().Name + ": " + ex.Message,
                    enteredPlayMode: ownsPlayMode);
            }
        }

        public void RestoreIfOwned()
        {
            try
            {
                if (ownsPlayMode && EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                }
            }
            finally
            {
                ownsPlayMode = false;
                RestoreEnterPlayModeOptions();
            }
        }

        internal static bool IsPlayModeReady(
            bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            // Unity keeps isPlayingOrWillChangePlaymode true for the duration of Play Mode.
            // Waiting for it to become false makes every successful entry time out.
            return isPlaying;
        }

        private void ApplyDisableDomainReload()
        {
            if (mutatedEnterPlayModeOptions)
            {
                return;
            }

            previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            mutatedEnterPlayModeOptions = true;
        }

        private void RestoreEnterPlayModeOptions()
        {
            if (!mutatedEnterPlayModeOptions)
            {
                return;
            }

            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            mutatedEnterPlayModeOptions = false;
        }

        private static bool TryResolveScenePath(string sceneName, out string scenePath)
        {
            scenePath = null;
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < buildScenes.Length; i++)
            {
                EditorBuildSettingsScene scene = buildScenes[i];
                if (scene == null || string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }

                if (string.Equals(Path.GetFileNameWithoutExtension(scene.path), sceneName, StringComparison.Ordinal))
                {
                    scenePath = scene.path;
                    return true;
                }
            }

            string[] guids = AssetDatabase.FindAssets(sceneName + " t:Scene");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), sceneName, StringComparison.Ordinal))
                {
                    scenePath = path;
                    return true;
                }
            }

            return false;
        }

        private static Task<bool> WaitUntilAsync(
            Func<bool> predicate,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            double deadline = EditorApplication.timeSinceStartup + Math.Max(0.001, timeout.TotalSeconds);
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        EditorApplication.update -= tick;
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    if (predicate != null && predicate())
                    {
                        EditorApplication.update -= tick;
                        tcs.TrySetResult(true);
                        return;
                    }

                    if (EditorApplication.timeSinceStartup >= deadline)
                    {
                        EditorApplication.update -= tick;
                        tcs.TrySetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    EditorApplication.update -= tick;
                    tcs.TrySetException(ex);
                }
            };

            EditorApplication.update += tick;
            return tcs.Task;
        }
    }
}
#endif
