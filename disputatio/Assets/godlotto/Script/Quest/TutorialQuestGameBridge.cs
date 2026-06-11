using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 로드·Fungus 플래그·Kitchen 블록 종료를 퀘스트 트래커 HUD에 연결합니다.
/// 기존 퍼즐/인벤토리 로직은 변경하지 않고 외부 이벤트만 구독합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialQuestGameBridge : MonoBehaviour
{
    bool introPresented;
    bool lastElectricOn;
    bool lastGetBottle;
    bool fungusEdgesInitialized;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BlockSignals.OnBlockEnd += OnBlockEnd;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        BlockSignals.OnBlockEnd -= OnBlockEnd;
    }

    void Start()
    {
        ProcessActiveScene(SceneManager.GetActiveScene());
    }

    void Update()
    {
        PollFungusFlagEdges();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ProcessActiveScene(scene);
    }

    void ProcessActiveScene(Scene scene)
    {
        QuestTrackerHudBootstrap.EnsureSystems();
        QuestTrackerHudController hud = QuestTrackerHudController.Instance;
        if (hud == null)
            return;

        hud.AttachHudToScene(scene);

        if (TutorialQuestWorldScenes.ShouldHideTutorialHud(scene.name))
            return;

        if (hud.TrackerState == null)
            return;

        TutorialQuestWorldFlags flags = TutorialQuestWorldReader.ReadCurrent();
        BootstrapQuestIfNeeded(hud, flags);
        ApplyCatchUp(hud, flags);
        TryCompleteFromSceneEntry(hud, flags);
        ResetFungusEdgeTracking(flags);
    }

    void BootstrapQuestIfNeeded(QuestTrackerHudController hud, TutorialQuestWorldFlags flags)
    {
        if (!string.IsNullOrEmpty(hud.TrackerState.CurrentQuestId))
            return;

        if (TutorialQuestProgressAdapter.IsTutorialFullyComplete(flags))
            return;

        string questId = TutorialQuestProgressAdapter.ResolveInitialQuestId(flags);
        if (string.IsNullOrWhiteSpace(questId))
            return;

        hud.PresentQuest(questId, playIntro: !introPresented);
        introPresented = true;
    }

    void ApplyCatchUp(QuestTrackerHudController hud, TutorialQuestWorldFlags flags)
    {
        IReadOnlyList<string> catchUpStepIds = TutorialQuestProgressAdapter.ResolveCatchUpStepIds(
            hud.TrackerState,
            flags);

        for (int i = 0; i < catchUpStepIds.Count; i++)
            hud.TryCompleteTutorialStep(catchUpStepIds[i]);
    }

    void TryCompleteFromSceneEntry(QuestTrackerHudController hud, TutorialQuestWorldFlags flags)
    {
        QuestTrackerState state = hud.TrackerState;
        if (state == null || state.IsQuestCleared || string.IsNullOrEmpty(state.CurrentQuestId))
            return;

        if (!TutorialQuestProgressAdapter.TryMapSceneEntryToStepId(
                state.CurrentQuestId,
                flags.ActiveSceneName,
                flags,
                out string stepId))
            return;

        hud.TryCompleteTutorialStep(stepId);
    }

    void PollFungusFlagEdges()
    {
        TutorialQuestWorldFlags flags = TutorialQuestWorldReader.ReadCurrent();
        if (!fungusEdgesInitialized)
        {
            ResetFungusEdgeTracking(flags);
            return;
        }

        if (flags.ElectricOn && !lastElectricOn)
            TryCompleteFromFungusFlag(FungusVariableKeys.ElectricOn, true);

        if (flags.GetBottle && !lastGetBottle)
            TryCompleteFromFungusFlag(FungusVariableKeys.GetBottle, true);

        lastElectricOn = flags.ElectricOn;
        lastGetBottle = flags.GetBottle;
    }

    void ResetFungusEdgeTracking(TutorialQuestWorldFlags flags)
    {
        lastElectricOn = flags.ElectricOn;
        lastGetBottle = flags.GetBottle;
        fungusEdgesInitialized = true;
    }

    void TryCompleteFromFungusFlag(string fungusKey, bool isEnabled)
    {
        QuestTrackerHudController hud = QuestTrackerHudController.Instance;
        QuestTrackerState state = hud?.TrackerState;
        if (state == null || state.IsQuestCleared || string.IsNullOrEmpty(state.CurrentQuestId))
            return;

        if (!TutorialQuestProgressAdapter.TryMapFungusFlagEdgeToStepId(
                state.CurrentQuestId,
                fungusKey,
                isEnabled,
                out string stepId))
            return;

        hud.TryCompleteTutorialStep(stepId);
    }

    void OnBlockEnd(Block block)
    {
        if (block == null)
            return;

        if (!TutorialQuestWorldScenes.IsKitchenScene(SceneManager.GetActiveScene().name))
            return;

        if (!TutorialQuestProgressAdapter.IsTrackedKitchenBlock(block.BlockName))
            return;

        StartCoroutine(CompleteKitchenBlockStepNextFrame(block.BlockName));
    }

    IEnumerator CompleteKitchenBlockStepNextFrame(string blockName)
    {
        yield return null;

        QuestTrackerHudController hud = QuestTrackerHudController.Instance;
        QuestTrackerState state = hud?.TrackerState;
        if (state == null || state.IsQuestCleared)
            yield break;

        if (!TutorialQuestProgressAdapter.TryMapKitchenBlockToStepId(blockName, out string stepId))
            yield break;

        hud.TryCompleteTutorialStep(stepId);
    }
}
