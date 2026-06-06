using UnityEngine;

/// <summary>
/// 선택지 메뉴 패널을 화면 어디에 정렬할지 정하는 9분할 앵커 프리셋.
/// </summary>
public enum MenuAnchor
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight
}

/// <summary>
/// <see cref="MenuAnchor"/>를 uGUI RectTransform의 anchor/pivot 값으로 변환하는 순수 헬퍼.
/// MonoBehaviour 의존이 없어 EditMode에서 단독 테스트 가능합니다.
/// </summary>
public static class MenuAnchorLayout
{
    /// <summary>앵커 프리셋을 anchorMin/anchorMax/pivot 값으로 변환합니다(min==max==pivot).</summary>
    public static void Resolve(MenuAnchor anchor, out Vector2 min, out Vector2 max, out Vector2 pivot)
    {
        float x = HorizontalFactor(anchor);
        float y = VerticalFactor(anchor);
        min = new Vector2(x, y);
        max = new Vector2(x, y);
        pivot = new Vector2(x, y);
    }

    /// <summary>RectTransform에 앵커 프리셋을 적용하고 오프셋을 anchoredPosition으로 설정합니다.</summary>
    public static void Apply(RectTransform target, MenuAnchor anchor, Vector2 offset)
    {
        Resolve(anchor, out var min, out var max, out var pivot);
        target.anchorMin = min;
        target.anchorMax = max;
        target.pivot = pivot;
        target.anchoredPosition = offset;
    }

    static float HorizontalFactor(MenuAnchor a)
    {
        switch (a)
        {
            case MenuAnchor.TopLeft:
            case MenuAnchor.MiddleLeft:
            case MenuAnchor.BottomLeft:
                return 0f;
            case MenuAnchor.TopRight:
            case MenuAnchor.MiddleRight:
            case MenuAnchor.BottomRight:
                return 1f;
            default:
                return 0.5f;
        }
    }

    static float VerticalFactor(MenuAnchor a)
    {
        switch (a)
        {
            case MenuAnchor.TopLeft:
            case MenuAnchor.TopCenter:
            case MenuAnchor.TopRight:
                return 1f;
            case MenuAnchor.BottomLeft:
            case MenuAnchor.BottomCenter:
            case MenuAnchor.BottomRight:
                return 0f;
            default:
                return 0.5f;
        }
    }
}
