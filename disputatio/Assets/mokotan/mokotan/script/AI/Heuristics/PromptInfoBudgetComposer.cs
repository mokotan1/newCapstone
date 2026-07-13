using System;

public static class PromptInfoBudgetComposer
{
    public static event Action<HeuristicDebugSnapshot> OnSnapshotUpdated;

    private static HeuristicDebugSnapshot lastSnapshot;

    public static HeuristicDebugSnapshot LastSnapshot => lastSnapshot;

    public static string Compose(string currentPrompt, HeuristicSignalInput input)
        => Compose(currentPrompt, input, CheshireLocaleResolver.ResolveCurrentLocale());

    public static string Compose(string currentPrompt, HeuristicSignalInput input, string locale)
    {
        PlayerSkillProfile profile = PlayerSkillProfileBuilder.Build(input);
        string policy = HintInformationPolicy.BuildPromptBlock(profile, locale);

        lastSnapshot = new HeuristicDebugSnapshot
        {
            roomName = string.IsNullOrEmpty(input.roomName) ? "Unknown" : input.roomName,
            progressScore = profile.progressScore,
            accuracyScore = profile.accuracyScore,
            stuckScore = profile.stuckScore,
            skillScore = profile.skillScore,
            level = profile.level,
            unsolvedRevisitCount = input.unsolvedRevisitCount,
            revisitIntervalSeconds = input.revisitIntervalSeconds,
            noProgressAfterRevisitCount = input.noProgressAfterRevisitCount,
            reason = profile.reason,
            generatedAtUtc = DateTime.UtcNow.ToString("O")
        };

        OnSnapshotUpdated?.Invoke(lastSnapshot);
        return currentPrompt + policy;
    }
}
