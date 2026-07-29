using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fungus;
using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.TestTools;

/// <summary>
/// Drives <see cref="TutorQuizGrader.CoGradeThenReact"/> synchronously via a fake <see cref="IGraderHost"/>.
/// Covers design §4: the same try/finally request-flag + <see cref="InteractionInputGate"/> pattern as
/// <see cref="ChatHttpClient"/>, and the &lt;5 valid questions localized-error path.
/// </summary>
[TestFixture]
public class TutorQuizGraderCoroutineTests
{
    [TearDown]
    public void TearDown()
    {
        InteractionInputGate.ResetForTests();
    }

    private static Flowchart CreateFlowchartWithCorrectAnswerCount(int count)
    {
        var go = new GameObject("TutorQuizGraderCoroutineTests_Flowchart");
        Flowchart fc = go.AddComponent<Flowchart>();
        IntegerVariable iv = go.AddComponent<IntegerVariable>();
        iv.Key = FungusVariableKeys.CorrectAnswerCount;
        iv.Scope = VariableScope.Public;
        iv.Value = count;
        fc.Variables.Add(iv);
        return fc;
    }

    [TearDown]
    public void DestroyFlowcharts()
    {
        Flowchart[] charts = UnityEngine.Object.FindObjectsByType<Flowchart>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < charts.Length; i++)
        {
            Flowchart fc = charts[i];
            if (fc != null && fc.gameObject != null
                && fc.gameObject.name == "TutorQuizGraderCoroutineTests_Flowchart")
                UnityEngine.Object.DestroyImmediate(fc.gameObject);
        }
    }

    // ---------------------------------------------------------------
    //  Insufficient questions (<5 valid) → localized error, input unlocked
    // ---------------------------------------------------------------

    [Test]
    public void CoGradeThenReact_InsufficientQuestions_SaysLocalizedErrorAndUnlocksInput()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        var state = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: null,
            insufficientQuestionsError: "insufficient valid questions: need 5, have 3.");
        var grader = new TutorQuizGrader("http://test.local/tutor/grade", debugQuizProgress: false, state);
        var host = new FakeGraderHost();

        LogAssert.Expect(LogType.Error, new Regex("유효한 문제가 5개 미만"));

        DrainEnumerator(grader.CoGradeThenReact("42", host));

        string expectedLocale = CheshireLocaleResolver.ResolveCurrentLocale();
        Assert.AreEqual(1, host.SaidLines.Count);
        Assert.AreEqual(CheshireUiStrings.TutorInsufficientQuestions(expectedLocale), host.SaidLines[0]);
        Assert.IsFalse(host.IsRequestInProgress, "Request flag must be cleared even on the insufficient-questions path.");
        Assert.IsFalse(InteractionInputGate.IsBlocked, "Input gate must be unblocked — never stuck thinking.");
        CollectionAssert.AreEqual(new[] { true, false }, host.RequestInProgressTransitions);
    }

    // ---------------------------------------------------------------
    //  HTTP failure (timeout / connection error) → flag + gate cleared
    // ---------------------------------------------------------------

    [Test]
    public void CoGradeThenReact_HttpTimeout_ClearsRequestFlagAndGate()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            new[] { "Q001", "Q002", "Q003", "Q004", "Q005" },
            seed: 1,
            sessionSize: 5,
            out TutorQuizSessionSelector selector,
            out _));
        var state = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: selector);
        var grader = new TutorQuizGrader("http://test.local/tutor/grade", debugQuizProgress: false, state)
        {
            SimulateGradeAttempt = () => new ChatHttpAttemptOutcome(
                UnityWebRequest.Result.ConnectionError,
                responseCode: 0,
                error: "Request timeout",
                body: ""),
        };
        var host = new FakeGraderHost();

        LogAssert.Expect(LogType.Error, new Regex("/tutor/grade 실패"));

        DrainEnumerator(grader.CoGradeThenReact("42", host));

        Assert.IsFalse(host.IsRequestInProgress, "Request flag must be cleared after an HTTP timeout.");
        Assert.IsFalse(InteractionInputGate.IsBlocked, "Input gate must be unblocked after an HTTP timeout.");
        CollectionAssert.AreEqual(new[] { true, false }, host.RequestInProgressTransitions);
        Assert.IsTrue(state.ExpectingQuizAnswer, "Player should be able to retry after a transport failure.");
        Assert.AreEqual(1, host.WaitStartedCount);
        Assert.AreEqual(1, host.WaitFinishedCount);
    }

    // ---------------------------------------------------------------
    //  Malformed JSON response → flag + gate cleared
    // ---------------------------------------------------------------

    [Test]
    public void CoGradeThenReact_MalformedJsonResponse_ClearsRequestFlagAndGate()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            new[] { "Q001", "Q002", "Q003", "Q004", "Q005" },
            seed: 1,
            sessionSize: 5,
            out TutorQuizSessionSelector selector,
            out _));
        var state = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: selector);
        var grader = new TutorQuizGrader("http://test.local/tutor/grade", debugQuizProgress: false, state)
        {
            SimulateGradeAttempt = () => new ChatHttpAttemptOutcome(
                UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "{not valid json"),
        };
        var host = new FakeGraderHost();

        LogAssert.Expect(LogType.Error, new Regex("채점 응답 파싱 실패"));

        DrainEnumerator(grader.CoGradeThenReact("42", host));

        Assert.IsFalse(host.IsRequestInProgress, "Request flag must be cleared after a parse failure.");
        Assert.IsFalse(InteractionInputGate.IsBlocked, "Input gate must be unblocked after a parse failure.");
        CollectionAssert.AreEqual(new[] { true, false }, host.RequestInProgressTransitions);
        Assert.IsTrue(state.ExpectingQuizAnswer);
    }

    // ---------------------------------------------------------------
    //  Correct answer → flag + gate cleared once the whole flow finishes
    // ---------------------------------------------------------------

    [Test]
    public void CoGradeThenReact_CorrectAnswer_ClearsRequestFlagAfterFollowUpTurn()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            new[] { "Q001", "Q002", "Q003", "Q004", "Q005" },
            seed: 1,
            sessionSize: 5,
            out TutorQuizSessionSelector selector,
            out _));
        var state = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: selector);
        var grader = new TutorQuizGrader("http://test.local/tutor/grade", debugQuizProgress: false, state)
        {
            SimulateGradeAttempt = () => new ChatHttpAttemptOutcome(
                UnityWebRequest.Result.Success,
                responseCode: 200,
                error: "",
                body: "{\"is_correct\":true,\"question_id\":\"" + selector.GetQuestionIdAt(0) + "\"," +
                      "\"reference_snippet\":\"\",\"quiz_complete_after\":false,\"unknown_question\":false}"),
        };
        var host = new FakeGraderHost();

        DrainEnumerator(grader.CoGradeThenReact("correct answer", host));

        Assert.IsFalse(host.IsRequestInProgress);
        Assert.IsFalse(InteractionInputGate.IsBlocked);
        Assert.AreEqual(1, state.ReadCorrectAnswerCount());
        Assert.IsTrue(host.GptResponsePrompts.Count > 0, "Correct-answer path should trigger a follow-up AI turn.");
    }

    // ---------------------------------------------------------------
    //  Enumerator driver (same shape as ChatHttpClientTests.DrainEnumerator)
    // ---------------------------------------------------------------

    private static void DrainEnumerator(IEnumerator routine, int maxSteps = 500)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int steps = 0;
        while (stack.Count > 0)
        {
            if (steps++ > maxSteps)
                throw new InvalidOperationException("Enumerator did not complete within step budget.");

            IEnumerator current = stack.Peek();
            bool moved;
            try
            {
                moved = current.MoveNext();
            }
            catch
            {
                while (stack.Count > 0)
                    (stack.Pop() as IDisposable)?.Dispose();
                throw;
            }

            if (!moved)
            {
                stack.Pop();
                continue;
            }

            if (current.Current is IEnumerator nested)
                stack.Push(nested);
        }
    }

    // ---------------------------------------------------------------
    //  Fake IGraderHost — mirrors BaseChatbot's real IChatHttpCallbacks.IsRequestInProgress wiring
    //  (protected field + InteractionInputGate.Block/Unblock) so gate assertions are meaningful.
    // ---------------------------------------------------------------

    private sealed class FakeGraderHost : IGraderHost
    {
        private const string GateReason = "FakeGraderHost:test_request";
        private bool _isRequestInProgress;

        public List<string> SaidLines { get; } = new List<string>();
        public List<string> GptResponsePrompts { get; } = new List<string>();
        public List<bool> RequestInProgressTransitions { get; } = new List<bool>();
        public int WaitStartedCount { get; private set; }
        public int WaitFinishedCount { get; private set; }
        public bool HideCalled { get; private set; }

        public bool? UseToolsOverrideForNextRequest { get; set; }

        public bool IsRequestInProgress
        {
            get => _isRequestInProgress;
            set
            {
                _isRequestInProgress = value;
                RequestInProgressTransitions.Add(value);
                if (value)
                    InteractionInputGate.Block(GateReason);
                else
                    InteractionInputGate.Unblock(GateReason);
            }
        }

        public void SayLine(string message, Action onComplete)
        {
            SaidLines.Add(message ?? "");
            onComplete?.Invoke();
        }

        public IEnumerator GetGPTResponse(string userMessage)
        {
            GptResponsePrompts.Add(userMessage ?? "");
            yield break;
        }

        public IEnumerator CoThinkingHoldIfSlow()
        {
            yield break;
        }

        public Coroutine StartHostCoroutine(IEnumerator routine)
        {
            if (routine != null)
                DrainEnumerator(routine);
            return null;
        }

        public void StopHostCoroutine(Coroutine coroutine) { }

        public void NotifyHttpWaitStarted() => WaitStartedCount++;

        public void NotifyHttpWaitFinished() => WaitFinishedCount++;

        public void AttachCertificateBypass(UnityWebRequest request) { }

        public void HideTutorQuizUiAfterSessionComplete() => HideCalled = true;
    }
}
