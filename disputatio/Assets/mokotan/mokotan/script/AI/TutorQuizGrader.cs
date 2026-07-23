using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Callbacks that <see cref="TutorQuizGrader"/> needs from its MonoBehaviour host.
/// </summary>
internal interface IGraderHost
{
    void SayLine(string message, Action onComplete);
    IEnumerator GetGPTResponse(string userMessage);
    IEnumerator CoThinkingHoldIfSlow();
    Coroutine StartHostCoroutine(IEnumerator routine);
    void StopHostCoroutine(Coroutine coroutine);
    void NotifyHttpWaitStarted();
    void NotifyHttpWaitFinished();
    void AttachCertificateBypass(UnityWebRequest request);
    void HideTutorQuizUiAfterSessionComplete();
    bool? UseToolsOverrideForNextRequest { get; set; }

    /// <summary>
    /// Mirrors <see cref="IChatHttpCallbacks.IsRequestInProgress"/> — same request flag +
    /// <see cref="Godlotto.Interaction.InteractionInputGate"/> block/unblock. <see cref="TutorQuizGrader"/>
    /// sets this true/false in a try/finally around the <c>/tutor/grade</c> call so timeouts, parse
    /// failures, and early returns can never leave input stuck locked (design §4).
    /// </summary>
    bool IsRequestInProgress { get; set; }
}

/// <summary>
/// Handles the <c>/tutor/grade</c> HTTP API call and related response parsing.
/// Plain C# class — delegates coroutine execution to <see cref="IGraderHost"/>.
/// </summary>
internal sealed class TutorQuizGrader
{
    public const int MaxTutorGradeAnswerChars = 4000;

    private readonly string _tutorGradeUrlOverride;
    private readonly bool _debug;
    private readonly TutorQuizStateTracker _state;

    private string _chatUrl;

    /// <summary>EditMode seam: when set, replaces the real UnityWebRequest for the grade call.</summary>
    internal Func<ChatHttpAttemptOutcome> SimulateGradeAttempt;

    public TutorQuizGrader(
        string tutorGradeUrlOverride,
        bool debugQuizProgress,
        TutorQuizStateTracker stateTracker)
    {
        _tutorGradeUrlOverride = tutorGradeUrlOverride ?? "";
        _debug = debugQuizProgress;
        _state = stateTracker;
    }

    public void SetChatUrl(string chatUrl) => _chatUrl = chatUrl;

    /// <summary>
    /// Builds the JSON body for <c>POST /tutor/grade</c>. Includes canonical <c>locale</c>.
    /// </summary>
    public static Dictionary<string, object> BuildGradeRequestPayload(
        string questionId,
        string userAnswer,
        int correctCountBefore,
        int quizTarget,
        string locale)
    {
        return new Dictionary<string, object>
        {
            ["question_id"] = questionId ?? "",
            ["user_answer"] = userAnswer ?? "",
            ["correct_count_before"] = correctCountBefore,
            ["quiz_target"] = quizTarget,
            ["locale"] = CheshireLocaleResolver.NormalizeLocale(locale),
        };
    }

    /// <summary>
    /// <c>…/chat</c> → <c>…/tutor/grade</c>.
    /// 리버스 프록시·경로가 다르면 Inspector의 TutorGradeUrlOverride를 사용.
    /// </summary>
    public string ResolveTutorGradeUrl()
    {
        if (!string.IsNullOrWhiteSpace(_tutorGradeUrlOverride))
            return _tutorGradeUrlOverride.Trim();

        string chatUrl = _chatUrl;
        if (string.IsNullOrWhiteSpace(chatUrl))
            return "";

        string u = chatUrl.Trim().TrimEnd('/');
        const string gradeSuffix = "/tutor/grade";
        if (u.EndsWith(gradeSuffix, StringComparison.OrdinalIgnoreCase))
            return u;

        const string chatSuffix = "/chat";
        if (u.EndsWith(chatSuffix, StringComparison.OrdinalIgnoreCase))
            return u.Substring(0, u.Length - chatSuffix.Length) + gradeSuffix;

        if (u.Contains("/chat", StringComparison.OrdinalIgnoreCase))
            return u.Replace("/chat", gradeSuffix, StringComparison.OrdinalIgnoreCase);

        return u + gradeSuffix;
    }

    // ------------------------------------------------------------------
    //  Deterministic grading coroutine (caller StartCoroutines this)
    // ------------------------------------------------------------------

    public IEnumerator CoGradeThenReact(string playerAnswer, IGraderHost host)
    {
        _state.ExpectingQuizAnswer = false;
        string locale = CheshireLocaleResolver.ResolveCurrentLocale();
        bool answeredCorrectly = false;

        // Same finally pattern as ChatHttpClient.GetGPTResponse: HTTP timeout, parse failure, or any
        // early return below must still clear the request flag + InteractionInputGate (design §4) —
        // never leave the player stuck on "thinking". Released *before* the follow-up
        // host.GetGPTResponse call further down, which manages this same flag itself and would
        // no-op (yield break) if called while it were still true.
        host.IsRequestInProgress = true;
        try
        {
            if (_state.HasInsufficientQuestions)
            {
                Debug.LogError(
                    "[TutorQuiz] 유효한 문제가 5개 미만입니다: " + _state.InsufficientQuestionsError);
                bool errorDone = false;
                host.SayLine(CheshireUiStrings.TutorInsufficientQuestions(locale), () => errorDone = true);
                yield return new WaitUntil(() => errorDone);
                yield break;
            }

            string qid = _state.ResolveCurrentQuestionIdFromOrderAsset();
            if (string.IsNullOrWhiteSpace(qid))
            {
                Debug.LogError("[TutorQuiz] 채점할 question_id가 없습니다. " +
                               "TutorQuestionOrder·CorrectAnswerCount를 확인하세요.");
                _state.ExpectingQuizAnswer = true;
                yield break;
            }

            string url = ResolveTutorGradeUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                Debug.LogError("[TutorQuiz] 채점 URL이 비었습니다. " +
                               "BaseChatbot localServerUrl 또는 TutorGradeUrlOverride를 설정하세요.");
                _state.ExpectingQuizAnswer = true;
                yield break;
            }

            string answerForGrade = playerAnswer ?? "";
            if (answerForGrade.Length > MaxTutorGradeAnswerChars)
                answerForGrade = answerForGrade.Substring(0, MaxTutorGradeAnswerChars);
            int ccBefore = Mathf.Clamp(_state.ReadCorrectAnswerCount(), 0, 10_000);

            var payload = BuildGradeRequestPayload(
                qid,
                answerForGrade,
                ccBefore,
                TutorQuizStateTracker.TutorQuizTargetCorrectCount,
                locale);
            string jsonBody = JsonConvert.SerializeObject(payload);

            ChatHttpAttemptOutcome outcome;
            host.NotifyHttpWaitStarted();
            try
            {
                if (SimulateGradeAttempt != null)
                {
                    outcome = SimulateGradeAttempt();
                    yield return null;
                }
                else
                {
                    using (var req = new UnityWebRequest(url, "POST"))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        req.downloadHandler = new DownloadHandlerBuffer();
                        req.SetRequestHeader("Content-Type", "application/json");
                        host.AttachCertificateBypass(req);
                        req.timeout = 30;

                        yield return req.SendWebRequest();

                        string body = req.downloadHandler != null ? req.downloadHandler.text : "";
                        outcome = new ChatHttpAttemptOutcome(req.result, req.responseCode, req.error ?? "", body);
                    }
                }
            }
            finally
            {
                host.NotifyHttpWaitFinished();
            }

            if (outcome.Result != UnityWebRequest.Result.Success)
            {
                string body = outcome.Body;
                if (body != null && body.Length > 200)
                    body = body.Substring(0, 200) + "…";
                Debug.LogError(
                    "[TutorQuiz] /tutor/grade 실패: " + outcome.Error +
                    " | HTTP " + outcome.ResponseCode +
                    " | URL: " + url +
                    " | 서버에 POST /tutor/grade 라우트가 배포됐는지 확인하세요." +
                    " (로그에 404면 구버전 백엔드일 수 있음)" +
                    (string.IsNullOrEmpty(body) ? "" : " | body: " + body));
                _state.ExpectingQuizAnswer = true;
                yield break;
            }

            TutorGradeResponseDto grade;
            try
            {
                grade = JsonConvert.DeserializeObject<TutorGradeResponseDto>(outcome.Body);
            }
            catch (Exception e)
            {
                Debug.LogError("[TutorQuiz] 채점 응답 파싱 실패: " + e.Message);
                _state.ExpectingQuizAnswer = true;
                yield break;
            }

            if (grade == null)
            {
                _state.ExpectingQuizAnswer = true;
                yield break;
            }

            if (_debug)
                GameLog.Log(
                    $"[TutorQuiz] /tutor/grade: qid={grade.question_id}, " +
                    $"ok={grade.is_correct}, unknown={grade.unknown_question}");

            if (!grade.is_correct || grade.unknown_question)
            {
                string hint = string.IsNullOrWhiteSpace(grade.reference_snippet)
                    ? CheshireUiStrings.WrongAnswerRetry(locale)
                    : CheshireUiStrings.WrongAnswerWithHint(locale, grade.reference_snippet);
                bool hintDone = false;
                host.SayLine(hint, () => hintDone = true);
                yield return new WaitUntil(() => hintDone);
                _state.LastGradedWasCorrect = false;
                _state.LastEmbeddedQuizComplete = false;
                _state.AwaitingQuestionAdvance = true;
                _state.ExpectingQuizAnswer = true;
                yield break;
            }

            _state.ApplyQuizResult(true, false);
            _state.LastGradedWasCorrect = true;
            answeredCorrectly = true;
        }
        finally
        {
            host.IsRequestInProgress = false;
        }

        // Correct-answer follow-up runs *outside* the grade request-in-progress scope above —
        // host.GetGPTResponse manages IsRequestInProgress itself (ChatHttpClient) and must see it
        // false to actually run instead of no-op'ing.
        if (!answeredCorrectly)
            yield break;

        host.UseToolsOverrideForNextRequest = false;
        Coroutine thinkingHold = host.StartHostCoroutine(host.CoThinkingHoldIfSlow());

        if (_state.IsTutorQuizFinished)
        {
            yield return host.StartHostCoroutine(
                host.GetGPTResponse(CheshireUiStrings.UserPromptMissionComplete(locale)));
            host.HideTutorQuizUiAfterSessionComplete();
        }
        else
        {
            yield return host.StartHostCoroutine(
                host.GetGPTResponse(CheshireUiStrings.UserPromptAfterCorrectAnswer(locale)));
        }

        if (thinkingHold != null)
            host.StopHostCoroutine(thinkingHold);
    }

    // ------------------------------------------------------------------
    //  Static response-parsing helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// TutorRoomPrompt가 요구하는 응답 끝의 is_correct / quiz_complete JSON을 제거해
    /// 대사만 남기고, 값은 out으로 반환합니다.
    /// </summary>
    public static string PrepareTutorDisplayAndQuizMetadata(
        string raw,
        out bool hasMetadata,
        out bool isCorrect,
        out bool quizComplete)
    {
        hasMetadata = false;
        isCorrect = false;
        quizComplete = false;
        if (string.IsNullOrEmpty(raw))
            return raw;

        int marker = raw.LastIndexOf("\"is_correct\"", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
            return raw;

        int braceStart = raw.LastIndexOf('{', marker);
        if (braceStart < 0)
            return raw;

        if (!TryExtractBalancedJsonObject(raw, braceStart, out string jsonCandidate))
            return raw;

        jsonCandidate = jsonCandidate.Trim();
        try
        {
            JObject obj = JObject.Parse(jsonCandidate);
            JToken ic = obj["is_correct"];
            JToken qc = obj["quiz_complete"];
            if (ic == null || qc == null)
                return raw;

            hasMetadata = true;
            isCorrect = ic.Type == JTokenType.Boolean ? ic.Value<bool>() : bool.Parse(ic.ToString());
            quizComplete = qc.Type == JTokenType.Boolean ? qc.Value<bool>() : bool.Parse(qc.ToString());
            return raw.Substring(0, braceStart).TrimEnd();
        }
        catch (Exception e)
        {
            GameLog.LogWarning($"[TutorChatbot] 튜터 응답 끝 JSON 파싱 실패: {e.Message}");
            return raw;
        }
    }

    /// <summary>
    /// 시작 <c>{</c>부터 짝이 맞는 <c>}</c>까지만 잘라 JSON으로 파싱합니다.
    /// </summary>
    public static bool TryExtractBalancedJsonObject(string raw, int openBrace, out string json)
    {
        json = null;
        if (openBrace < 0 || openBrace >= raw.Length || raw[openBrace] != '{')
            return false;

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = openBrace; i < raw.Length; i++)
        {
            char c = raw[i];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (inString)
            {
                if (c == '\\')
                    escape = true;
                else if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    json = raw.Substring(openBrace, i - openBrace + 1);
                    return true;
                }
            }
        }

        return false;
    }

    public static bool ShouldOfferPanelAdvanceAfterTurn(
        IReadOnlyList<OpenAIMessage> history,
        bool hasEmbeddedQuizJson,
        bool embeddedQuizComplete)
    {
        if (!hasEmbeddedQuizJson || embeddedQuizComplete)
            return false;
        if (history == null || history.Count < 2)
            return false;
        OpenAIMessage prev = history[history.Count - 2];
        if (prev.role != "user")
            return false;
        string c = prev.content ?? string.Empty;
        if (c.StartsWith("[", StringComparison.Ordinal))
            return false;
        return true;
    }

    // ------------------------------------------------------------------
    //  Tool-call post-processing (quiz tools ignored; emote delegated)
    // ------------------------------------------------------------------

    /// <summary>정답 처리는 <see cref="CoGradeThenReact"/>만 담당. LLM의 update_quiz는 무시.</summary>
    public void ProcessTutorNonQuizToolsOnly(
        List<FunctionCallData> functionCalls,
        Action<Dictionary<string, object>> handleEmote)
    {
        if (functionCalls == null)
            return;

        foreach (var fc in functionCalls)
        {
            switch (fc.name)
            {
                case "update_quiz":
                    if (_debug)
                        GameLog.Log("[TutorQuiz] update_quiz 툴 호출은 무시합니다(채점은 /tutor/grade).");
                    break;
                case "emote":
                    handleEmote?.Invoke(fc.arguments);
                    break;
                default:
                    GameLog.Log($"Unhandled function call: {fc.name}");
                    break;
            }
        }
    }
}
