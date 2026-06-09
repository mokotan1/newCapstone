using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 미니멀 고스트 LogButton hover 처리: 밑줄 표시·아이콘·캡션 강조색 전환.
/// 스펙: docs/dialogue-log-button-03-ghost.spec.json underline.visibleStates = ["hover"]
/// </summary>
public class DialogueLogGhostButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] GameObject underline;
    [SerializeField] Graphic iconGraphic;
    [SerializeField] TMP_Text caption;
    [SerializeField] Color idleColor;
    [SerializeField] Color hoverColor;

    public void Initialize(GameObject underlineRoot, Graphic icon, TMP_Text captionLabel, Color idle, Color hover)
    {
        underline = underlineRoot;
        iconGraphic = icon;
        caption = captionLabel;
        idleColor = idle;
        hoverColor = hover;
        ApplyIdleState();
    }

    void OnEnable() => ApplyIdleState();

    public void OnPointerEnter(PointerEventData eventData) => ApplyHoverState();

    public void OnPointerExit(PointerEventData eventData) => ApplyIdleState();

    void ApplyHoverState()
    {
        if (underline != null)
            underline.SetActive(true);
        ApplyColor(hoverColor);
    }

    void ApplyIdleState()
    {
        if (underline != null)
            underline.SetActive(false);
        ApplyColor(idleColor);
    }

    void ApplyColor(Color color)
    {
        if (iconGraphic != null)
            iconGraphic.color = color;
        if (caption != null)
            caption.color = color;
    }
}
