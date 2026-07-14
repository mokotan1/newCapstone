using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class QuestTrackerHudTests
{
    GameObject canvasObject;
    GameObject controllerObject;
    QuestTrackerHudController controller;
    QuestTrackerState tracker;

    [SetUp]
    public void SetUp()
    {
        QuestTrackerHudController.ResetInstanceForTests();
        TutorialQuestCatalog.ResetCacheForTest();

        canvasObject = new GameObject("HudTestCanvas", typeof(RectTransform), typeof(Canvas));
        controllerObject = new GameObject("QuestTrackerHudController");
        controller = controllerObject.AddComponent<QuestTrackerHudController>();

        TutorialQuestCatalog catalog = TutorialQuestCatalog.GetOrCreate();
        Assert.IsNotNull(catalog, "TutorialQuestCatalog asset is required.");
        tracker = new QuestTrackerState(catalog.ToDefinitions());
        controller.Initialize(tracker);
        controller.EnsureHudForTests(canvasObject.transform);
        controller.RefreshFromState(immediate: true);
    }

    [TearDown]
    public void TearDown()
    {
        QuestTrackerHudController.ResetInstanceForTests();
        if (controllerObject != null)
            Object.DestroyImmediate(controllerObject);
        if (canvasObject != null)
            Object.DestroyImmediate(canvasObject);
    }

    [Test]
    public void EnsureHud_CreatesRootUnderCanvas()
    {
        Transform hudRoot = FindTransformIncludingInactive(QuestTrackerHudFactory.RootObjectName);
        Assert.IsNotNull(hudRoot);
        Assert.AreSame(canvasObject.transform, hudRoot.parent);
    }

    [Test]
    public void RefreshFromState_OnlyCurrentStepIsActive()
    {
        tracker.TrySetCurrentQuest(TutorialQuestIds.LightTheManor);
        controller.RefreshFromState(immediate: true);

        int activeCount = controller.HudView.StepRows.Count(row => row.CurrentPhase == QuestStepPhase.Active);
        Assert.AreEqual(1, activeCount);
        Assert.AreEqual(QuestStepPhase.Active, controller.HudView.GetStepPhaseAt(0));
        Assert.AreEqual(QuestStepPhase.Pending, controller.HudView.GetStepPhaseAt(1));
        Assert.AreEqual(QuestStepPhase.Pending, controller.HudView.GetStepPhaseAt(2));
    }

    [Test]
    public void RefreshFromState_CompletedStepsRenderAsDone()
    {
        tracker.TrySetCurrentQuest(TutorialQuestIds.LightTheManor);
        tracker.AdvanceStep();
        controller.RefreshFromState(immediate: true);

        Assert.AreEqual(QuestStepPhase.Completed, controller.HudView.GetStepPhaseAt(0));
        Assert.AreEqual(QuestStepPhase.Active, controller.HudView.GetStepPhaseAt(1));
    }

    [Test]
    public void RefreshFromState_ClearedQuestShowsCompletionBanner()
    {
        tracker.TrySetCurrentQuest(TutorialQuestIds.LightTheManor);
        tracker.AdvanceStep();
        tracker.AdvanceStep();
        tracker.AdvanceStep();
        controller.RefreshFromState(immediate: true);

        Assert.IsTrue(tracker.IsQuestCleared);
        Transform banner = FindTransformIncludingInactive("ClearedBanner");
        Assert.IsNotNull(banner);
        Assert.IsTrue(banner.gameObject.activeSelf);
        Assert.AreEqual(0, controller.HudView.StepRows.Count(row => row.CurrentPhase == QuestStepPhase.Active));
    }

    [Test]
    public void TryCompleteTutorialStep_CompletesOnlyMatchingActiveStep()
    {
        tracker.TrySetCurrentQuest(TutorialQuestIds.LightTheManor);
        controller.RefreshFromState(immediate: true);

        Assert.IsFalse(controller.TryCompleteTutorialStep(TutorialQuestIds.LightTheManorSteps.RaiseBreaker));
        Assert.IsTrue(controller.TryCompleteTutorialStep(TutorialQuestIds.LightTheManorSteps.GoKitchen));
        Assert.AreEqual(QuestStepPhase.Completed, controller.HudView.GetStepPhaseAt(0));
        Assert.AreEqual(QuestStepPhase.Active, controller.HudView.GetStepPhaseAt(1));
    }

    [Test]
    public void Hud_DoesNotBlockPointerInput()
    {
        Transform hudRoot = FindTransformIncludingInactive(QuestTrackerHudFactory.RootObjectName);
        var canvasGroup = hudRoot.GetComponent<CanvasGroup>();
        Assert.IsFalse(canvasGroup.blocksRaycasts);
        Assert.IsFalse(canvasGroup.interactable);

        foreach (Graphic graphic in hudRoot.GetComponentsInChildren<Graphic>(true))
            Assert.IsFalse(graphic.raycastTarget, $"Expected {graphic.name} to ignore raycasts.");
    }

    [Test]
    public void CrossfadeTiming_MatchesSpec()
    {
        Assert.AreEqual(1.5f, QuestTrackerStylePalette.CrossfadeDelayAfterClearSeconds, 0.001f);
        Assert.AreEqual(0.35f, QuestTrackerStylePalette.IntroDurationSeconds, 0.001f);
        Assert.AreEqual(0.35f, QuestTrackerStylePalette.CrossfadeDurationSeconds, 0.001f);
    }

    [Test]
    public void FinalQuest_WhenCleared_DismissesHudRoot()
    {
        controller.SetClearTransitionDelayForTests(0f);
        Assert.IsTrue(tracker.TrySetCurrentQuest(TutorialQuestIds.BottleKey));
        controller.RefreshFromState(immediate: true);
        Assert.IsTrue(controller.HudView.gameObject.activeSelf);

        Assert.IsTrue(controller.AdvanceStep());
        Assert.IsTrue(controller.AdvanceStep());
        Assert.IsTrue(controller.AdvanceStep());

        Assert.IsTrue(tracker.IsQuestCleared);
        Assert.AreEqual(TutorialQuestIds.BottleKey, tracker.CurrentQuestId);
        Assert.IsFalse(controller.HudView.gameObject.activeSelf);
    }

    [Test]
    public void KoreanQuestTitle_RendersFromCatalog()
    {
        tracker.TrySetCurrentQuest(TutorialQuestIds.LightTheManor);
        controller.RefreshFromState(immediate: true);

        Transform questNameTransform = FindTransformIncludingInactive("QuestName");
        var questName = questNameTransform.GetComponent<TextMeshProUGUI>();
        Assert.IsNotNull(questName);
        Assert.AreEqual("저택에 불을 밝혀라", questName.text);
        Assert.IsFalse(string.IsNullOrEmpty(questName.text));
        Assert.IsNotNull(questName.font);
    }

    [Test]
    public void HudRoot_UsesTopRightPlacementFromSpec()
    {
        Transform hudRoot = FindTransformIncludingInactive(QuestTrackerHudFactory.RootObjectName);
        var rect = hudRoot.GetComponent<RectTransform>();

        Assert.AreEqual(new Vector2(1f, 1f), rect.anchorMin);
        Assert.AreEqual(new Vector2(1f, 1f), rect.anchorMax);
        Assert.AreEqual(new Vector2(-QuestTrackerStylePalette.MarginRight, -QuestTrackerStylePalette.MarginTop), rect.anchoredPosition);
        Assert.AreEqual(QuestTrackerStylePalette.PanelWidth, rect.sizeDelta.x, 0.01f);
    }

    static Transform FindTransformIncludingInactive(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform item in transforms)
        {
            if (item.name == objectName)
                return item;
        }

        Assert.Fail($"Expected to find {objectName}.");
        return null;
    }
}
