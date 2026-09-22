using System.Collections;
using UnityEngine;

public sealed class RewardFadePresenter : MonoBehaviour
{
    private Coroutine fadeRoutine;

    public void Play(float seconds)
    {
        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeIn(Mathf.Max(0.01f, seconds)));
    }

    private IEnumerator FadeIn(float seconds)
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / seconds);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        fadeRoutine = null;
    }
}
