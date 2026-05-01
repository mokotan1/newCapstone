using Fungus;
using UnityEngine;

/// <summary>
/// Hall_playerble에 배치 — No40 러너를 보장하고 저택 허브 방문·첫 입성 독백을 시작합니다.
/// 문 닫힘 효과음 등은 여기 인스펙터에서 연결하세요.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Mokotan/UI/No40 Hall Entry Hook")]
public sealed class No40HallPlayerbleEntryHook : MonoBehaviour
{
    [Header("No.40 — 첫 입성")]
    [SerializeField] private AudioClip doorSlamClip;

    [SerializeField] private string firstEntryLineOverride = "";
    [SerializeField] private string firstDeathLineOverride = "";
    [SerializeField] private string bloodPathLineOverride = "";

    [Header("No.40 — 첫 망령 사망 후 독백 UI")]
    [Tooltip("SayDialogNotebook 등 SayDialog 프리팹")]
    [SerializeField] private SayDialog deathLineSayDialogPrefab;

    private void Start()
    {
        No40ConditionalDialogueRunner r = No40ConditionalDialogueRunner.EnsureExists();
        r.ApplyDesignerOverrides(doorSlamClip,
            firstEntryLineOverride,
            firstDeathLineOverride,
            bloodPathLineOverride,
            deathLineSayDialogPrefab);
        r.RegisterMansionHubVisitAndTryFirstEntry();
    }
}
