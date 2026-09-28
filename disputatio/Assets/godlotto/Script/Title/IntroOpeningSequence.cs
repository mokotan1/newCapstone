using System.Collections;
using Godlotto.Interaction;
using UnityEngine;

/// <summary>
/// Reproduces the IntroScene start block without requiring a Fungus Flowchart.
/// Assign the four chainsaw frames and post-exposure controller through the Inspector.
/// SFX uses the same runtime singleton as the former Fungus commands.
/// </summary>
public sealed class IntroOpeningSequence : MonoBehaviour
{
    [Header("Intro image frames")]
    [SerializeField] private GameObject firstChainsawFrame1;
    [SerializeField] private GameObject firstChainsawFrame2;
    [SerializeField] private GameObject firstChainsawFrame3;
    [SerializeField] private GameObject firstChainsawFrame4;

    [Header("Opening effects")]
    [SerializeField] private PostExposureController postExposureController;

    private bool started;
    private bool cancelled;

    private void Start()
    {
        if (started)
            return;

        started = true;
        StartCoroutine(PlayOpeningSequence());
    }

    private void OnDisable()
    {
        cancelled = true;
    }

    private IEnumerator PlayOpeningSequence()
    {
        // Original start block commands 1–5.
        yield return new WaitForSeconds(2f);
        if (!CanContinue()) yield break;
        postExposureController.SetEyesClosed();
        firstChainsawFrame1.SetActive(false);
        firstChainsawFrame2.SetActive(true);
        PlaySfx(23);

        // Original commands 6–12.
        yield return new WaitForSeconds(1f);
        if (!CanContinue()) yield break;
        postExposureController.SetEyesOpenSmoothly(0.7f);
        yield return new WaitForSeconds(2f);
        if (!CanContinue()) yield break;
        postExposureController.SetEyesClosed();
        firstChainsawFrame2.SetActive(false);
        firstChainsawFrame3.SetActive(true);
        PlaySfx(23);

        // Original commands 13–20.
        yield return new WaitForSeconds(1f);
        if (!CanContinue()) yield break;
        postExposureController.SetEyesOpenSmoothly(0.7f);
        PlaySfx(2);
        yield return new WaitForSeconds(2f);
        if (!CanContinue()) yield break;
        postExposureController.SetEyesClosed();
        firstChainsawFrame3.SetActive(false);
        firstChainsawFrame4.SetActive(true);
        PlaySfx(23);

        // Original commands 21–28.
        yield return new WaitForSeconds(1f);
        if (!CanContinue()) yield break;
        postExposureController.SetEyesOpened();
        postExposureController.SetDramaticEnding();
        yield return new WaitForSeconds(1.5f);
        if (!CanContinue()) yield break;
        yield return new WaitForSeconds(1.5f);
        if (!CanContinue()) yield break;

        GameplayScreenFade.FadeThen(1f, 1f, OnOpeningFadeCompleted);
    }

    private void OnOpeningFadeCompleted()
    {
        if (!CanContinue())
            return;

        if (SfxController.Instance != null)
            SfxController.Instance.StopSFX(2);
        SceneTransitionService.LoadSceneSafely(SceneNames.OpeningOffice);
    }

    private static void PlaySfx(int index)
    {
        if (SfxController.Instance != null)
            SfxController.Instance.PlaySFX(index);
    }

    private bool CanContinue()
    {
        return !cancelled && this != null && isActiveAndEnabled && gameObject.activeInHierarchy;
    }
}
