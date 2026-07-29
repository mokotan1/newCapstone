using System;
using System.Collections.Generic;

/// <summary>
/// Picks a fixed set of unique question IDs for one TutorRoom quiz session via a single seedable
/// shuffle. The session's IDs never change afterwards — wrong answers/re-asks do not reshuffle
/// (design §4: "세션 선택기는 시작 시 seed 가능한 셔플을 한 번 수행하여 중복 없는 5개 ID를 고정한다").
/// </summary>
internal sealed class TutorQuizSessionSelector
{
    private readonly List<string> _sessionQuestionIds;

    private TutorQuizSessionSelector(List<string> sessionQuestionIds)
    {
        _sessionQuestionIds = sessionQuestionIds;
    }

    /// <summary>Fixed session order, length == the requested session size.</summary>
    public IReadOnlyList<string> SessionQuestionIds => _sessionQuestionIds;

    /// <summary>
    /// Attempts to build a session selector: dedupes <paramref name="validQuestionIds"/>, shuffles once
    /// with <paramref name="seed"/> (Fisher-Yates), and takes the first <paramref name="sessionSize"/>.
    /// Fails (returns false) when fewer than <paramref name="sessionSize"/> unique IDs are available —
    /// callers must surface a localized error and unlock input rather than get stuck (design §4).
    /// </summary>
    public static bool TrySelectSession(
        IReadOnlyList<string> validQuestionIds,
        int seed,
        int sessionSize,
        out TutorQuizSessionSelector selector,
        out string error)
    {
        selector = null;
        error = null;

        if (sessionSize <= 0)
        {
            error = $"sessionSize must be positive (was {sessionSize}).";
            return false;
        }

        List<string> distinct = DistinctPreserveOrder(validQuestionIds);
        if (distinct.Count < sessionSize)
        {
            error = $"insufficient valid questions: need {sessionSize}, have {distinct.Count}.";
            return false;
        }

        List<string> shuffled = new List<string>(distinct);
        var rng = new Random(seed);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var sessionIds = shuffled.GetRange(0, sessionSize);
        selector = new TutorQuizSessionSelector(sessionIds);
        return true;
    }

    /// <summary>Clamped lookup — index beyond the session just repeats the last question.</summary>
    public string GetQuestionIdAt(int index)
    {
        if (_sessionQuestionIds.Count == 0)
            return null;
        int clamped = index < 0 ? 0 : index >= _sessionQuestionIds.Count ? _sessionQuestionIds.Count - 1 : index;
        return _sessionQuestionIds[clamped];
    }

    private static List<string> DistinctPreserveOrder(IReadOnlyList<string> ids)
    {
        var result = new List<string>();
        if (ids == null)
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < ids.Count; i++)
        {
            string id = (ids[i] ?? "").Trim();
            if (id.Length == 0)
                continue;
            if (seen.Add(id))
                result.Add(id);
        }
        return result;
    }
}
