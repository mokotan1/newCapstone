using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Lets the MagnifierLoupe item activate a draggable loupe overlay on LockPanel.
/// The overlay reveals tiny PAGE letters only when the lens is close to them.
/// </summary>
public class LockPanelMagnifierDropZone : MonoBehaviour, IDropHandler
{
    [Header("Required Item")]
    public Item requiredItem;

    [Header("Loupe Overlay")]
    public GameObject magnifierObject;
    public RectTransform boundsRect;
    public Vector2 startPosition = new Vector2(-360f, 170f);
    public float revealRadius = 88f;

    [Header("Micro Letters")]
    public string hintLetters = "PAGE";
    public Color hiddenLetterColor = new Color(0.79f, 0.70f, 0.50f, 0.045f);
    public Color revealedLetterColor = new Color(0.93f, 0.84f, 0.58f, 1f);
    public float hiddenFontSize = 8f;
    public float revealedFontSize = 72f;

    private RectTransform magnifierRect;
    private TMP_Text[] letterTexts;
    private RectTransform[] letterRects;

    private static readonly Vector2[] DefaultLetterPositions =
    {
        new Vector2(-470f, 178f),
        new Vector2(-170f, -132f),
        new Vector2(205f, 126f),
        new Vector2(465f, -185f),
    };

    private void Awake()
    {
        if (boundsRect == null)
            boundsRect = transform as RectTransform;

        PrepareMagnifier();
        EnsureLetters();
        SetMagnifierActive(false);
    }

    private void OnEnable()
    {
        EnsureLetters();
        RefreshLetterVisibility();
    }

    private void Update()
    {
        RefreshLetterVisibility();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (requiredItem == null || InventorySlot.draggedItem != requiredItem)
            return;

        ActivateMagnifier();
        InventorySlot.ClearDragState();
    }

    public void ActivateMagnifier()
    {
        PrepareMagnifier();
        EnsureLetters();
        SetMagnifierActive(true);

        if (magnifierRect != null)
        {
            magnifierRect.SetAsLastSibling();
            magnifierRect.anchoredPosition = startPosition;

            var drag = magnifierRect.GetComponent<FilterCardBoundedDrag>();
            if (drag == null)
                drag = magnifierRect.gameObject.AddComponent<FilterCardBoundedDrag>();

            drag.SetBounds(boundsRect);
            drag.NotifyExternalDragEnded();
        }

        RefreshLetterVisibility();
    }

    private void PrepareMagnifier()
    {
        if (magnifierObject == null)
            return;

        magnifierRect = magnifierObject.GetComponent<RectTransform>();
        if (magnifierRect == null)
            return;

        if (magnifierObject.GetComponent<FilterCardBoundedDrag>() == null)
            magnifierObject.AddComponent<FilterCardBoundedDrag>().SetBounds(boundsRect);

        Image image = magnifierObject.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = true;
    }

    private void EnsureLetters()
    {
        if (letterTexts != null && letterTexts.Length == hintLetters.Length)
            return;

        letterTexts = new TMP_Text[hintLetters.Length];
        letterRects = new RectTransform[hintLetters.Length];

        for (int i = 0; i < hintLetters.Length; i++)
        {
            GameObject letterObject = new GameObject("MagnifierMicroLetter_" + hintLetters[i]);
            letterObject.transform.SetParent(transform, false);

            var rect = letterObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(120f, 120f);
            rect.anchoredPosition = i < DefaultLetterPositions.Length
                ? DefaultLetterPositions[i]
                : Vector2.zero;

            var text = letterObject.AddComponent<TextMeshProUGUI>();
            text.text = hintLetters[i].ToString();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = hiddenFontSize;
            text.color = hiddenLetterColor;

            letterRects[i] = rect;
            letterTexts[i] = text;
        }
    }

    private void RefreshLetterVisibility()
    {
        if (letterTexts == null || letterRects == null)
            return;

        bool loupeActive = magnifierObject != null && magnifierObject.activeInHierarchy && magnifierRect != null;
        Vector2 lensPosition = loupeActive ? magnifierRect.anchoredPosition : Vector2.positiveInfinity;

        for (int i = 0; i < letterTexts.Length; i++)
        {
            if (letterTexts[i] == null || letterRects[i] == null)
                continue;

            float distance = Vector2.Distance(lensPosition, letterRects[i].anchoredPosition);
            float t = loupeActive ? Mathf.Clamp01(1f - distance / revealRadius) : 0f;

            letterTexts[i].color = Color.Lerp(hiddenLetterColor, revealedLetterColor, t);
            letterTexts[i].fontSize = Mathf.Lerp(hiddenFontSize, revealedFontSize, t);
        }

        if (magnifierRect != null)
            magnifierRect.SetAsLastSibling();
    }

    private void SetMagnifierActive(bool active)
    {
        if (magnifierObject != null)
            magnifierObject.SetActive(active);
    }
}
