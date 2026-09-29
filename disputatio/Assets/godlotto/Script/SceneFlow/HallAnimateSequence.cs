using System;
using System.Collections;
using Godlotto.Interaction;
using UnityEngine;

/// <summary>
/// Hall_animate Parret_Animated Flowchart: fade in, SFX 9, move parrot, fade out, record previous, load Hall_playerble.
/// </summary>
public sealed class HallAnimateSequence : MonoBehaviour
{
    [SerializeField] Transform parrot;
    [SerializeField] Transform destination;
    [SerializeField] float enterFadeDurationSeconds = 1f;
    [SerializeField] float enterFadeTargetAlpha = 0f;
    [SerializeField] float exitFadeDurationSeconds = 0.25f;
    [SerializeField] float exitFadeTargetAlpha = 1f;
    [SerializeField] float moveDurationSeconds = 1f;
    [SerializeField] int sfxIndex = 9;

    bool started;
    bool cancelled;

    internal static Action<int> PlaySfxHandlerForTests;
    internal static Action<string> LoadSceneHandlerForTests;

    void Start()
    {
        if (started)
            return;
        started = true;
        StartCoroutine(PlayRoutine());
    }

    void OnDisable()
    {
        cancelled = true;
    }

    internal IEnumerator PlayRoutineForTests()
    {
        return PlayRoutine();
    }

    /// <summary>EditMode helper: runs the cutscene steps without relying on nested yields.</summary>
    internal void RunSynchronouslyForTests()
    {
        GameplayScreenFade.FadeThen(enterFadeTargetAlpha, enterFadeDurationSeconds, null);
        PlaySfx(sfxIndex);
        if (parrot != null && destination != null)
            parrot.position = destination.position;
        GameplayScreenFade.FadeThen(exitFadeTargetAlpha, exitFadeDurationSeconds, null);
        SceneRouteState.RecordDepartedScene(SceneNames.HallAnimate);
        TrySetFungusPreviousSceneName(SceneNames.HallAnimate);
        LoadHallPlayable();
    }

    IEnumerator PlayRoutine()
    {
        bool enterFadeDone = false;
        GameplayScreenFade.FadeThen(
            enterFadeTargetAlpha,
            enterFadeDurationSeconds,
            () => enterFadeDone = true);
        while (!enterFadeDone)
        {
            if (!CanContinue())
                yield break;
            yield return null;
        }

        if (!CanContinue())
            yield break;

        PlaySfx(sfxIndex);

        if (parrot != null && destination != null)
        {
            if (moveDurationSeconds <= 0f)
            {
                parrot.position = destination.position;
            }
            else
            {
                Vector3 from = parrot.position;
                Vector3 to = destination.position;
                float elapsed = 0f;
                while (elapsed < moveDurationSeconds)
                {
                    if (!CanContinue())
                        yield break;
                    elapsed += Time.deltaTime;
                    parrot.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / moveDurationSeconds));
                    yield return null;
                }

                parrot.position = to;
            }
        }

        if (!CanContinue())
            yield break;

        bool exitFadeDone = false;
        GameplayScreenFade.FadeThen(
            exitFadeTargetAlpha,
            exitFadeDurationSeconds,
            () => exitFadeDone = true);
        while (!exitFadeDone)
        {
            if (!CanContinue())
                yield break;
            yield return null;
        }

        if (!CanContinue())
            yield break;

        SceneRouteState.RecordDepartedScene(SceneNames.HallAnimate);
        TrySetFungusPreviousSceneName(SceneNames.HallAnimate);
        LoadHallPlayable();
    }

    static void PlaySfx(int index)
    {
        if (PlaySfxHandlerForTests != null)
        {
            PlaySfxHandlerForTests(index);
            return;
        }

        if (SfxController.Instance != null)
            SfxController.Instance.PlaySFX(index);
    }

    static void TrySetFungusPreviousSceneName(string sceneName)
    {
        Fungus.Flowchart flowchart = FlowchartLocator.Find();
        if (flowchart == null)
            return;

        flowchart.SetStringVariable("PreviousSceneName", sceneName);
    }

    static void LoadHallPlayable()
    {
        if (LoadSceneHandlerForTests != null)
        {
            LoadSceneHandlerForTests(SceneNames.HallPlayable);
            return;
        }

        SceneTransitionService.LoadSceneSafely(SceneNames.HallPlayable);
    }

    bool CanContinue()
    {
        return !cancelled && this != null && isActiveAndEnabled && gameObject.activeInHierarchy;
    }
}
