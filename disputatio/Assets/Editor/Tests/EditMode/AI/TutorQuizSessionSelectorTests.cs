using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class TutorQuizSessionSelectorTests
{
    private static readonly List<string> EightyIds =
        Enumerable.Range(1, 80).Select(n => $"Q{n:D3}").ToList();

    [Test]
    public void TrySelectSession_SameSeed_ProducesSameFiveIds()
    {
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 42, sessionSize: 5, out var selectorA, out _));
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 42, sessionSize: 5, out var selectorB, out _));

        CollectionAssert.AreEqual(selectorA.SessionQuestionIds, selectorB.SessionQuestionIds);
    }

    [Test]
    public void TrySelectSession_ReturnsExactlyRequestedCount()
    {
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 1, sessionSize: 5, out var selector, out _));

        Assert.AreEqual(5, selector.SessionQuestionIds.Count);
    }

    [Test]
    public void TrySelectSession_SessionIdsAreUnique()
    {
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 7, sessionSize: 5, out var selector, out _));

        var distinct = new HashSet<string>(selector.SessionQuestionIds);
        Assert.AreEqual(selector.SessionQuestionIds.Count, distinct.Count);
    }

    [Test]
    public void TrySelectSession_DifferentSeeds_CanProduceDifferentSessions()
    {
        bool foundDifference = false;
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 1, sessionSize: 5, out var baseline, out _));

        for (int seed = 2; seed < 50 && !foundDifference; seed++)
        {
            Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
                EightyIds, seed: seed, sessionSize: 5, out var candidate, out _));
            if (!candidate.SessionQuestionIds.SequenceEqual(baseline.SessionQuestionIds))
                foundDifference = true;
        }

        Assert.IsTrue(foundDifference, "Expected at least one differing session across 48 seeds.");
    }

    [Test]
    public void TrySelectSession_FewerThanSessionSizeUniqueIds_Fails()
    {
        var fourIds = new List<string> { "Q001", "Q002", "Q003", "Q004" };

        bool ok = TutorQuizSessionSelector.TrySelectSession(
            fourIds, seed: 1, sessionSize: 5, out var selector, out string error);

        Assert.IsFalse(ok);
        Assert.IsNull(selector);
        StringAssert.Contains("insufficient", error);
    }

    [Test]
    public void TrySelectSession_DuplicateIdsCollapseBeforeCounting_Fails()
    {
        var idsWithDuplicates = new List<string> { "Q001", "Q001", "Q002", "Q003" };

        bool ok = TutorQuizSessionSelector.TrySelectSession(
            idsWithDuplicates, seed: 1, sessionSize: 5, out var selector, out string error);

        Assert.IsFalse(ok);
        Assert.IsNull(selector);
    }

    [Test]
    public void TrySelectSession_ExactlySessionSizeUniqueIds_SucceedsAndUsesAll()
    {
        var fiveIds = new List<string> { "Q001", "Q002", "Q003", "Q004", "Q005" };

        bool ok = TutorQuizSessionSelector.TrySelectSession(
            fiveIds, seed: 3, sessionSize: 5, out var selector, out _);

        Assert.IsTrue(ok);
        CollectionAssert.AreEquivalent(fiveIds, selector.SessionQuestionIds);
    }

    [Test]
    public void TrySelectSession_NullIds_Fails()
    {
        bool ok = TutorQuizSessionSelector.TrySelectSession(
            null, seed: 1, sessionSize: 5, out var selector, out string error);

        Assert.IsFalse(ok);
        Assert.IsNull(selector);
        StringAssert.Contains("insufficient", error);
    }

    [Test]
    public void TrySelectSession_ZeroSessionSize_Fails()
    {
        bool ok = TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 1, sessionSize: 0, out var selector, out string error);

        Assert.IsFalse(ok);
        Assert.IsNull(selector);
    }

    [Test]
    public void GetQuestionIdAt_WithinRange_ReturnsCorrectId()
    {
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 5, sessionSize: 5, out var selector, out _));

        for (int i = 0; i < 5; i++)
            Assert.AreEqual(selector.SessionQuestionIds[i], selector.GetQuestionIdAt(i));
    }

    [Test]
    public void GetQuestionIdAt_BeyondRange_ClampsToLastQuestion()
    {
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 5, sessionSize: 5, out var selector, out _));

        Assert.AreEqual(selector.SessionQuestionIds[4], selector.GetQuestionIdAt(99));
    }

    [Test]
    public void GetQuestionIdAt_NegativeIndex_ClampsToFirstQuestion()
    {
        Assert.IsTrue(TutorQuizSessionSelector.TrySelectSession(
            EightyIds, seed: 5, sessionSize: 5, out var selector, out _));

        Assert.AreEqual(selector.SessionQuestionIds[0], selector.GetQuestionIdAt(-3));
    }
}
