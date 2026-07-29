using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

[TestFixture]
public class TutorQuizStateTrackerTests
{
    private static TutorQuizStateTracker CreateTracker(
        Flowchart flowchart,
        bool debug = false)
    {
        return new TutorQuizStateTracker(
            flowchart,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: debug,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { });
    }

    private static Flowchart CreateFlowchartWithCorrectAnswerCount(int count)
    {
        var go = new GameObject("TutorQuizStateTrackerTests_Flowchart");
        Flowchart fc = go.AddComponent<Flowchart>();
        IntegerVariable iv = go.AddComponent<IntegerVariable>();
        iv.Key = FungusVariableKeys.CorrectAnswerCount;
        iv.Scope = VariableScope.Public;
        iv.Value = count;
        fc.Variables.Add(iv);
        return fc;
    }

    [TearDown]
    public void TearDownFlowcharts()
    {
        Flowchart[] charts = UnityEngine.Object.FindObjectsByType<Flowchart>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < charts.Length; i++)
        {
            Flowchart fc = charts[i];
            if (fc != null && fc.gameObject != null && fc.gameObject.name == "TutorQuizStateTrackerTests_Flowchart")
                UnityEngine.Object.DestroyImmediate(fc.gameObject);
        }
    }

    [Test]
    public void NullFlowchart_IsTutorQuizFinished_IsFalse_WhenCountWouldBeZero()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);

        Assert.AreEqual(0, tracker.ReadCorrectAnswerCount());
        Assert.IsFalse(tracker.IsTutorQuizFinished);
    }

    [Test]
    public void NullFlowchart_WriteCorrectAnswerCount_DoesNotThrow()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);
        Assert.DoesNotThrow(() => tracker.WriteCorrectAnswerCount(99));
        Assert.AreEqual(0, tracker.ReadCorrectAnswerCount());
    }

    [Test]
    public void NullFlowchart_ResolveCorrectAnswerCountKey_ReturnsCanonicalKey()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);
        Assert.AreEqual(FungusVariableKeys.CorrectAnswerCount, tracker.ResolveCorrectAnswerCountKey());
    }

    [Test]
    public void IsTutorQuizFinished_IsFalse_WhenCountBelowTarget()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(TutorQuizStateTracker.TutorQuizTargetCorrectCount - 1);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        Assert.IsFalse(tracker.IsTutorQuizFinished);
    }

    [Test]
    public void IsTutorQuizFinished_IsTrue_WhenCountAtTarget()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(TutorQuizStateTracker.TutorQuizTargetCorrectCount);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        Assert.IsTrue(tracker.IsTutorQuizFinished);
    }

    [Test]
    public void IsTutorQuizFinished_IsTrue_WhenCountAboveTarget()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(TutorQuizStateTracker.TutorQuizTargetCorrectCount + 2);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        Assert.IsTrue(tracker.IsTutorQuizFinished);
    }

    // ---------------------------------------------------------------
    //  IncrementCorrectAnswerCount
    // ---------------------------------------------------------------

    [Test]
    public void IncrementCorrectAnswerCount_IncrementsByOne()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        tracker.IncrementCorrectAnswerCount();

        Assert.AreEqual(1, tracker.ReadCorrectAnswerCount());
    }

    [Test]
    public void IncrementCorrectAnswerCount_DoesNotExceedTarget()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(TutorQuizStateTracker.TutorQuizTargetCorrectCount);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        tracker.IncrementCorrectAnswerCount();

        Assert.AreEqual(TutorQuizStateTracker.TutorQuizTargetCorrectCount, tracker.ReadCorrectAnswerCount());
    }

    [Test]
    public void IncrementCorrectAnswerCount_NullFlowchart_DoesNotThrow()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);
        Assert.DoesNotThrow(() => tracker.IncrementCorrectAnswerCount());
    }

    // ---------------------------------------------------------------
    //  ApplyQuizResult
    // ---------------------------------------------------------------

    [Test]
    public void ApplyQuizResult_Correct_IncrementsCount()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(2);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        tracker.ApplyQuizResult(isCorrect: true, quizComplete: false);

        Assert.AreEqual(3, tracker.ReadCorrectAnswerCount());
    }

    [Test]
    public void ApplyQuizResult_Correct_ResetsSkipOrderOffset()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        TutorQuizStateTracker tracker = CreateTracker(fc);
        tracker.SkipOrderOffset = 3;

        tracker.ApplyQuizResult(isCorrect: true, quizComplete: false);

        Assert.AreEqual(0, tracker.SkipOrderOffset);
    }

    [Test]
    public void ApplyQuizResult_Incorrect_DoesNotIncrementCount()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(2);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        tracker.ApplyQuizResult(isCorrect: false, quizComplete: false);

        Assert.AreEqual(2, tracker.ReadCorrectAnswerCount());
    }

    [Test]
    public void ApplyQuizResult_Incorrect_DoesNotResetSkipOrderOffset()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        TutorQuizStateTracker tracker = CreateTracker(fc);
        tracker.SkipOrderOffset = 2;

        tracker.ApplyQuizResult(isCorrect: false, quizComplete: false);

        Assert.AreEqual(2, tracker.SkipOrderOffset);
    }

    // ---------------------------------------------------------------
    //  TryFinalizeQuizSessionIfNeeded
    // ---------------------------------------------------------------

    [Test]
    public void TryFinalizeQuizSession_FiresEvent_WhenCountReachesTarget()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(
            TutorQuizStateTracker.TutorQuizTargetCorrectCount - 1);
        bool eventFired = false;
        var evt = new UnityEvent();
        evt.AddListener(() => eventFired = true);
        bool finalizeCalled = false;

        var tracker = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: evt,
            onQuizSessionFinalized: () => finalizeCalled = true);

        tracker.ApplyQuizResult(isCorrect: true, quizComplete: true);

        Assert.IsTrue(eventFired, "OnQuizCompletedEvent should fire when target count is reached.");
        Assert.IsTrue(finalizeCalled, "OnQuizSessionFinalized should be called.");
    }

    [Test]
    public void TryFinalizeQuizSession_DoesNotFireTwice()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(TutorQuizStateTracker.TutorQuizTargetCorrectCount);
        int fireCount = 0;
        var evt = new UnityEvent();
        evt.AddListener(() => fireCount++);

        var tracker = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: evt,
            onQuizSessionFinalized: () => { });

        tracker.TryFinalizeQuizSessionIfNeeded(modelSaidQuizComplete: true);
        tracker.TryFinalizeQuizSessionIfNeeded(modelSaidQuizComplete: true);

        Assert.AreEqual(1, fireCount, "Event should fire exactly once.");
    }

    [Test]
    public void TryFinalizeQuizSession_DoesNotFire_WhenCountBelowTarget()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(
            TutorQuizStateTracker.TutorQuizTargetCorrectCount - 2);
        bool eventFired = false;
        var evt = new UnityEvent();
        evt.AddListener(() => eventFired = true);

        var tracker = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: evt,
            onQuizSessionFinalized: () => { });

        tracker.TryFinalizeQuizSessionIfNeeded(modelSaidQuizComplete: true);

        Assert.IsFalse(eventFired, "Event should not fire when count is below target.");
    }

    // ---------------------------------------------------------------
    //  ResolveCorrectAnswerCountKey — typo fallback
    // ---------------------------------------------------------------

    [Test]
    public void ResolveCorrectAnswerCountKey_PrefersCanonicalKey()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        Assert.AreEqual(FungusVariableKeys.CorrectAnswerCount, tracker.ResolveCorrectAnswerCountKey());
    }

    [Test]
    public void ResolveCorrectAnswerCountKey_FallsBackToTypo_WhenCanonicalMissing()
    {
        var go = new GameObject("TutorQuizStateTrackerTests_Flowchart");
        Flowchart fc = go.AddComponent<Flowchart>();
        IntegerVariable iv = go.AddComponent<IntegerVariable>();
        iv.Key = TutorQuizStateTracker.VarCorrectAnswerCcTypo;
        iv.Scope = VariableScope.Public;
        iv.Value = 0;
        fc.Variables.Add(iv);

        TutorQuizStateTracker tracker = CreateTracker(fc);

        Assert.AreEqual(TutorQuizStateTracker.VarCorrectAnswerCcTypo, tracker.ResolveCorrectAnswerCountKey());
    }

    // ---------------------------------------------------------------
    //  WriteCorrectAnswerCount
    // ---------------------------------------------------------------

    [Test]
    public void WriteCorrectAnswerCount_WritesValue()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        TutorQuizStateTracker tracker = CreateTracker(fc);

        tracker.WriteCorrectAnswerCount(3);

        Assert.AreEqual(3, tracker.ReadCorrectAnswerCount());
    }

    // ---------------------------------------------------------------
    //  ResetTutorClickStateFlagsOnQuizComplete
    // ---------------------------------------------------------------

    [Test]
    public void ResetTutorClickStateFlags_ResetsWindowClickedAndIsClicked()
    {
        var go = new GameObject("TutorQuizStateTrackerTests_Flowchart");
        Flowchart fc = go.AddComponent<Flowchart>();

        IntegerVariable iv = go.AddComponent<IntegerVariable>();
        iv.Key = FungusVariableKeys.CorrectAnswerCount;
        iv.Scope = VariableScope.Public;
        iv.Value = 0;
        fc.Variables.Add(iv);

        BooleanVariable windowClicked = go.AddComponent<BooleanVariable>();
        windowClicked.Key = FungusVariableKeys.WindowClicked;
        windowClicked.Scope = VariableScope.Public;
        windowClicked.Value = true;
        fc.Variables.Add(windowClicked);

        BooleanVariable isClicked = go.AddComponent<BooleanVariable>();
        isClicked.Key = FungusVariableKeys.IsClicked;
        isClicked.Scope = VariableScope.Public;
        isClicked.Value = true;
        fc.Variables.Add(isClicked);

        TutorQuizStateTracker tracker = CreateTracker(fc);

        tracker.ResetTutorClickStateFlagsOnQuizComplete();

        Assert.IsFalse(fc.GetBooleanVariable(FungusVariableKeys.WindowClicked));
        Assert.IsFalse(fc.GetBooleanVariable(FungusVariableKeys.IsClicked));
    }

    [Test]
    public void ResetTutorClickStateFlags_NullFlowchart_DoesNotThrow()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);
        Assert.DoesNotThrow(() => tracker.ResetTutorClickStateFlagsOnQuizComplete());
    }

    // ---------------------------------------------------------------
    //  Property accessors
    // ---------------------------------------------------------------

    [Test]
    public void ExpectingQuizAnswer_DefaultsFalse_ThenSetTrue()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);

        Assert.IsFalse(tracker.ExpectingQuizAnswer);
        tracker.ExpectingQuizAnswer = true;
        Assert.IsTrue(tracker.ExpectingQuizAnswer);
    }

    [Test]
    public void AwaitingQuestionAdvance_DefaultsFalse_ThenSetTrue()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);

        Assert.IsFalse(tracker.AwaitingQuestionAdvance);
        tracker.AwaitingQuestionAdvance = true;
        Assert.IsTrue(tracker.AwaitingQuestionAdvance);
    }

    [Test]
    public void LastGradedWasCorrect_DefaultsFalse_ThenSetTrue()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);

        Assert.IsFalse(tracker.LastGradedWasCorrect);
        tracker.LastGradedWasCorrect = true;
        Assert.IsTrue(tracker.LastGradedWasCorrect);
    }

    [Test]
    public void ResolveCurrentQuestionIdFromOrderAsset_NullFlowchart_ReturnsNull()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);
        Assert.IsNull(tracker.ResolveCurrentQuestionIdFromOrderAsset());
    }

    // ---------------------------------------------------------------
    //  Session selector wiring
    // ---------------------------------------------------------------

    private static TutorQuizSessionSelector CreateSelector(params string[] ids)
    {
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            ids, seed: 1, sessionSize: ids.Length, out TutorQuizSessionSelector selector, out _));
        return selector;
    }

    [Test]
    public void ResolveCurrentQuestionId_WithSessionSelector_UsesSelectorIdsInsteadOfOrderAsset()
    {
        // The selector performs a seedable shuffle (design §4), so its SessionQuestionIds order is
        // not the input order — assert against the selector's own (shuffled) output rather than a
        // hard-coded pre-shuffle ID, which is what this test actually needs to verify: the tracker
        // delegates to the selector instead of walking the legacy TutorQuestionOrder asset.
        TutorQuizSessionSelector selector = CreateSelector("Q010", "Q020", "Q030", "Q040", "Q050");
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        var tracker = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: selector);

        Assert.AreEqual(selector.SessionQuestionIds[0], tracker.ResolveCurrentQuestionIdFromOrderAsset());
    }

    [Test]
    public void ResolveCurrentQuestionId_WithSessionSelector_AdvancesWithCorrectAnswerCount()
    {
        TutorQuizSessionSelector selector = CreateSelector("Q010", "Q020", "Q030", "Q040", "Q050");
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(2);
        var tracker = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: selector);

        Assert.AreEqual(selector.SessionQuestionIds[2], tracker.ResolveCurrentQuestionIdFromOrderAsset());
    }

    [Test]
    public void ResolveCurrentQuestionId_NullSelector_FallsBackToOrderAssetWalk()
    {
        Flowchart fc = CreateFlowchartWithCorrectAnswerCount(0);
        var tracker = new TutorQuizStateTracker(
            fc,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: null);

        // No order asset and no selector configured — legacy Resources fallback path, still returns
        // gracefully (null or a value) without throwing.
        Assert.DoesNotThrow(() => tracker.ResolveCurrentQuestionIdFromOrderAsset());
    }

    // ---------------------------------------------------------------
    //  HasInsufficientQuestions
    // ---------------------------------------------------------------

    [Test]
    public void HasInsufficientQuestions_DefaultsFalse()
    {
        TutorQuizStateTracker tracker = CreateTracker(flowchart: null);

        Assert.IsFalse(tracker.HasInsufficientQuestions);
        Assert.IsNull(tracker.InsufficientQuestionsError);
    }

    [Test]
    public void HasInsufficientQuestions_TrueWhenErrorProvided()
    {
        var tracker = new TutorQuizStateTracker(
            flowchart: null,
            tutorQuestionOrderAsset: null,
            debugQuizProgress: false,
            onQuizCompletedEvent: new UnityEvent(),
            onQuizSessionFinalized: () => { },
            sessionSelector: null,
            insufficientQuestionsError: "insufficient valid questions: need 5, have 3.");

        Assert.IsTrue(tracker.HasInsufficientQuestions);
        StringAssert.Contains("need 5", tracker.InsufficientQuestionsError);
    }
}
