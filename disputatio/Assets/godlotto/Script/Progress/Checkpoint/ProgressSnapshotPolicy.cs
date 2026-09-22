using System;

public static class ProgressSnapshotPolicy
{
    public static bool ShouldCapturePlayerPrefsKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        if (string.Equals(key, SettingPlayerPrefsKeys.BgmVolume, StringComparison.Ordinal) ||
            string.Equals(key, SettingPlayerPrefsKeys.SfxVolume, StringComparison.Ordinal) ||
            string.Equals(key, SettingPlayerPrefsKeys.Fullscreen, StringComparison.Ordinal) ||
            string.Equals(key, SettingPlayerPrefsKeys.ResolutionIndex, StringComparison.Ordinal) ||
            string.Equals(key, FungusVariableKeys.IsClicked, StringComparison.Ordinal) ||
            string.Equals(key, FungusVariableKeys.WindowClicked, StringComparison.Ordinal))
        {
            return false;
        }

        if (key.StartsWith("Dial_", StringComparison.Ordinal) ||
            key.StartsWith("SnapState_", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}
