using System;
using System.Collections.Generic;

public static class CheshireHintRewritePlanner
{
    public static bool TryBuildBottleUseHint(
        string userMessage,
        bool hasBottle,
        out HintRewritePayload payload)
    {
        payload = null;
        if (!hasBottle || string.IsNullOrWhiteSpace(userMessage))
            return false;

        string normalized = userMessage.Trim().ToLowerInvariant();
        bool mentionsBottle = ContainsAny(normalized, "병", "물병", "bottle");
        bool asksUse = ContainsAny(normalized, "어디", "쓰", "사용", "use", "where");
        if (!mentionsBottle || !asksUse)
            return false;

        payload = new HintRewritePayload
        {
            hint_id = "bottle_sink_use",
            item_id = "bottle",
            hint_target = "kitchen_sink",
            hint_level = "direct",
            base_hint = "그 병은 주방 싱크대에서 사용할 수 있다.",
            required_terms = new List<string> { "병", "싱크대" },
            forbidden_terms = new List<string> { "열쇠", "비밀번호", "정답", "번호" },
            fallback_line = "그 병은 주방 싱크대에서 물을 채워볼 수 있다.",
            narrative_seed = "체셔는 장난스럽지만 플레이어를 조롱하지 않는다. 힌트는 아이템 사용처만 암시한다.",
            interaction_type = "item_usage_hint",
            allow_highlight = true
        };
        return true;
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
        {
            if (value.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
