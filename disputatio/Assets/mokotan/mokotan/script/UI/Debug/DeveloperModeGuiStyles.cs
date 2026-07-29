using UnityEngine;

/// <summary>
/// Dev Mode IMGUI용 스타일 복제본. <see cref="GUI.skin"/> 기본값을 건드리지 않습니다.
/// </summary>
public sealed class DeveloperModeGuiStyles
{
    public GUIStyle Label { get; private set; }
    public GUIStyle Button { get; private set; }
    public GUIStyle TextField { get; private set; }
    public GUIStyle Box { get; private set; }
    public GUIStyle Window { get; private set; }
    public GUIStyle ToggleButton { get; private set; }

    float appliedFontSize = -1f;

    public bool IsReady =>
        Label != null &&
        Button != null &&
        TextField != null &&
        Box != null &&
        Window != null &&
        ToggleButton != null;

    /// <summary>
    /// Marks styles stale so the next <see cref="EnsureBuilt"/> rebuilds them.
    /// Safe to call outside <c>OnGUI</c> (does not touch <see cref="GUI.skin"/>).
    /// </summary>
    public void MarkDirty()
    {
        appliedFontSize = -1f;
        Label = null;
        Button = null;
        TextField = null;
        Box = null;
        Window = null;
        ToggleButton = null;
    }

    /// <summary>스타일이 없거나 글자 크기가 바뀌었을 때만 다시 만듭니다.</summary>
    public void EnsureBuilt(float fontSize)
    {
        if (IsReady && Mathf.Approximately(appliedFontSize, fontSize))
            return;

        Rebuild(fontSize);
    }

    public void Rebuild(float fontSize)
    {
        appliedFontSize = fontSize;

        Label = CreateStyle(GetSkinStyle(GUI.skin?.label), fontSize, wordWrap: true);
        Button = CreateStyle(GetSkinStyle(GUI.skin?.button), fontSize);
        TextField = CreateStyle(GetSkinStyle(GUI.skin?.textField), fontSize);
        Box = CreateStyle(GetSkinStyle(GUI.skin?.box), fontSize);
        Window = CreateStyle(GetSkinStyle(GUI.skin?.window), fontSize);
        ToggleButton = CreateStyle(GetSkinStyle(GUI.skin?.button), fontSize);
    }

    public float ScaledWidth(float referenceWidthAtDefaultFont)
    {
        return DeveloperModeGuiTypography.ScaledLength(referenceWidthAtDefaultFont);
    }

    public float ScaledHeight(float referenceHeightAtDefaultFont)
    {
        return DeveloperModeGuiTypography.ScaledLength(referenceHeightAtDefaultFont);
    }

    static GUIStyle GetSkinStyle(GUIStyle source)
    {
        return source ?? GUIStyle.none;
    }

    static GUIStyle CreateStyle(GUIStyle source, float fontSize, bool wordWrap = false)
    {
        var style = new GUIStyle(source)
        {
            fontSize = Mathf.RoundToInt(fontSize),
            wordWrap = wordWrap,
            clipping = wordWrap ? TextClipping.Clip : source.clipping,
            richText = true,
        };

        if (wordWrap)
        {
            style.padding = new RectOffset(
                source.padding.left,
                source.padding.right,
                source.padding.top + 1,
                source.padding.bottom + 1);
        }

        return style;
    }
}
