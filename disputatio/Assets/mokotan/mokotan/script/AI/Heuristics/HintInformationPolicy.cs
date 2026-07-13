public static class HintInformationPolicy
{
    const string NoviceKey = "HintPolicy_Novice";
    const string IntermediateKey = "HintPolicy_Intermediate";
    const string ExpertKey = "HintPolicy_Expert";

    /// <summary>
    /// Uses <see cref="CheshireLocaleResolver.ResolveCurrentLocale"/> for the active request.
    /// Prefer <see cref="BuildPromptBlock(PlayerSkillProfile, string)"/> when locale is already resolved.
    /// </summary>
    public static string BuildPromptBlock(PlayerSkillProfile profile)
        => BuildPromptBlock(profile, CheshireLocaleResolver.ResolveCurrentLocale());

    public static string BuildPromptBlock(PlayerSkillProfile profile, string locale)
    {
        if (profile == null)
            return string.Empty;

        string key;
        switch (profile.level)
        {
            case PlayerSkillLevel.Novice:
                key = NoviceKey;
                break;
            case PlayerSkillLevel.Intermediate:
                key = IntermediateKey;
                break;
            default:
                key = ExpertKey;
                break;
        }

        string body = CheshirePromptCatalog.Load(key, locale);
        if (string.IsNullOrEmpty(body))
            return string.Empty;

        return "\n\n" + body.Trim();
    }
}
