using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 항목 프리팹 1개에 화자·본문·구분선을 분리 바인딩한다.
/// 없으면 <see cref="DialogueLogPanel"/>이 기존 단일 TMP + <see cref="DialogueLogLogic.FormatEntry"/>로 폴백한다.
/// </summary>
public class DialogueLogEntryView : MonoBehaviour
{
    [SerializeField] private DialogueLogVisualStyle style = DialogueLogVisualStyle.ParchmentCodex;
    [SerializeField] private GameObject speakerRoot;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private Image speakerLine;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private Image entrySeparator;

    public DialogueLogVisualStyle Style => style;

    public void Bind(DialogueLogEntry entry) => Bind(entry, DialogueLogStylePalette.ForStyle(style));

    public void Bind(DialogueLogEntry entry, DialogueLogStylePalette palette)
    {
        bool isNarration = DialogueLogLogic.IsNarration(entry);

        if (speakerRoot != null)
            speakerRoot.SetActive(!isNarration);

        if (speakerLine != null)
            speakerLine.gameObject.SetActive(!isNarration);

        if (speakerLabel != null)
        {
            if (isNarration)
            {
                speakerLabel.text = string.Empty;
            }
            else
            {
                speakerLabel.richText = style == DialogueLogVisualStyle.ParchmentCodex;
                speakerLabel.text = style == DialogueLogVisualStyle.ParchmentCodex
                    ? DialogueLogLogic.FormatSpeakerRichText(entry.Speaker, style)
                    : DialogueLogLogic.FormatSpeakerLine(entry.Speaker, style);
                speakerLabel.color = palette.SpeakerColor;
                speakerLabel.fontStyle = style == DialogueLogVisualStyle.ParchmentCodex
                    ? FontStyles.Bold
                    : FontStyles.Normal;
                speakerLabel.characterSpacing = style == DialogueLogVisualStyle.DarkConfession ? 3f : speakerLabel.characterSpacing;
            }
        }

        if (speakerLine != null && !isNarration)
            speakerLine.color = palette.TitleUnderline;

        if (bodyLabel != null)
        {
            bodyLabel.text = entry.Text;
            bodyLabel.color = isNarration ? palette.NarrationColor : palette.BodyColor;
            bodyLabel.fontStyle = isNarration ? FontStyles.Italic : FontStyles.Normal;
        }

        DialogueLogTypography.ApplyEntryTypography(style, speakerLabel, bodyLabel);
        DialogueLogTypography.ApplyEntryLayout(
            transform as RectTransform,
            style,
            speakerLabel,
            bodyLabel,
            entrySeparator);

        if (entrySeparator != null)
            entrySeparator.color = palette.EntrySeparator;
    }
}
