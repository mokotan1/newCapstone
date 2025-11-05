using UnityEngine;
using Fungus;
using UnityEngine.UI;

/// <summary>
/// 저장 슬롯 이미지 변경 스크립트.
/// Fungus Flowchart의 "CurrentScene" 문자열 변수에 따라 슬롯 이미지(sprite)를 교체.
/// </summary>
public class ChangeSP : MonoBehaviour
{
    [Header("Fungus 연동")]
    public Flowchart flowchart;

    [Header("UI 요소")]
    public Button slot;

    [Header("씬별 슬롯 이미지 리스트 (씬 순서에 맞게 지정)")]
    public Sprite[] sprite;

    /// <summary>
    /// 저장 슬롯 이미지 변경 (저장 시 호출)
    /// </summary>
    public void OnChangeButtonImage()
    {
        if (flowchart == null || slot == null || sprite == null || sprite.Length == 0)
        {
            Debug.LogWarning("ChangeSP: 필수 요소가 연결되지 않았습니다.");
            return;
        }

        string sceneName = flowchart.GetStringVariable("CurrentScene");
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("ChangeSP: Flowchart의 CurrentScene 변수가 비어 있습니다.");
            return;
        }

        Debug.Log($"🖼️ ChangeSP: 현재 씬 이름 = {sceneName}");

        // 씬 이름에 따라 슬롯 이미지 변경
        switch (sceneName)
        {
            case "Opening":
                slot.image.sprite = sprite[0];
                break;

            case "Bedroom":
                slot.image.sprite = sprite[1];
                break;

            case "Basement":
                slot.image.sprite = sprite[2];
                break;

            case "Library":
                slot.image.sprite = sprite[3];
                break;

            case "Attic":
                slot.image.sprite = sprite[4];
                break;

            case "SecretRoom":
                slot.image.sprite = sprite[5];
                break;

            case "Ending":
                slot.image.sprite = sprite[6];
                break;

            default:
                Debug.LogWarning($"ChangeSP: {sceneName} 에 해당하는 스프라이트가 없습니다.");
                break;
        }
    }
}
