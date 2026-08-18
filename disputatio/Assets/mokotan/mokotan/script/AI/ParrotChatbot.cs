using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 앵무새 전용 프롬프트. GlobalChatbot의 입력 필드·전송·Tool Calling 처리를 그대로 씁니다.
/// </summary>
public class ParrotChatbot : GlobalChatbot
{
    /// <summary>공통 90자 규칙과 충돌하므로 이 봇만 초단문 전용 규칙만 사용.</summary>
    protected override bool AppendCommonChesterVoiceBlock => false;

    [Header("Tool Calling hooks (optional)")]
    [Tooltip("Invoked when the model calls emote(emotion). Wire Animator triggers / SFX in the inspector.")]
    [SerializeField] private UnityEvent<string> onEmotionTool;

    [Tooltip("Invoked when the model calls give_hint(hint_level, target_object, hint_category).")]
    [SerializeField] private UnityEvent<string, string, string> onGiveHintTool;

    protected override string BuildFinalSystemPrompt(string locale)
    {
        string prompt = CheshirePromptCatalog.Load("ParrotPrompt", locale);
        if (!string.IsNullOrEmpty(prompt))
            return prompt;

        // Legacy inline fallback if catalog asset is missing.
        return @"[수수께끼 앵무새 규칙]
1. 모든 답변은 공백 포함 한글 20자 이내로 할 것.
2. 수수께끼를 내거나 사용자의 오답을 비웃을 것.
3. 죽어도 정답은 알려주지 말 것.
4. 말끝에 '깍!', '삐약!', '푸드덕!' 중 하나를 무조건 붙일 것.";
    }

    protected override void ApplyGiveHint(Dictionary<string, object> args)
    {
        base.ApplyGiveHint(args);
        if (args == null) return;
        string level = ChatbotToolArgs.GetString(args, "hint_level", "subtle");
        string target = ChatbotToolArgs.GetString(args, "target_object");
        string category = ChatbotToolArgs.GetString(args, "hint_category");
        onGiveHintTool?.Invoke(level, target, category);
    }

    protected override void ApplyEmote(Dictionary<string, object> args)
    {
        base.ApplyEmote(args);
        string emotion = ChatbotToolArgs.GetString(args, "emotion");
        if (!string.IsNullOrEmpty(emotion))
            onEmotionTool?.Invoke(emotion);
    }
}
