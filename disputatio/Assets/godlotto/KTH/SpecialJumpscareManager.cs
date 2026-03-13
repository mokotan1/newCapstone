using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering; 
using UnityEngine.Rendering.Universal; 

public class SpecialJumpscareManager : MonoBehaviour
{
    public static SpecialJumpscareManager Instance; 

    [Header("효과 설정 (셰이더 & 블러)")]
    public Image blinkImage; 
    public Volume globalVolume; 

    [Header("시간 및 확률 설정")]
    public float waitTimeToScare = 3f;
    [Range(0f, 100f)]
    public float spawnChance = 100f;
    public float blinkDuration = 0.2f; 
    public float closedDuration = 0.1f; 
    public string retrySceneName = "MainScene";

    [Header("오브젝트 및 UI")]
    public GameObject parrotObject;
    public RectTransform triggerButtonRect;
    public Animator jumpscareAnimator;
    public GameObject gameOverPanel;
    public Button retryButton;

    private static bool hasVisitedSpecialScene = false;
    private bool hasTriggered = false;
    private DepthOfField dof;
    private readonly int blinkAmountProp = Shader.PropertyToID("_BlinkAmount");

    private void Awake() { if (Instance == null) Instance = this; }

    void Start()
    {
        if (blinkImage != null && blinkImage.material != null)
        {
            blinkImage.material = new Material(blinkImage.material);
            blinkImage.material.SetFloat(blinkAmountProp, 0.5f);
        }
        if (globalVolume != null && globalVolume.profile.TryGet(out dof))
            dof.gaussianMaxRadius.value = 0f;

        jumpscareAnimator.gameObject.SetActive(false);
        gameOverPanel.SetActive(false);

        if (!hasVisitedSpecialScene)
        {
            float randomValue = Random.Range(0f, 100f);
            if (randomValue <= spawnChance)
            {
                hasVisitedSpecialScene = true;
                SetupEnemyState(true);
            }
            else ShowParrotOnly();
        }
        else ShowParrotOnly();
    }

    private void SetupEnemyState(bool isPresent)
    {
        if (isPresent)
        {
            if (parrotObject != null) parrotObject.SetActive(false);
            triggerButtonRect.gameObject.SetActive(true);
            
            triggerButtonRect.GetComponent<Button>().onClick.RemoveAllListeners();
            triggerButtonRect.GetComponent<Button>().onClick.AddListener(ExecuteJumpscare);
            StartCoroutine(WaitAndExecuteScare());
        }
    }

    private void ShowParrotOnly()
    {
        if (parrotObject != null) parrotObject.SetActive(true);
        triggerButtonRect.gameObject.SetActive(false);
    }

    private IEnumerator WaitAndExecuteScare()
    {
        yield return new WaitForSeconds(waitTimeToScare);
        ExecuteJumpscare();
    }

    public void ExecuteJumpscare()
    {
        if (hasTriggered) return;
        hasTriggered = true;
        StopAllCoroutines();
        triggerButtonRect.gameObject.SetActive(false);
        StartCoroutine(FullJumpscareSequence());
    }

    private IEnumerator FullJumpscareSequence()
    {
        yield return StartCoroutine(AnimateBlink(0.5f, 0f, 0f, 2.0f, blinkDuration));
        yield return new WaitForSeconds(closedDuration);
        
        jumpscareAnimator.gameObject.SetActive(true);
        jumpscareAnimator.SetTrigger("Scare");
        yield return StartCoroutine(AnimateBlink(0f, 0.5f, 2.0f, 0f, blinkDuration));
    }

    private IEnumerator AnimateBlink(float bStart, float bEnd, float blStart, float blEnd, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            blinkImage.material.SetFloat(blinkAmountProp, Mathf.Lerp(bStart, bEnd, t));
            if (dof != null) dof.gaussianMaxRadius.value = Mathf.Lerp(blStart, blEnd, t);
            yield return null;
        }
        blinkImage.material.SetFloat(blinkAmountProp, bEnd);
        if (dof != null) dof.gaussianMaxRadius.value = blEnd;
    }

    public void OnJumpscareFinished()
    {
        jumpscareAnimator.gameObject.SetActive(false);
        gameOverPanel.SetActive(true);
        retryButton.onClick.RemoveAllListeners();
        retryButton.onClick.AddListener(() => SceneManager.LoadScene(retrySceneName));
    }
}