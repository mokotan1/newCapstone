using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Fungus;

public class GlobalChatbot : BaseChatbot
{
    [Header("GlobalBot UI")]
    [SerializeField] public Flowchart globalFlowchart;

    protected override bool DeactivateSayDialogWhenLineCompletes => false;

    private const int BottleItemId = 1;
    private const string FallbackSystemPrompt = "당신은 저택의 도우미입니다.";

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string roomPrompt = CheshirePromptCatalog.Load("introPrompt", locale);
        string finalSystemPrompt = !string.IsNullOrEmpty(roomPrompt) ? roomPrompt : FallbackSystemPrompt;

        if (globalFlowchart == null)
            return finalSystemPrompt;

        finalSystemPrompt += ItemAcquisitionTracker.BuildPromptSection(globalFlowchart, locale);
        return finalSystemPrompt;
    }

    protected override void AugmentChatPayload(LocalLlamaPayload payload, string userMessage)
    {
        if (globalFlowchart == null)
            return;

        string locale = CheshireLocaleResolver.ResolveCurrentLocale();
        bool hasBottle = ItemAcquisitionTracker.IsAcquired(globalFlowchart, BottleItemId);
        if (CheshireHintRewritePlanner.TryBuildBottleUseHint(
                userMessage, hasBottle, locale, out HintRewritePayload hintRewrite))
            payload.hint_rewrite = hintRewrite;
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

    /// <summary>Dispatch give_hint / emote and log unknown tools. Subclasses may override to extend.</summary>
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

    private void RecordGiveHintToolCall(Dictionary<string, object> args)
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
