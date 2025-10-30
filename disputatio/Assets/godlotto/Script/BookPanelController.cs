using UnityEngine;
using UnityEngine.UI;

public class BookPanelController : MonoBehaviour
{
    [Header("페이지 오브젝트 목록")]
    public GameObject[] pages;

    private int currentPageIndex = 0;

    // 책마다 다른 키로 저장하기 위한 변수
    private string PREF_KEY;

    void Awake()
    {
        // ✅ 책(오브젝트) 이름마다 고유한 저장 키를 생성
        PREF_KEY = "LastBookPage_" + gameObject.name;
    }

    void OnEnable()
    {
        // ✅ 저장된 페이지 불러오기 (없으면 첫 페이지)
        currentPageIndex = PlayerPrefs.GetInt(PREF_KEY, 0);
        ShowPage(currentPageIndex);
    }

    public void NextPage()
    {
        if (currentPageIndex < pages.Length - 1)
        {
            currentPageIndex++;
            ShowPage(currentPageIndex);
            SaveCurrentPage();
        }
    }

    public void PreviousPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            ShowPage(currentPageIndex);
            SaveCurrentPage();
        }
    }

    private void ShowPage(int index)
    {
        // 범위 초과 방지
        index = Mathf.Clamp(index, 0, pages.Length - 1);

        // 모든 페이지를 비활성화하고 선택된 페이지만 활성화
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == index);
        }
    }

    private void SaveCurrentPage()
    {
        // ✅ 책 이름별로 페이지 저장
        PlayerPrefs.SetInt(PREF_KEY, currentPageIndex);
        PlayerPrefs.Save();
    }

    void OnDisable()
    {
        // ✅ 씬을 나가거나 비활성화될 때 자동 저장
        SaveCurrentPage();
    }
}
