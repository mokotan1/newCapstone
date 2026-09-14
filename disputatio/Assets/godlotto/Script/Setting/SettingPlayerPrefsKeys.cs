/// <summary>
/// 그래픽·오디오 설정용 PlayerPrefs 키. 문자열 오타 방지 및 전 씬 공통 사용.
/// BgmVolume / SfxVolume 은 AudioMixer Exposed 파라미터 이름과 동일합니다.
/// 값은 기존 저장 데이터와 호환되도록 변경하지 마세요.
/// </summary>
public static class SettingPlayerPrefsKeys
{
    public const string BgmVolume = "BGMVolume";
    public const string SfxVolume = "SFXVolume";
    public const string Fullscreen = "Fullscreen";
    public const string ResolutionIndex = "ResolutionIndex";

    /// <summary>Cheshire answer size: 100, 120, or 140. New key — does not change audio/video keys.</summary>
    public const string AnswerTextScale = "CheshireAnswerTextScale";

    public const float DefaultLinearVolume = 0.75f;
    public const int FullscreenDefaultEnabled = 1;
}
