using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 슬라이더 등 0~1 선형 볼륨을 AudioMixer Exposed Parameter(dB)로 변환합니다.
/// </summary>
public static class AudioMixerVolumeUtility
{
    private const float SilentDecibels = -80f;
    private const float DecibelsPerDecade = 20f;

    public static float Linear01ToDecibels(float linear01)
    {
        if (linear01 <= 0f)
            return SilentDecibels;
        return Mathf.Log10(linear01) * DecibelsPerDecade;
    }

    public static void SetExposedVolume(AudioMixer mixer, string exposedParameterName, float linear01)
    {
        if (mixer == null || string.IsNullOrEmpty(exposedParameterName))
            return;
        mixer.SetFloat(exposedParameterName, Linear01ToDecibels(linear01));
    }
}

/// <summary>
/// 설정 화면에서 사용하는 해상도 목록: 동일 (가로×세로)당 최고 주사율 1개만 남기고,
/// 프로젝트에서 허용한 일반 해상도만 필터링합니다.
/// </summary>
public static class ResolutionListUtility
{
    private static readonly Vector2Int[] PreferredSizes =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1366, 768),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160),
    };

    public static List<Resolution> BuildPreferredResolutionList()
    {
        Resolution[] all = Screen.resolutions;
        List<Resolution> unique = all
            .GroupBy(r => new { r.width, r.height })
            .Select(g => g.OrderByDescending(r => r.refreshRateRatio.value).First())
            .OrderBy(r => r.width)
            .ThenBy(r => r.height)
            .ToList();

        List<Resolution> preferred = unique
            .Where(r => PreferredSizes.Any(p => p.x == r.width && p.y == r.height))
            .ToList();

        if (preferred.Count > 0)
            return preferred;

        if (unique.Count > 0)
            return unique;

        int fallbackWidth = Screen.currentResolution.width > 0 ? Screen.currentResolution.width : Screen.width;
        int fallbackHeight = Screen.currentResolution.height > 0 ? Screen.currentResolution.height : Screen.height;
        if (fallbackWidth <= 0)
            fallbackWidth = 1920;
        if (fallbackHeight <= 0)
            fallbackHeight = 1080;

        return new List<Resolution>
        {
            new Resolution
            {
                width = fallbackWidth,
                height = fallbackHeight
            }
        };
    }

    public static List<string> BuildLabels(IReadOnlyList<Resolution> resolutions)
    {
        var list = new List<string>(resolutions.Count);
        for (int i = 0; i < resolutions.Count; i++)
        {
            Resolution r = resolutions[i];
            list.Add($"{r.width} x {r.height}");
        }

        return list;
    }
}
