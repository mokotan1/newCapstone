using System;
using System.Collections.Generic;

public static class CheshireHintRewritePlanner
{
    public static bool TryBuildBottleUseHint(
        string userMessage,
        bool hasBottle,
        out HintRewritePayload payload)
        => TryBuildBottleUseHint(
            userMessage,
            hasBottle,
            CheshireLocaleResolver.ResolveCurrentLocale(),
            out payload);

    public static bool TryBuildBottleUseHint(
        string userMessage,
        bool hasBottle,
        string locale,
        out HintRewritePayload payload)
    {
        payload = null;
        if (!hasBottle || string.IsNullOrWhiteSpace(userMessage))
            return false;

        string normalized = userMessage.Trim().ToLowerInvariant();
        bool mentionsBottle = ContainsAny(normalized, "병", "물병", "bottle", "ボトル", "びん");
        bool asksUse = ContainsAny(normalized, "어디", "쓰", "사용", "use", "where", "どこ", "使い");
        if (!mentionsBottle || !asksUse)
            return false;

        string canonical = CheshireLocaleResolver.NormalizeLocale(locale);
        payload = BuildBottleSinkPayload(canonical);
        return true;
    }

    static HintRewritePayload BuildBottleSinkPayload(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return new HintRewritePayload
                {
                    hint_id = "bottle_sink_use",
                    item_id = "bottle",
                    hint_target = "kitchen_sink",
                    hint_level = "direct",
                    base_hint = "その瓶は台所のシンクで使える。",
                    required_terms = new List<string> { "瓶", "シンク" },
                    forbidden_terms = new List<string> { "鍵", "パスワード", "正解", "番号" },
                    fallback_line = "その瓶は台所のシンクで水を入れられる。",
                    narrative_seed = "チェシャーは茶目っ気があるがプレイヤーを嘲らない。ヒントはアイテムの使い場所だけを示唆する。",
                    interaction_type = "item_usage_hint",
                    allow_highlight = true
                };
            case CheshireLocaleResolver.English:
                return new HintRewritePayload
                {
                    hint_id = "bottle_sink_use",
                    item_id = "bottle",
                    hint_target = "kitchen_sink",
                    hint_level = "direct",
                    base_hint = "That bottle can be used at the kitchen sink.",
                    required_terms = new List<string> { "bottle", "sink" },
                    forbidden_terms = new List<string> { "key", "password", "answer", "code" },
                    fallback_line = "That bottle can be filled with water at the kitchen sink.",
                    narrative_seed = "Cheshire is playful but does not mock the player. The hint only implies where to use the item.",
                    interaction_type = "item_usage_hint",
                    allow_highlight = true
                };
            default:
                return new HintRewritePayload
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
        }
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
