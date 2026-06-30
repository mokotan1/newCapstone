using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class GlobalChatbot : BaseChatbot
{
    [Header("GlobalBot UI")]
    [SerializeField] public Flowchart globalFlowchart;

    /// <summary>
    /// 패널이 열려 있는 동안 SayDialog를 유지합니다.
    /// BaseChatbot 기본값(true)은 대사 완료 후 SayDialog를 끄는데,
    /// Parret_Panel 안에서는 패널이 닫힐 때 TutorPanelSayDialogSync가 처리하므로 꺼선 안 됩니다.
    /// </summary>
    protected override bool DeactivateSayDialogWhenLineCompletes => false;

    private const int BottleItemId = 1;
    private const string FallbackSystemPrompt = "당신은 저택의 도우미입니다.";

    protected override string BuildFinalSystemPrompt()
    {
        TextAsset promptAsset = Resources.Load<TextAsset>("introPrompt");
        string finalSystemPrompt = promptAsset != null ? promptAsset.text : FallbackSystemPrompt;

        if (globalFlowchart == null)
            return finalSystemPrompt;

        finalSystemPrompt += ItemAcquisitionTracker.BuildPromptSection(globalFlowchart);
        finalSystemPrompt += BuildBottleHintIfNeeded();

        return finalSystemPrompt;
    }

    private string BuildBottleHintIfNeeded()
    {
        if (chatHistory.Count == 0)
            return string.Empty;

        string lastMessage = chatHistory[chatHistory.Count - 1].content;
        bool mentionsBottle = lastMessage.Contains("물병") || lastMessage.Contains("병");

        if (mentionsBottle && ItemAcquisitionTracker.IsAcquired(globalFlowchart, BottleItemId))
            return "\n\n[중요 지시] 플레이어는 물병 단서를 가졌습니다. 부력에 대해 수수께끼로 답변하세요.";

        return string.Empty;
    }

    protected override HeuristicSignalInput BuildHeuristicSignalInput(string userMessage)
    {
        var signal = base.BuildHeuristicSignalInput(userMessage);
        signal.roomName = nameof(GlobalChatbot);

        if (globalFlowchart != null)
        {
            bool hasBottle = ItemAcquisitionTracker.IsAcquired(globalFlowchart, BottleItemId);
            signal.progressScore = hasBottle ? 0.7f : 0.3f;
            signal.accuracyScore = hasBottle ? 0.65f : 0.45f;
        }

        return signal;
    }

    protected override IEnumerator HandleChatbotResponse(string responseMessage, List<FunctionCallData> functionCalls)
    {
        bool isComplete = false;
        Say(responseMessage, () => isComplete = true);
        yield return new WaitUntil(() => isComplete);

        ProcessCommonFunctionCalls(functionCalls);
    }

    /// <summary>Dispatch give_hint / emote (and log unknown tools). Subclasses may override to extend.</summary>
    protected virtual void ProcessCommonFunctionCalls(List<FunctionCallData> functionCalls)
    {
        if (functionCalls == null) return;
        foreach (var fc in functionCalls)
        {
            if (string.IsNullOrEmpty(fc.name))
                continue;
            switch (fc.name)
            {
                case "give_hint":
                    RecordGiveHintToolCall(fc.arguments);
                    ApplyGiveHint(fc.arguments);
                    break;
                case "emote":
                    ApplyEmote(fc.arguments);
                    break;
                default:
                    GameLog.LogWarning($"[{GetType().Name}] Unhandled tool call: {fc.name}");
                    break;
            }
        }
    }

    void RecordGiveHintToolCall(Dictionary<string, object> args)
    {
        if (args == null)
            return;

        string level = ChatbotToolArgs.GetString(args, "hint_level", "subtle");
        PlayLogRecorder.RecordGiveHint(level, PlayLogRecorder.BuildProgressStateSnapshot(GetType().Name));
    }

    protected virtual void ApplyGiveHint(Dictionary<string, object> args)
    {
        if (args == null) return;
        string level = ChatbotToolArgs.GetString(args, "hint_level", "subtle");
        string target = ChatbotToolArgs.GetString(args, "target_object");
        string category = ChatbotToolArgs.GetString(args, "hint_category");
        GameLog.Log($"[Hint] level={level}, target={target}, category={category}");
    }

    protected virtual void ApplyEmote(Dictionary<string, object> args)
    {
        if (args == null) return;
        string emotion = ChatbotToolArgs.GetString(args, "emotion");
        if (string.IsNullOrEmpty(emotion)) return;
        GameLog.Log($"Chester emote: {emotion}");
    }
}
