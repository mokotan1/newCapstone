using UnityEngine;

/// <summary>
/// 퍼즐북 <see cref="BookPanelController"/>의 특정 페이지에서만 ItemPickup을 표시합니다.
/// </summary>
public class PuzzleBookPageItemGate : MonoBehaviour
{
    [SerializeField] BookPanelController bookPanel;
    [SerializeField] int visibleOnPageIndex = 1;
    [SerializeField] GameObject pickupObject;

    private int lastAppliedPage = int.MinValue;

    private void Awake()
    {
        if (bookPanel == null)
            bookPanel = GetComponent<BookPanelController>();
    }

    private void OnEnable()
    {
        ApplyVisibility(GetCurrentPage());
    }

    private void Update()
    {
        int page = GetCurrentPage();
        if (page != lastAppliedPage)
            ApplyVisibility(page);
    }

    private int GetCurrentPage()
    {
        return bookPanel != null ? bookPanel.CurrentPageIndex : 0;
    }

    private void ApplyVisibility(int page)
    {
        lastAppliedPage = page;
        if (pickupObject == null)
            return;

        bool visible = page == visibleOnPageIndex;
        if (pickupObject.activeSelf != visible)
            pickupObject.SetActive(visible);
    }
}
