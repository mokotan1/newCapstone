using System;

namespace Godlotto.Sequence
{
    /// <summary>
    /// Sequence 문서가 set_bool/set_string으로 기록하는 종료 outcome 플래그. 전용 명령 없이 BlockOutcome과 동일 의미.
    /// </summary>
    public static class SequenceBlockOutcomeMapper
    {
        public const string GoBackKey = "__sequence.outcome.go_back";
        public const string LoadSceneKey = "__sequence.outcome.load_scene";

        public static bool IsEphemeralOutcomeKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return string.Equals(key, GoBackKey, StringComparison.Ordinal)
                   || string.Equals(key, LoadSceneKey, StringComparison.Ordinal);
        }

        public static bool ShouldGoBack(FlagStore flags)
        {
            return flags != null && flags.Has(GoBackKey) && flags.GetBool(GoBackKey);
        }

        public static bool TryGetLoadScene(FlagStore flags, out string sceneName)
        {
            sceneName = null;
            if (flags == null || !flags.Has(LoadSceneKey))
                return false;

            sceneName = flags.GetString(LoadSceneKey);
            return !string.IsNullOrWhiteSpace(sceneName);
        }

        public static void Clear(FlagStore flags)
        {
            if (flags == null)
                return;

            if (flags.Has(GoBackKey))
                flags.SetBool(GoBackKey, false);
            if (flags.Has(LoadSceneKey))
                flags.SetString(LoadSceneKey, string.Empty);
        }
    }
}
