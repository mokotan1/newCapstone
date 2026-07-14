using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Locale-aware player-facing Say lines and synthetic tutor user prompts (ko/ja/en).
/// Loads <c>Resources/Scenario/cheshire_ui_strings.csv</c> via <see cref="ScenarioLocalizationTable"/>
/// (same id,ko,en,ja + KO fallback pattern as scenario dialogue). Hardcoded strings remain last-resort.
/// Snapshot one locale via <see cref="CheshireLocaleResolver.ResolveCurrentLocale"/> or a passed param.
/// </summary>
public static class CheshireUiStrings
{
    private const string ResourcePath = "Scenario/cheshire_ui_strings";

    private static readonly Dictionary<string, ScenarioLocalizationTable> TablesByLocale =
        new Dictionary<string, ScenarioLocalizationTable>(StringComparer.Ordinal);

    private static string _csvTextOverride;

    /// <summary>EditMode tests: inject CSV text and clear locale cache.</summary>
    internal static void SetCsvTextOverrideForTests(string csv)
    {
        _csvTextOverride = csv ?? "";
        TablesByLocale.Clear();
    }

    /// <summary>EditMode tests: restore Resources-backed loading.</summary>
    internal static void ClearCsvOverrideForTests()
    {
        _csvTextOverride = null;
        TablesByLocale.Clear();
    }

    public static string EmptyInputPlease(string locale)
    {
        return Lookup("EmptyInputPlease", locale);
    }

    public static string ConnectionErrorPrefix(string locale)
    {
        return Lookup("ConnectionErrorPrefix", locale);
    }

    public static string ReconnectRetrying(string locale)
    {
        return Lookup("ReconnectRetrying", locale);
    }

    public static string WrongAnswerRetry(string locale)
    {
        return Lookup("WrongAnswerRetry", locale);
    }

    public static string WrongAnswerWithHint(string locale, string referenceSnippet)
    {
        string template = Lookup("WrongAnswerWithHint", locale);
        string snippet = referenceSnippet ?? "";
        try
        {
            return string.Format(template, snippet);
        }
        catch (FormatException)
        {
            return template + snippet;
        }
    }

    public static string UserPromptAfterCorrectAnswer(string locale)
    {
        return Lookup("UserPromptAfterCorrectAnswer", locale);
    }

    public static string UserPromptMissionComplete(string locale)
    {
        return Lookup("UserPromptMissionComplete", locale);
    }

    public static string UserPromptChesterWindowOpen(string locale)
    {
        return Lookup("UserPromptChesterWindowOpen", locale);
    }

    public static string UserPromptChesterParrotAskQuestionNow(string locale)
    {
        return Lookup("UserPromptChesterParrotAskQuestionNow", locale);
    }

    public static string TimerLowTimePrompt(string locale)
    {
        return Lookup("TimerLowTimePrompt", locale);
    }

    public static string EmptyPanelAdvancePrompt(string locale)
    {
        return Lookup("EmptyPanelAdvancePrompt", locale);
    }

    public static string EmptyPanelSkipPrompt(string locale)
    {
        return Lookup("EmptyPanelSkipPrompt", locale);
    }

    public static string ThinkingHoldDefault(string locale)
    {
        return Lookup("ThinkingHoldDefault", locale);
    }

    /// <summary>
    /// Inspector override wins when non-empty; otherwise locale default.
    /// </summary>
    public static string ResolveThinkingHoldMessage(string inspectorOverride, string locale)
    {
        if (!string.IsNullOrWhiteSpace(inspectorOverride))
            return inspectorOverride.Trim();
        return ThinkingHoldDefault(locale);
    }

    public static string ProgressEmptySection(string locale)
    {
        return Lookup("ProgressEmptySection", locale);
    }

    public static string ProgressAcquiredHeader(string locale)
    {
        return Lookup("ProgressAcquiredHeader", locale);
    }

    public static string ProgressGuideFooter(string locale)
    {
        return Lookup("ProgressGuideFooter", locale);
    }

    private static string Lookup(string stringId, string locale)
    {
        string normalized = CheshireLocaleResolver.NormalizeLocale(locale);
        ScenarioLocalizationTable table = GetTable(normalized);
        string value = table.Get(stringId);
        if (string.IsNullOrEmpty(value) || string.Equals(value, stringId, StringComparison.Ordinal))
            return HardcodedLookup(stringId, normalized);

        return value;
    }

    private static ScenarioLocalizationTable GetTable(string normalizedLocale)
    {
        if (TablesByLocale.TryGetValue(normalizedLocale, out ScenarioLocalizationTable cached))
            return cached;

        string csv = ResolveCsvText();
        ScenarioLocalizationTable table = ScenarioLocalizationTable.FromCsv(csv, normalizedLocale, "string_id");
        TablesByLocale[normalizedLocale] = table;
        return table;
    }

    private static string ResolveCsvText()
    {
        if (_csvTextOverride != null)
            return _csvTextOverride;

        TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
        return asset != null ? asset.text : string.Empty;
    }

    private static string HardcodedLookup(string stringId, string normalizedLocale)
    {
        switch (stringId)
        {
            case "EmptyInputPlease":
                return HardcodedEmptyInputPlease(normalizedLocale);
            case "ConnectionErrorPrefix":
                return HardcodedConnectionErrorPrefix(normalizedLocale);
            case "ReconnectRetrying":
                return HardcodedReconnectRetrying(normalizedLocale);
            case "WrongAnswerRetry":
                return HardcodedWrongAnswerRetry(normalizedLocale);
            case "WrongAnswerWithHint":
                return HardcodedWrongAnswerWithHint(normalizedLocale);
            case "UserPromptAfterCorrectAnswer":
                return HardcodedUserPromptAfterCorrectAnswer(normalizedLocale);
            case "UserPromptMissionComplete":
                return HardcodedUserPromptMissionComplete(normalizedLocale);
            case "UserPromptChesterWindowOpen":
                return HardcodedUserPromptChesterWindowOpen(normalizedLocale);
            case "UserPromptChesterParrotAskQuestionNow":
                return HardcodedUserPromptChesterParrotAskQuestionNow(normalizedLocale);
            case "TimerLowTimePrompt":
                return HardcodedTimerLowTimePrompt(normalizedLocale);
            case "EmptyPanelAdvancePrompt":
                return HardcodedEmptyPanelAdvancePrompt(normalizedLocale);
            case "EmptyPanelSkipPrompt":
                return HardcodedEmptyPanelSkipPrompt(normalizedLocale);
            case "ThinkingHoldDefault":
                return HardcodedThinkingHoldDefault(normalizedLocale);
            case "ProgressEmptySection":
                return HardcodedProgressEmptySection(normalizedLocale);
            case "ProgressAcquiredHeader":
                return HardcodedProgressAcquiredHeader(normalizedLocale);
            case "ProgressGuideFooter":
                return HardcodedProgressGuideFooter(normalizedLocale);
            default:
                return string.Empty;
        }
    }

    private static string HardcodedEmptyInputPlease(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "内容を入力してください。";
            case CheshireLocaleResolver.English:
                return "Please enter a message.";
            default:
                return "내용을 입력해 주세요.";
        }
    }

    private static string HardcodedConnectionErrorPrefix(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "接続エラー: ";
            case CheshireLocaleResolver.English:
                return "Connection error: ";
            default:
                return "연결 오류: ";
        }
    }

    private static string HardcodedReconnectRetrying(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "接続が不安定なため、再試行します…";
            case CheshireLocaleResolver.English:
                return "Connection unstable. Retrying…";
            default:
                return "연결이 원활하지 않아 다시 시도합니다…";
        }
    }

    private static string HardcodedWrongAnswerRetry(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "まだ正解じゃないよ。もう一度考えてみて！";
            case CheshireLocaleResolver.English:
                return "That's not the right answer yet. Think again!";
            default:
                return "아직 정답이 아니야. 다시 생각해 봐!";
        }
    }

    private static string HardcodedWrongAnswerWithHint(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "まだ正解じゃないよ。ヒント: {0}";
            case CheshireLocaleResolver.English:
                return "That's not the right answer yet. Hint: {0}";
            default:
                return "아직 정답이 아니야. 힌트: {0}";
        }
    }

    private static string HardcodedUserPromptAfterCorrectAnswer(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "[システム: いまのプレイヤーの答えはサーバーで正解と確定した。ごく短く励ましたあと、"
                    + "問題バンクに書かれた**次の**質問文を一字一句そのまま一行だけで言え。"
                    + "進行の数字・n/5・何問かは言うな。新しいJSONやツールは使うな。]";
            case CheshireLocaleResolver.English:
                return "[System: The player's answer was just confirmed correct by the server. "
                    + "Encourage briefly, then speak the **next** question sentence from the question bank "
                    + "verbatim as a single line only. Do not say progress numbers, n/5, or how many questions. "
                    + "Do not emit new JSON or tools.]";
            default:
                return "[시스템: 방금 플레이어 답은 서버에서 정답으로 확정되었다. 아주 짧게 격려한 뒤, "
                    + "문제 은행에 적힌 **다음** 질문 문장을 글자 그대로 한 줄로만 말해. "
                    + "진행 숫자·n/5·몇 문제는 말하지 마. 새 JSON이나 툴은 쓰지 마.]";
        }
    }

    private static string HardcodedUserPromptMissionComplete(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "[システム: プレイヤーは今日のクイズミッションをすべて完了した。"
                    + "短く褒めて締めの挨拶だけして。新しい問題は出すな。]";
            case CheshireLocaleResolver.English:
                return "[System: The player finished today's quiz mission. "
                    + "Praise briefly and give a closing greeting only. Do not ask a new question.]";
            default:
                return "[시스템: 플레이어가 오늘 퀴즈 미션을 모두 완료했다. "
                    + "짧게 칭찬하고 마무리 인사만 해. 새 문제는 내지 마.]";
        }
    }

    private static string HardcodedUserPromptChesterWindowOpen(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "[システム: プレイヤーがチェシャー(窓)との会話をちょうど始めた。ごく短く挨拶したあと、"
                    + "問題バンクに書かれた**いまの**クイズ質問文を一字一句そのまま一行だけで言え。"
                    + "進行の数字・n/5・何問かは言うな。新しいJSONやツールは使うな。]";
            case CheshireLocaleResolver.English:
                return "[System: The player just started talking with Cheshire (the window). "
                    + "Greet very briefly, then speak the **current** quiz question from the question bank "
                    + "verbatim as a single line only. Do not say progress numbers, n/5, or how many questions. "
                    + "Do not emit new JSON or tools.]";
            default:
                return "[시스템: 플레이어가 체셔(창)와 대화를 막 시작했다. 아주 짧게 인사만 한 뒤, "
                    + "문제 은행에 적힌 **지금** 퀴즈 질문 문장을 글자 그대로 한 줄로만 말해. "
                    + "진행 숫자·n/5·몇 문제는 말하지 마. 새 JSON이나 툴은 쓰지 마.]";
        }
    }

    private static string HardcodedUserPromptChesterParrotAskQuestionNow(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "[システム: プレイヤーがオウムを押してクイズを始めた。挨拶・導入・雑談・追加の文は禁止。"
                    + "問題バンクの**いまの**質問文だけを一字一句そのまま一行で言え。"
                    + "進行の数字・n/5・引用符・前置き禁止。新しいJSONやツールは使うな。]";
            case CheshireLocaleResolver.English:
                return "[System: The player just clicked the parrot to start the quiz. "
                    + "No greeting, intro, small talk, or extra sentences. "
                    + "Speak only the **current** question from the bank verbatim as one line. "
                    + "No progress numbers, n/5, quotes, or preamble. Do not emit new JSON or tools.]";
            default:
                return "[시스템: 플레이어가 앵무를 방금 눌러 퀴즈를 시작했다. 인사·도입·잡담·추가 문장 금지. "
                    + "문제 은행의 **지금** 질문 문장만 글자 그대로 한 줄로 말해. "
                    + "진행 숫자·n/5·따옴표·머리말 금지. 새 JSON이나 툴은 쓰지 마.]";
        }
    }

    private static string HardcodedTimerLowTimePrompt(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "[タイマー] 残りプレイ時間が少ないことだけ短く伝えて。新しいクイズ問題は出すな。";
            case CheshireLocaleResolver.English:
                return "[Timer] Briefly note that little play time remains. Do not ask a new quiz question.";
            default:
                return "[타이머] 남은 플레이 시간이 얼마 없다는 안내만 짧게 해 줘. 새 퀴즈 문제는 내지 마.";
        }
    }

    private static string HardcodedEmptyPanelAdvancePrompt(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "[プレイヤーが次の問題へ進みました。短く反応したあと、次のクイズ質問を一つだけ出して。]";
            case CheshireLocaleResolver.English:
                return "[The player moved on to the next question. React briefly, then ask only the next quiz question.]";
            default:
                return "[플레이어가 다음 문제로 넘어갔습니다. 짧게 반응한 뒤 다음 퀴즈 질문 한 가지만 출제해 줘.]";
        }
    }

    private static string HardcodedEmptyPanelSkipPrompt(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "[プレイヤーはこの問題をスキップします。短く反応したあと、次のクイズ質問を一つだけ出して。]";
            case CheshireLocaleResolver.English:
                return "[The player is skipping this question. React briefly, then ask only the next quiz question.]";
            default:
                return "[플레이어가 이 문제를 건너뜁니다. 짧게 반응한 뒤 다음 퀴즈 질문 한 가지만 출제해 줘.]";
        }
    }

    private static string HardcodedThinkingHoldDefault(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "うーん…いま考え中だよ。少し待っててね。";
            case CheshireLocaleResolver.English:
                return "Hmm… thinking now. Give me a moment.";
            default:
                return "음… 지금 생각하는 중이야. 조금만 기다려 줘.";
        }
    }

    private static string HardcodedProgressEmptySection(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[進行] まだ入手した手がかりアイテムはありません。";
            case CheshireLocaleResolver.English:
                return "\n\n[Progress] No clue items acquired yet.";
            default:
                return "\n\n[진행] 아직 획득한 단서 아이템이 없습니다.";
        }
    }

    private static string HardcodedProgressAcquiredHeader(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[進行] 入手アイテム: ";
            case CheshireLocaleResolver.English:
                return "\n\n[Progress] Acquired items: ";
            default:
                return "\n\n[진행] 획득 아이템: ";
        }
    }

    private static string HardcodedProgressGuideFooter(string locale)
    {
        switch (locale)
        {
            case CheshireLocaleResolver.Japanese:
                return "\n[進行案内] 上の一覧はプレイヤーが一度でも入手したアイテムです。"
                    + "インベントリで消費しても入手履歴は残ります。";
            case CheshireLocaleResolver.English:
                return "\n[Progress note] The list above is items the player has acquired at least once. "
                    + "Acquisition history remains even if they were consumed from the inventory.";
            default:
                return "\n[진행 안내] 위 목록은 플레이어가 한 번이라도 습득한 아이템입니다. "
                    + "인벤토리에서 소비했어도 습득 이력은 유지됩니다.";
        }
    }
}
