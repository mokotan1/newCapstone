using Fungus;
using Mokotan.StandingDialogue;
using UnityEngine;

/// <summary>
/// 로그 패널 열기 전 Active SayDialog·StandingDialogue UI 상태를 저장하고 닫을 때 복원한다.
/// Canvas.ForceUpdateCanvases()가 스탠딩 캐릭터 anchoredPosition을 되돌려 대사창 위로 겹치는 현상을 방지한다.
/// </summary>
internal struct DialogueLogSayDialogSnapshot
{
    struct RectLayoutSnapshot
    {
        RectTransform rect;
        int siblingIndex;
        bool active;
        Vector2 anchorMin;
        Vector2 anchorMax;
        Vector2 offsetMin;
        Vector2 offsetMax;
        Vector2 sizeDelta;
        Vector2 anchoredPosition;

        public static RectLayoutSnapshot From(RectTransform source)
        {
            if (source == null)
                return default;

            return new RectLayoutSnapshot
            {
                rect = source,
                siblingIndex = source.GetSiblingIndex(),
                active = source.gameObject.activeSelf,
                anchorMin = source.anchorMin,
                anchorMax = source.anchorMax,
                offsetMin = source.offsetMin,
                offsetMax = source.offsetMax,
                sizeDelta = source.sizeDelta,
                anchoredPosition = source.anchoredPosition,
            };
        }

        public void Apply()
        {
            if (rect == null)
                return;

            rect.gameObject.SetActive(active);
            rect.SetSiblingIndex(siblingIndex);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
        }
    }

    SayDialog sayDialog;
    RectLayoutSnapshot storyTextLayout;
    RectLayoutSnapshot sayDialogCharacterLayout;
    RectLayoutSnapshot leftCharacterLayout;
    RectLayoutSnapshot rightCharacterLayout;
    RectLayoutSnapshot leftSlotLayout;
    RectLayoutSnapshot rightSlotLayout;

    public static DialogueLogSayDialogSnapshot Capture()
    {
        var snapshot = new DialogueLogSayDialogSnapshot();
        snapshot.sayDialog = SayDialog.ActiveSayDialog;
        if (snapshot.sayDialog != null)
        {
            snapshot.storyTextLayout = RectLayoutSnapshot.From(snapshot.sayDialog.StoryTextRectTrans);
            var characterImage = snapshot.sayDialog.CharacterImage;
            snapshot.sayDialogCharacterLayout = RectLayoutSnapshot.From(
                characterImage != null ? characterImage.rectTransform : null);
        }

        StandingDialogueManager standing = StandingDialogueManager.Instance;
        if (standing != null)
        {
            snapshot.leftCharacterLayout = RectLayoutSnapshot.From(standing.LeftCharacterRect);
            snapshot.rightCharacterLayout = RectLayoutSnapshot.From(standing.RightCharacterRect);
            snapshot.leftSlotLayout = RectLayoutSnapshot.From(standing.LeftSlotRect);
            snapshot.rightSlotLayout = RectLayoutSnapshot.From(standing.RightSlotRect);
        }

        return snapshot;
    }

    public void Restore()
    {
        ApplyLayouts();
        Canvas.ForceUpdateCanvases();
        ApplyLayouts();
    }

    void ApplyLayouts()
    {
        leftSlotLayout.Apply();
        rightSlotLayout.Apply();
        leftCharacterLayout.Apply();
        rightCharacterLayout.Apply();
        sayDialogCharacterLayout.Apply();
        storyTextLayout.Apply();
    }
}
