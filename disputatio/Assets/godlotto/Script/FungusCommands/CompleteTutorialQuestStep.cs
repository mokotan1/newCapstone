using Fungus;
using UnityEngine;

/// <summary>
/// Fungus 시퀀스에서 현재 활성 튜토리얼 퀘스트 단계를 완료합니다.
/// step id는 <see cref="TutorialQuestIds"/> 상수를 사용하세요.
/// </summary>
[CommandInfo("Quest",
    "Complete Tutorial Quest Step",
    "현재 활성 튜토리얼 퀘스트 단계를 완료합니다. step id는 TutorialQuestIds를 사용합니다.")]
[AddComponentMenu("")]
public class CompleteTutorialQuestStep : Command
{
    [Tooltip("완료할 단계 id (예: TutorialQuestIds.LightTheManorSteps.GoKitchen)")]
    [SerializeField] string stepId = TutorialQuestIds.LightTheManorSteps.GoKitchen;

    public override void OnEnter()
    {
        QuestTrackerHudController hud = QuestTrackerHudController.Instance;
        if (hud == null)
        {
            GameLog.LogWarning("[CompleteTutorialQuestStep] QuestTrackerHudController.Instance가 null입니다.");
            Continue();
            return;
        }

        if (!hud.TryCompleteTutorialStep(stepId))
            GameLog.LogWarning($"[CompleteTutorialQuestStep] 단계 '{stepId}'를 완료하지 못했습니다.");

        Continue();
    }
}
