using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 트래커 HUD 싱글톤. 상태를 구독하고 등장·완료·교체 애니메이션을 조율한다.
/// </summary>
public sealed class QuestTrackerHudController : SingletonMonoBehaviour<QuestTrackerHudController>
{
    protected override bool PersistAcrossScenes => true;

    [SerializeField] bool autoLoadTutorialCatalog = true;

    QuestTrackerState trackerState;
    QuestTrackerHudView hudView;
    CanvasGroup hudCanvasGroup;
    RectTransform hudRoot;
    string pendingNextQuestId;
    Coroutine introRoutine;
    Coroutine crossfadeRoutine;

    public QuestTrackerState TrackerState => trackerState;
    public QuestTrackerHudView HudView => hudView;

    protected override void OnSingletonAwake()
    {
        if (autoLoadTutorialCatalog)
            InitializeFromTutorialCatalog();
    }

    void Update()
    {
        if (!QuestTrackerHudHost.ShouldAttachHud(SceneManager.GetActiveScene().name)
            && hudView != null
            && hudView.gameObject.activeSelf)
            hudView.gameObject.SetActive(false);
    }

    public void Initialize(QuestTrackerState state)
    {
        trackerState = state;
    }

    public void InitializeFromTutorialCatalog()
    {
        TutorialQuestCatalog catalog = TutorialQuestCatalog.GetOrCreate();
        if (catalog == null)
            return;

        trackerState = new QuestTrackerState(catalog.ToDefinitions());
    }

    public bool PresentQuest(string questId, bool playIntro = true)
    {
        if (trackerState == null)
            return false;

        if (!QuestTrackerHudHost.ShouldAttachHud(SceneManager.GetActiveScene().name))
            return false;

        AttachHudToScene(SceneManager.GetActiveScene());
        if (!trackerState.TrySetCurrentQuest(questId))
            return false;

        pendingNextQuestId = null;
        RefreshFromState(immediate: !playIntro);
        if (playIntro)
            PlayIntroAnimation();

        return true;
    }

    public void RefreshFromState(bool immediate = false)
    {
        if (trackerState == null || hudView == null)
            return;

        hudView.RefreshFromState(trackerState);
        if (immediate && hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = 1f;
            if (hudRoot != null)
                hudRoot.anchoredPosition = new Vector2(-QuestTrackerStylePalette.MarginRight, -QuestTrackerStylePalette.MarginTop);
        }
    }

    public bool AdvanceStep()
    {
        if (trackerState == null)
            return false;

        bool advanced = trackerState.AdvanceStep();
        if (!advanced)
            return false;

        RefreshFromState(immediate: true);
        if (trackerState.IsQuestCleared)
            HandleQuestCleared();

        return true;
    }

    /// <summary>
    /// 현재 활성 단계 id가 일치할 때만 완료합니다. 마지막 단계면 완료 배너·다음 퀘스트 교체를 처리합니다.
    /// </summary>
    public bool TryCompleteTutorialStep(string stepId)
    {
        if (trackerState == null || string.IsNullOrWhiteSpace(stepId))
            return false;

        if (!trackerState.CompleteStep(stepId))
            return false;

        RefreshFromState(immediate: true);
        if (trackerState.IsQuestCleared)
        {
            string nextQuestId = TutorialQuestProgressAdapter.GetNextQuestId(trackerState.CurrentQuestId);
            if (!string.IsNullOrWhiteSpace(nextQuestId))
                QueueCrossfadeToQuest(nextQuestId);

            HandleQuestCleared();
        }

        return true;
    }

    public void QueueCrossfadeToQuest(string nextQuestId, float delaySeconds = QuestTrackerStylePalette.CrossfadeDelayAfterClearSeconds)
    {
        pendingNextQuestId = nextQuestId;
        if (trackerState != null && trackerState.IsQuestCleared)
            StartCrossfade(delaySeconds);
    }

    public void PlayIntroAnimation()
    {
        if (hudCanvasGroup == null || hudRoot == null)
            return;

        if (introRoutine != null)
            StopCoroutine(introRoutine);

        introRoutine = StartCoroutine(IntroRoutine());
    }

    void HandleQuestCleared()
    {
        hudView?.SetClearedVisuals(true);
        if (!string.IsNullOrWhiteSpace(pendingNextQuestId))
            StartCrossfade(QuestTrackerStylePalette.CrossfadeDelayAfterClearSeconds);
    }

    void StartCrossfade(float delaySeconds)
    {
        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeRoutine(delaySeconds));
    }

    IEnumerator IntroRoutine()
    {
        Vector2 target = new Vector2(-QuestTrackerStylePalette.MarginRight, -QuestTrackerStylePalette.MarginTop);
        Vector2 start = target + new Vector2(QuestTrackerStylePalette.IntroSlideOffset, 0f);
        hudRoot.anchoredPosition = start;
        hudCanvasGroup.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < QuestTrackerStylePalette.IntroDurationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / QuestTrackerStylePalette.IntroDurationSeconds);
            hudRoot.anchoredPosition = Vector2.Lerp(start, target, t);
            hudCanvasGroup.alpha = t;
            yield return null;
        }

        hudRoot.anchoredPosition = target;
        hudCanvasGroup.alpha = 1f;
        introRoutine = null;
    }

    IEnumerator CrossfadeRoutine(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);

        string nextQuestId = pendingNextQuestId;
        pendingNextQuestId = null;
        if (string.IsNullOrWhiteSpace(nextQuestId) || trackerState == null)
        {
            crossfadeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < QuestTrackerStylePalette.CrossfadeDurationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / QuestTrackerStylePalette.CrossfadeDurationSeconds);
            hudCanvasGroup.alpha = 1f - t;
            yield return null;
        }

        if (!trackerState.TrySetCurrentQuest(nextQuestId))
        {
            hudCanvasGroup.alpha = 1f;
            crossfadeRoutine = null;
            yield break;
        }

        RefreshFromState(immediate: true);
        PlayIntroAnimation();
        crossfadeRoutine = null;
    }

    /// <summary>
    /// 활성 씬 Canvas 아래에 HUD를 부착합니다. 씬 전환 시 이전 HUD는 파괴되고 상태만 DDOL 컨트롤러에 유지됩니다.
    /// </summary>
    public void AttachHudToScene(Scene scene)
    {
        if (!QuestTrackerHudHost.ShouldAttachHud(scene.name))
        {
            DestroyHudVisual();
            return;
        }

        if (hudView == null)
        {
            hudCanvasGroup = null;
            hudRoot = null;
        }

        Transform parent = QuestTrackerHudHost.ResolveCanvasParent();
        GameObject keepRoot = hudView != null && QuestTrackerHudHost.HasHudRootInScene(scene, hudView.gameObject)
            ? hudView.gameObject
            : null;

        QuestTrackerHudHost.DestroyExtraHudRoots(scene, keepRoot);

        if (hudView != null && hudView.gameObject.scene == scene)
        {
            if (hudView.transform.parent != parent)
                hudView.transform.SetParent(parent, false);

            if (!hudView.gameObject.activeSelf)
                hudView.gameObject.SetActive(true);

            return;
        }

        DestroyHudVisual();
        QuestTrackerHudHost.DestroyExtraHudRoots(scene, null);
        CreateHudUnder(parent);
        RefreshFromState(immediate: true);
    }

    void CreateHudUnder(Transform parent)
    {
        if (hudView != null)
            return;

        QuestTrackerHudFactory.BuiltHud built = QuestTrackerHudFactory.Create(parent, gameObject.layer);
        hudView = built.View;
        hudCanvasGroup = built.CanvasGroup;
        hudRoot = built.Root;
    }

    void DestroyHudVisual()
    {
        if (hudView == null)
            return;

        if (Application.isPlaying)
            Destroy(hudView.gameObject);
        else
            DestroyImmediate(hudView.gameObject);

        hudView = null;
        hudCanvasGroup = null;
        hudRoot = null;
    }

    internal void EnsureHudForTests(Transform parent)
    {
        DestroyHudVisualForTests();
        CreateHudUnder(parent);
    }

    internal void DestroyHudVisualForTests()
    {
        DestroyHudVisual();
    }

    internal static void ResetInstanceForTests()
    {
        Instance = null;
    }
}
