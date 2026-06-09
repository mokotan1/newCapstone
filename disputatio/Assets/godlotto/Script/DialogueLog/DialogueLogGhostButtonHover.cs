using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 미니멀 고스트 LogButton hover 처리: 밑줄 표시·아이콘·캡션 강조색 전환.
/// 밑줄을 SetActive로 토글하면 VerticalLayoutGroup 레이아웃이 바뀌어 hitbox가 흔들리므로
/// CanvasGroup alpha만 변경한다.
/// </summary>
public class DialogueLogGhostButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] GameObject underline;
    [SerializeField] Graphic iconGraphic;
    [SerializeField] TMP_Text caption;
    [SerializeField] Color idleColor;
    [SerializeField] Color hoverColor;

    CanvasGroup underlineCanvasGroup;

    public void Initialize(GameObject underlineRoot, Graphic icon, TMP_Text captionLabel, Color idle, Color hover)
    {
        underline = underlineRoot;
        iconGraphic = icon;
        caption = captionLabel;
        idleColor = idle;
        hoverColor = hover;
        EnsureUnderlineLayoutStable();
        ApplyIdleState();
    }

    void OnEnable()
    {
        EnsureUnderlineLayoutStable();
        ApplyIdleState();
    }

    public void OnPointerEnter(PointerEventData eventData) => ApplyHoverState();

    public void OnPointerExit(PointerEventData eventData) => ApplyIdleState();

    void ApplyHoverState()
    {
        SetUnderlineVisible(true);
        ApplyColor(hoverColor);
    }

    void ApplyIdleState()
    {
        SetUnderlineVisible(false);
        ApplyColor(idleColor);
    }

    void EnsureUnderlineLayoutStable()
    {
        if (underline == null)
            return;

        if (!underline.activeSelf)
            underline.SetActive(true);

        underlineCanvasGroup = underline.GetComponent<CanvasGroup>();
        if (underlineCanvasGroup == null)
            underlineCanvasGroup = underline.AddComponent<CanvasGroup>();
    }

    void SetUnderlineVisible(bool visible)
    {
        EnsureUnderlineLayoutStable();
        if (underlineCanvasGroup != null)
            underlineCanvasGroup.alpha = visible ? 1f : 0f;
    }

    void ApplyColor(Color color)
    {
        if (iconGraphic != null)
            iconGraphic.color = color;
        if (caption != null)
            caption.color = color;
    }
}
