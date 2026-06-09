using Fungus;
using UnityEngine;

/// <summary>
/// 로그 패널 열기 전 Active SayDialog UI 상태를 저장하고 닫을 때 복원한다.
/// 캐릭터 일러스트 sibling/레이아웃이 대사창 위로 올라오는 현상을 방지한다.
/// </summary>
internal struct DialogueLogSayDialogSnapshot
{
    SayDialog sayDialog;
    RectTransform storyTextRect;
    RectTransform characterRect;
    int storyTextSiblingIndex;
    int characterSiblingIndex;
    bool characterActive;
    Vector2 storyOffsetMin;
    Vector2 storyOffsetMax;
    Vector2 storyAnchorMin;
    Vector2 storyAnchorMax;
    Vector2 storySizeDelta;
    Vector2 storyAnchoredPosition;

    public static DialogueLogSayDialogSnapshot Capture()
    {
        var snapshot = new DialogueLogSayDialogSnapshot();
        snapshot.sayDialog = SayDialog.ActiveSayDialog;
        if (snapshot.sayDialog == null)
            return snapshot;

        snapshot.storyTextRect = snapshot.sayDialog.StoryTextRectTrans;
        var characterImage = snapshot.sayDialog.CharacterImage;
        snapshot.characterRect = characterImage != null ? characterImage.rectTransform : null;

        if (snapshot.storyTextRect != null)
        {
            snapshot.storyTextSiblingIndex = snapshot.storyTextRect.GetSiblingIndex();
            snapshot.storyOffsetMin = snapshot.storyTextRect.offsetMin;
            snapshot.storyOffsetMax = snapshot.storyTextRect.offsetMax;
            snapshot.storyAnchorMin = snapshot.storyTextRect.anchorMin;
            snapshot.storyAnchorMax = snapshot.storyTextRect.anchorMax;
            snapshot.storySizeDelta = snapshot.storyTextRect.sizeDelta;
            snapshot.storyAnchoredPosition = snapshot.storyTextRect.anchoredPosition;
        }

        if (snapshot.characterRect != null)
        {
            snapshot.characterSiblingIndex = snapshot.characterRect.GetSiblingIndex();
            snapshot.characterActive = snapshot.characterRect.gameObject.activeSelf;
        }

        return snapshot;
    }

    public void Restore()
    {
        if (sayDialog == null)
            return;

        if (characterRect != null)
        {
            characterRect.gameObject.SetActive(characterActive);
            characterRect.SetSiblingIndex(characterSiblingIndex);
        }

        if (storyTextRect != null)
        {
            storyTextRect.SetSiblingIndex(storyTextSiblingIndex);
            storyTextRect.anchorMin = storyAnchorMin;
            storyTextRect.anchorMax = storyAnchorMax;
            storyTextRect.offsetMin = storyOffsetMin;
            storyTextRect.offsetMax = storyOffsetMax;
            storyTextRect.sizeDelta = storySizeDelta;
            storyTextRect.anchoredPosition = storyAnchoredPosition;
        }

        Canvas.ForceUpdateCanvases();
    }
}
