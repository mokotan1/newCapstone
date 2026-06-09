using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스타일별 패널 루트·스크롤·항목 프리팹 묶음.
/// <see cref="DialogueLogPanel"/>이 <see cref="DialogueLogVisualStyle"/>에 따라 활성 레이어를 선택한다.
/// </summary>
[Serializable]
public class DialogueLogStyleLayer
{
    [Tooltip("이 스타일의 LogPanel 루트(닫을 때 비활성).")]
    public GameObject panelRoot;

    [Tooltip("항목이 쌓이는 ScrollRect.")]
    public ScrollRect scrollRect;

    [Tooltip("이 스타일 전용 DialogueLogEntry 프리팹.")]
    public GameObject entryPrefab;

    public bool IsConfigured => panelRoot != null && scrollRect != null && entryPrefab != null;
}
