using System;
using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages tutor quiz progress state via Fungus variables.
/// Plain C# class — receives all dependencies via constructor.
/// </summary>
internal sealed class TutorQuizStateTracker
{
    /// <summary>레거시/오타 Fungus 변수명.</summary>
    public const string VarCorrectAnswerCcTypo = "CorrectAnswerCc";
    public const int TutorQuizTargetCorrectCount = 5;

    private readonly Flowchart _flowchart;
    private readonly TextAsset _questionOrderAsset;
    private readonly bool _debug;
    private readonly UnityEvent _onQuizCompletedEvent;
    private readonly Action _onQuizSessionFinalized;
    private readonly TutorQuizSessionSelector _sessionSelector;
    private readonly string _insufficientQuestionsError;

    private int _skipOrderOffset;
    private bool _quizCompletionEventFired;
    private bool _expectingQuizAnswer;
    private bool _awaitingQuestionAdvance;
    private bool _lastGradedWasCorrect;
    private bool _lastEmbeddedQuizComplete;

    public TutorQuizStateTracker(
        Flowchart flowchart,
        TextAsset tutorQuestionOrderAsset,
        bool debugQuizProgress,
        UnityEvent onQuizCompletedEvent,
        Action onQuizSessionFinalized,
        TutorQuizSessionSelector sessionSelector = null,
        string insufficientQuestionsError = null)
    {
        _flowchart = flowchart;
        _questionOrderAsset = tutorQuestionOrderAsset;
        _debug = debugQuizProgress;
        _onQuizCompletedEvent = onQuizCompletedEvent;
        _onQuizSessionFinalized = onQuizSessionFinalized;
        _sessionSelector = sessionSelector;
        _insufficientQuestionsError = insufficientQuestionsError;
    }

    /// <summary>
    /// True when the session could not be built with enough unique valid questions
    /// (design §4: fewer than 5 valid questions → localized error, input unlocked — never stuck "thinking").
    /// </summary>
    public bool HasInsufficientQuestions => !string.IsNullOrEmpty(_insufficientQuestionsError);

    /// <summary>Diagnostic detail behind <see cref="HasInsufficientQuestions"/> (file/row context); null when healthy.</summary>
    public string InsufficientQuestionsError => _insufficientQuestionsError;

    public bool ExpectingQuizAnswer
    {
        get => _expectingQuizAnswer;
        set => _expectingQuizAnswer = value;
    }

    public bool AwaitingQuestionAdvance
    {
        get => _awaitingQuestionAdvance;
        set => _awaitingQuestionAdvance = value;
    }

    public bool LastGradedWasCorrect
    {
        get => _lastGradedWasCorrect;
        set => _lastGradedWasCorrect = value;
    }

    public bool LastEmbeddedQuizComplete
    {
        get => _lastEmbeddedQuizComplete;
        set => _lastEmbeddedQuizComplete = value;
    }

    public int SkipOrderOffset
    {
        get => _skipOrderOffset;
        set => _skipOrderOffset = value;
    }

    public bool IsTutorQuizFinished =>
        _quizCompletionEventFired || ReadCorrectAnswerCount() >= TutorQuizTargetCorrectCount;

    public int ReadCorrectAnswerCount()
    {
        return _flowchart != null ? _flowchart.GetIntegerVariable(ResolveCorrectAnswerCountKey()) : 0;
    }

    public void WriteCorrectAnswerCount(int value)
    {
        if (_flowchart == null)
            return;
        string key = ResolveCorrectAnswerCountKey();
        int prev = _flowchart.GetIntegerVariable(key);
        _flowchart.SetIntegerVariable(key, value);
        if (_debug)
            GameLog.Log($"[TutorQuiz] CorrectAnswerCount 쓰기: key={key}, {prev} → {value}");
    }

    /// <summary>정답 수 Integer 변수 키. <see cref="FungusVariableKeys.CorrectAnswerCount"/> 우선, 없으면 <see cref="VarCorrectAnswerCcTypo"/>.</summary>
    public string ResolveCorrectAnswerCountKey()
    {
        if (_flowchart == null)
            return FungusVariableKeys.CorrectAnswerCount;
        if (_flowchart.GetVariable(FungusVariableKeys.CorrectAnswerCount) is IntegerVariable)
            return FungusVariableKeys.CorrectAnswerCount;
        if (_flowchart.GetVariable(VarCorrectAnswerCcTypo) is IntegerVariable)
            return VarCorrectAnswerCcTypo;
        return FungusVariableKeys.CorrectAnswerCount;
    }

    /// <summary>
    /// CorrectAnswerCount(+ 건너뛰기 오프셋) 인덱스의 현재 세션 question_id.
    /// <see cref="TutorQuizSessionSelector"/>가 있으면 그 고정 5문제를 우선 사용하고,
    /// 없으면(레거시) <c>TutorQuestionOrder.txt</c> 순차 워크로 폴백합니다.
    /// </summary>
    public string ResolveCurrentQuestionIdFromOrderAsset()
    {
        if (_flowchart == null)
            return null;

        if (_sessionSelector != null)
        {
            IReadOnlyList<string> sessionIds = _sessionSelector.SessionQuestionIds;
            if (sessionIds == null || sessionIds.Count == 0)
                return null;
            int selectorIdx = Mathf.Clamp(ReadCorrectAnswerCount() + _skipOrderOffset, 0, sessionIds.Count - 1);
            return sessionIds[selectorIdx];
        }

        TextAsset asset = _questionOrderAsset != null
            ? _questionOrderAsset
            : Resources.Load<TextAsset>("TutorQuestionOrder");
        if (asset == null)
            return null;

        string[] rawLines = asset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var ids = new List<string>();
        foreach (string line in rawLines)
        {
            string t = line.Trim();
            if (t.Length == 0 || t.StartsWith("#", StringComparison.Ordinal))
                continue;
            ids.Add(t);
        }

        if (ids.Count == 0)
            return null;

        int idx = Mathf.Clamp(ReadCorrectAnswerCount() + _skipOrderOffset, 0, ids.Count - 1);
        return ids[idx];
    }

    public void IncrementCorrectAnswerCount()
    {
        if (_flowchart == null)
            return;

        string key = ResolveCorrectAnswerCountKey();
        int currentCount = _flowchart.GetIntegerVariable(key);
        if (currentCount >= TutorQuizTargetCorrectCount)
        {
            if (_debug)
                GameLog.Log(
                    $"[TutorQuiz] IncrementCorrectAnswerCount: key={key}, 이미 {currentCount} " +
                    $"(상한 {TutorQuizTargetCorrectCount}) — 증가 안 함");
            return;
        }

        WriteCorrectAnswerCount(currentCount + 1);
    }

    public void ApplyQuizResult(bool isCorrect, bool quizComplete)
    {
        int before = ReadCorrectAnswerCount();
        if (_debug)
            GameLog.Log(
                $"[TutorQuiz] ApplyQuizResult: isCorrect={isCorrect}, quizComplete={quizComplete}, " +
                $"CorrectAnswerCount(before)={before}, _skipOrderOffset(before)={_skipOrderOffset}");

        if (isCorrect)
        {
            _skipOrderOffset = 0;
            IncrementCorrectAnswerCount();
        }

        if (_debug && isCorrect)
            GameLog.Log(
                $"[TutorQuiz] ApplyQuizResult: 정답 처리 후 CorrectAnswerCount={ReadCorrectAnswerCount()}, " +
                $"_skipOrderOffset={_skipOrderOffset}");

        TryFinalizeQuizSessionIfNeeded(quizComplete);
    }

    /// <summary>
    /// 게임 로직상 미션 완료는 Fungus 정답 누적(<see cref="TutorQuizTargetCorrectCount"/>)만 신뢰합니다.
    /// 모델의 <c>quiz_complete</c>는 UI·내러티브 힌트용이며, 조기 true여도 이벤트는 카운트 미달 시 발생하지 않습니다.
    /// </summary>
    public void TryFinalizeQuizSessionIfNeeded(bool modelSaidQuizComplete)
    {
        int n = ReadCorrectAnswerCount();
        if (modelSaidQuizComplete && n < TutorQuizTargetCorrectCount && _debug)
        {
            GameLog.LogWarning(
                $"[TutorQuiz] 모델이 quiz_complete=true였으나 CorrectAnswerCount={n} — " +
                $"OnQuizCompletedEvent는 정답 {TutorQuizTargetCorrectCount}회 도달 시에만 발생합니다.");
        }

        if (_quizCompletionEventFired || n < TutorQuizTargetCorrectCount)
            return;

        _quizCompletionEventFired = true;
        ResetTutorClickStateFlagsOnQuizComplete();
        _onQuizCompletedEvent?.Invoke();
        _onQuizSessionFinalized?.Invoke();
    }

    public void ResetTutorClickStateFlagsOnQuizComplete()
    {
        if (_flowchart == null)
            return;

        if (_flowchart.GetVariable(FungusVariableKeys.WindowClicked) is BooleanVariable)
            _flowchart.SetBooleanVariable(FungusVariableKeys.WindowClicked, false);

        if (_flowchart.GetVariable(FungusVariableKeys.IsClicked) is BooleanVariable)
            _flowchart.SetBooleanVariable(FungusVariableKeys.IsClicked, false);
    }
}
