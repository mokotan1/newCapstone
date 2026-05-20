using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central book overlay reader. Splits one long text into readable pages and wires page buttons.
/// </summary>
public class BookOverlayPagedReader : MonoBehaviour
{
    [Header("Text Targets")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Text pageIndicatorText;

    [Header("Buttons")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button closeButton;

    [Header("Content")]
    [SerializeField] private string title = "찢어진 책의 메모";
    [TextArea(8, 24)]
    [SerializeField] private string fullText =
        "잉크가 번진 문장 사이로 같은 이름이 반복되어 있다.\n\n" +
        "마지막 줄만 유난히 선명하다.\n" +
        "\"불을 너무 오래 두지 말 것. 그 냄새를 맡은 사람은 반드시 뒤를 돌아본다.\"";

    [Header("Paging")]
    [Tooltip("한 페이지에 들어갈 최대 글자 수입니다. 줄바꿈과 공백도 포함합니다.")]
    [SerializeField] private int maxCharactersPerPage = 170;
    [SerializeField] private bool resetToFirstPageOnOpen = true;

    private readonly List<string> pages = new List<string>();
    private int currentPageIndex;
    private bool lastPageShownSinceOpen;

    public event Action<BookOverlayPagedReader> LastPageShown;
    public event Action<BookOverlayPagedReader> Closed;
    public event Action<BookOverlayPagedReader, int, int> PageShown;
    public bool HasShownLastPageSinceOpen => lastPageShownSinceOpen;
    public int CurrentPageIndex => currentPageIndex;
    public int PageCount => pages.Count;

    private void Awake()
    {
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousPage);
        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        RebuildPages();
        if (resetToFirstPageOnOpen)
            currentPageIndex = 0;
        lastPageShownSinceOpen = false;
        ShowPage(currentPageIndex);
    }

    private void OnDestroy()
    {
        if (previousButton != null)
            previousButton.onClick.RemoveListener(PreviousPage);
        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextPage);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    public void SetContent(string newTitle, string newFullText)
    {
        title = newTitle;
        fullText = newFullText;
        currentPageIndex = 0;
        lastPageShownSinceOpen = false;
        RebuildPages();
        ShowPage(currentPageIndex);
    }

    public void SetMaxCharactersPerPage(int value)
    {
        maxCharactersPerPage = Mathf.Max(40, value);
        RebuildPages();
        ShowPage(currentPageIndex);
    }

    public void NextPage()
    {
        if (currentPageIndex >= pages.Count - 1)
            return;
        currentPageIndex++;
        ShowPage(currentPageIndex);
    }

    public void PreviousPage()
    {
        if (currentPageIndex <= 0)
            return;
        currentPageIndex--;
        ShowPage(currentPageIndex);
    }

    public void Close()
    {
        Closed?.Invoke(this);
        gameObject.SetActive(false);
        ClickInteractionCleanup.ResetAfterUiBoundary();
    }

    private void RebuildPages()
    {
        pages.Clear();

        int limit = Mathf.Max(40, maxCharactersPerPage);
        string source = string.IsNullOrWhiteSpace(fullText) ? string.Empty : fullText.Trim();
        if (string.IsNullOrEmpty(source))
        {
            pages.Add(string.Empty);
            return;
        }

        string normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalized.IndexOf('\f') >= 0)
        {
            string[] forcedPageGroups = normalized.Split('\f');
            foreach (string forcedPageGroup in forcedPageGroups)
            {
                string forcedPage = forcedPageGroup.Trim();
                if (!string.IsNullOrEmpty(forcedPage))
                    pages.Add(forcedPage);
            }
        }
        else
        {
            AddPagesFromTextGroup(normalized, limit);
        }

        if (pages.Count == 0)
            pages.Add(string.Empty);

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, pages.Count - 1);
    }

    private void AddPagesFromTextGroup(string textGroup, int limit)
    {
        string[] paragraphs = textGroup.Split(new[] { "\n\n" }, System.StringSplitOptions.None);
        var page = new StringBuilder(limit + 32);

        foreach (string rawParagraph in paragraphs)
        {
            string paragraph = rawParagraph.Trim();
            if (paragraph.Length == 0)
                continue;

            if (paragraph.Length > limit)
            {
                FlushPage(page);
                SplitLongParagraph(paragraph, limit);
                continue;
            }

            int extra = page.Length == 0 ? paragraph.Length : paragraph.Length + 2;
            if (page.Length > 0 && page.Length + extra > limit)
                FlushPage(page);

            if (page.Length > 0)
                page.Append("\n\n");
            page.Append(paragraph);
        }

        FlushPage(page);
    }

    private void SplitLongParagraph(string paragraph, int limit)
    {
        int start = 0;
        while (start < paragraph.Length)
        {
            int length = Mathf.Min(limit, paragraph.Length - start);
            int split = FindSplitPoint(paragraph, start, length);
            pages.Add(paragraph.Substring(start, split - start).Trim());
            start = split;
            while (start < paragraph.Length && char.IsWhiteSpace(paragraph[start]))
                start++;
        }
    }

    private static int FindSplitPoint(string text, int start, int length)
    {
        int end = start + length;
        if (end >= text.Length)
            return text.Length;

        for (int i = end - 1; i > start + length / 2; i--)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == '.' || c == ',' || c == '!' || c == '?' || c == '。' || c == '，')
                return i + 1;
        }

        return end;
    }

    private void FlushPage(StringBuilder page)
    {
        if (page.Length == 0)
            return;

        pages.Add(page.ToString().Trim());
        page.Length = 0;
    }

    private void ShowPage(int index)
    {
        if (titleText != null)
            titleText.text = title;
        if (bodyText != null)
            bodyText.text = pages.Count == 0 ? string.Empty : pages[Mathf.Clamp(index, 0, pages.Count - 1)];
        if (pageIndicatorText != null)
            pageIndicatorText.text = pages.Count == 0 ? "0 / 0" : string.Format("{0} / {1}", currentPageIndex + 1, pages.Count);

        bool hasPrevious = currentPageIndex > 0;
        bool hasNext = currentPageIndex < pages.Count - 1;
        if (previousButton != null)
            previousButton.interactable = hasPrevious;
        if (nextButton != null)
            nextButton.interactable = hasNext;

        PageShown?.Invoke(this, currentPageIndex, pages.Count);

        if (pages.Count > 0 && currentPageIndex >= pages.Count - 1 && !lastPageShownSinceOpen)
        {
            lastPageShownSinceOpen = true;
            LastPageShown?.Invoke(this);
        }
    }
}
