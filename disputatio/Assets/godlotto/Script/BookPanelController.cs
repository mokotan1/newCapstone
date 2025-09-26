using UnityEngine;
using UnityEngine.UI; // UI 관련 기능을 사용하기 위해 추가 (선택사항)

public class BookPanelController : MonoBehaviour
{
    // 유니티 인스펙터 창에서 관리할 페이지 게임오브젝트 배열
    public GameObject[] pages;

    // 현재 보여지고 있는 페이지의 번호 (0부터 시작)
    private int currentPageIndex = 0;

    // Book_Panel이 활성화될 때마다 항상 첫 페이지부터 보여주기 위한 함수
    void OnEnable()
    {
        // 페이지 인덱스를 0으로 초기화
        currentPageIndex = 0;
        // 해당 인덱스의 페이지만 보여주는 함수 호출
        ShowPage(currentPageIndex);
    }

    // 다음 페이지로 넘어가는 함수 (버튼에 연결할 예정)
    public void NextPage()
    {
        // 현재 페이지가 마지막 페이지가 아니라면
        if (currentPageIndex < pages.Length - 1)
        {
            currentPageIndex++; // 페이지 번호를 1 증가
            ShowPage(currentPageIndex);
        }
    }

    // 이전 페이지로 넘어가는 함수 (버튼에 연결할 예정)
    public void PreviousPage()
    {
        // 현재 페이지가 첫 번째 페이지가 아니라면
        if (currentPageIndex > 0)
        {
            currentPageIndex--; // 페이지 번호를 1 감소
            ShowPage(currentPageIndex);
        }
    }

    // 특정 번호의 페이지만 보여주는 핵심 함수
    private void ShowPage(int index)
    {
        // 모든 페이지를 일단 전부 비활성화
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(false);
        }

        // 원하는 번호(index)의 페이지만 활성화
        pages[index].SetActive(true);
    }
}