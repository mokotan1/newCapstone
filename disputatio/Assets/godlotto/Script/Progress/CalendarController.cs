using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CalendarController : MonoBehaviour
{
    [Header("UI References")]
    public Image calendarImage;
    public Button leftButton;
    public Button rightButton;

    [Header("Month Sprites (1~12월)")]
    public List<Sprite> monthSprites = new List<Sprite>();

    [Range(1, 12)]
    public int defaultStartMonth = 5; // 기본 시작 월 (최초 실행 시만 적용)

    private int currentMonth;

    private const string PREF_KEY = "LastCalendarMonth"; // PlayerPrefs 키 이름

    void Start()
    {
        // ✅ 이전에 저장된 월이 있으면 불러오고, 없으면 기본값(5월)
        currentMonth = PlayerPrefs.GetInt(PREF_KEY, defaultStartMonth);

        leftButton.onClick.AddListener(PrevMonth);
        rightButton.onClick.AddListener(NextMonth);

        UpdateCalendar();
    }

    public void PrevMonth()
    {
        currentMonth--;
        if (currentMonth < 1) currentMonth = 12;
        UpdateCalendar();
        SaveCurrentMonth();
    }

    public void NextMonth()
    {
        currentMonth++;
        if (currentMonth > 12) currentMonth = 1;
        UpdateCalendar();
        SaveCurrentMonth();
    }

    private void UpdateCalendar()
    {
        if (calendarImage != null && monthSprites.Count >= currentMonth)
        {
            calendarImage.sprite = monthSprites[currentMonth - 1];
        }
        else
        {
            GameLog.LogWarning($"달력 이미지를 찾을 수 없습니다. (월: {currentMonth})");
        }
    }

    // ✅ 현재 월을 PlayerPrefs에 저장
    private void SaveCurrentMonth()
    {
        PlayerPrefs.SetInt(PREF_KEY, currentMonth);
        PlayerPrefs.Save(); // 즉시 저장
    }

    // 씬이 종료될 때 자동 저장 (안전장치)
    void OnDestroy()
    {
        SaveCurrentMonth();
    }
}
